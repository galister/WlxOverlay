using Newtonsoft.Json;
using Valve.VR;
using WlxOverlay.Core;
using WlxOverlay.Core.Interactions;
using WlxOverlay.Core.Interactions.Internal;
using WlxOverlay.Numerics;
using WlxOverlay.Types;

namespace WlxOverlay.Backend.OVR;

public class OVRInput : IXrInput, IDisposable
{
    private string? _actionSet;
    private ulong _actionSetHandle;

    public readonly Dictionary<string, bool[]> BooleanState = new();
    public readonly Dictionary<string, Vector3[]> Vector3State = new();
    public readonly Dictionary<string, Transform3D> PoseState = new();
    public readonly List<TrackedDevice> DeviceStates = new();

    private readonly List<OpenVrInputAction> _inputActions = new();

    private readonly string[] _inputSources = { "/user/hand/left", "/user/hand/right", "/user/head" };
    private readonly ulong[] _inputSourceHandles = new ulong[3];

    private InputDigitalActionData_t _digitalActionData;
    private readonly uint _digitalActionDataSize;
    private InputAnalogActionData_t _analogActionData;
    private readonly uint _analogActionDataSize;
    private InputPoseActionData_t _poseActionData;
    private readonly uint _poseActionDataSize;

    private readonly TrackedDevicePose_t[] _poses = new TrackedDevicePose_t[OpenVR.k_unMaxTrackedDeviceCount];
    private readonly TrackedDevicePose_t[] _gamePoses = new TrackedDevicePose_t[0];

    private readonly ulong[] _hapticsHandles = new ulong[2];

    private VRActiveActionSet_t[]? _activeActionSets;
    private readonly uint _activeActionSetsSize;

    private readonly Dictionary<(LeftRight hand, string action), FileStream> _exportFiles = new();

    private readonly TrackedDevice[] _controllers = new TrackedDevice[2];
    private TrackedDevice _hmd;

    private DateTime _nextDevicesUpdate = DateTime.MinValue;

    internal OVRInput()
    {
        _digitalActionDataSize = (uint)Marshal.SizeOf(typeof(InputDigitalActionData_t));
        _analogActionDataSize = (uint)Marshal.SizeOf(typeof(InputAnalogActionData_t));
        _poseActionDataSize = (uint)Marshal.SizeOf(typeof(InputPoseActionData_t));

        _activeActionSetsSize = (uint)Marshal.SizeOf(typeof(VRActiveActionSet_t));

        var error = EVRInitError.None;
        OpenVR.GetGenericInterface(OpenVR.IVRInput_Version, ref error);
        if (error.TryPrint())
            Environment.Exit(1);

        Console.WriteLine($"{OpenVR.IVRInput_Version}: pass");

        LoadActionSets();
        InitExportFileHandles();
    }

    private void LoadActionSets()
    {
        if (!Config.TryGetFile("actions.json", out var actionsPath, true))
            return;

        var err = OpenVR.Input.SetActionManifestPath(actionsPath);
        if (err.TryPrint("SetActionManifestPath"))
            Environment.Exit(1);

        var actionsJson = JsonConvert.DeserializeObject<Actions>(File.ReadAllText(actionsPath));
        _actionSet = "/actions/default";

        var stringToActionType = Enum.GetValues<OpenVrInputActionType>()
            .ToDictionary(x => x.ToString().ToLowerInvariant());
        foreach (var action in actionsJson!.actions!)
        {
            if (action.type == "vibration")
                continue;

            var inputAction = new OpenVrInputAction(action.name!, stringToActionType[action.type!]);
            inputAction.Initialize();

            switch (inputAction.Type)
            {
                case OpenVrInputActionType.Boolean:
                    BooleanState[inputAction.Name] = new[] { false, false };
                    break;
                case OpenVrInputActionType.Pose:
                    PoseState[inputAction.Name] = Transform3D.Identity;
                    break;
                case OpenVrInputActionType.Single:
                case OpenVrInputActionType.Vector1:
                case OpenVrInputActionType.Vector2:
                case OpenVrInputActionType.Vector3:
                    Vector3State[inputAction.Name] = new[] { Vector3.Zero, Vector3.Zero };
                    break;
            }

            _inputActions.Add(inputAction);
        }
        Console.WriteLine($"Loaded {_inputActions.Count} input actions for {_actionSet}");

        err = OpenVR.Input.GetActionSetHandle(_actionSet, ref _actionSetHandle);
        if (err.TryPrint("GetActionSetHandle", _actionSet))
            Environment.Exit(1);

        _activeActionSets = new VRActiveActionSet_t[]
        {
            new()
            {
                ulActionSet = _actionSetHandle,
                ulRestrictedToDevice = OpenVR.k_ulInvalidInputValueHandle,
            }
        };

        GetHandles();
    }

    private void GetHandles()
    {
        for (var i = 0; i < _inputSources.Length; i++)
        {
            var path = _inputSources[i];
            var handle = 0UL;
            var err = OpenVR.Input.GetInputSourceHandle(path, ref handle);
            if (err.TryPrint("GetInputSources", path))
                continue;

            _inputSourceHandles[i] = handle;
        }

        for (var hand = LeftRight.Left; hand <= LeftRight.Right; hand++)
        {
            var handle = 0UL;
            var path = $"/actions/default/out/Haptics{hand}";

            var err = OpenVR.Input.GetActionHandle(path, ref handle);
            if (err.TryPrint("GetActionHandle", path))
                continue;

            _hapticsHandles[(int)hand] = handle;
        }
    }

    private void InitExportFileHandles()
    {
        try
        {
            foreach (var (key, path) in Config.Instance.ExportInputs)
            {
                var splat = key.Split('.', 2);

                if (_inputActions.All(x => x.Name != splat[1]))
                {
                    Console.WriteLine($"Could not use export_inputs entry @ '{key}': No action '{splat[1]}' found.");
                    continue;
                }

                try
                {
                    var leftRight = Enum.Parse<LeftRight>(splat[0]);
                    var file = File.Open(path, FileMode.OpenOrCreate, FileAccess.Write, FileShare.Read);
                    _exportFiles[(leftRight, splat[1])] = file;
                }
                catch (Exception x)
                {
                    Console.WriteLine($"Could not use export_inputs entry @ '{key}': {x.Message}");
                }
            }
        }
        catch
        {
            Console.WriteLine("export_inputs will not be used.");
        }
    }

    private static readonly string[] Hands = { "LeftHand", "RightHand" };
    public void Update()
    {
        if (_nextDevicesUpdate < DateTime.UtcNow)
        {
            _ = Task.Run(UpdateDeviceStates);
        }

        var cErr = OpenVR.Compositor.GetLastPoses(_poses, _gamePoses);
        if (cErr.TryPrint("GetLastPoses"))
            return;

        var pose = _poses[OpenVR.k_unTrackedDeviceIndex_Hmd];

        if (pose.bPoseIsValid)
            HmdTransform = pose.mDeviceToAbsoluteTracking.ToTransform3D();

        for (var side = 0; side < 2; side++)
        {
            var controller = _controllers[side];
            if (!controller.Valid)
                continue;

            pose = _poses[controller.Index];

            if (pose.bPoseIsValid)
                PoseState[Hands[side]] = pose.mDeviceToAbsoluteTracking.ToTransform3D().RotatedLocal(Vector3.Left, Mathf.Pi * 0.3f);
        }

        var err = OpenVR.Input.UpdateActionState(_activeActionSets, _activeActionSetsSize);
        if (err.TryPrint("UpdateActionState", _actionSet!))
            return;

        foreach (var inputAction in _inputActions)
        {
            if (inputAction.Type == OpenVrInputActionType.Pose)
            {
                err = OpenVR.Input.GetPoseActionDataForNextFrame(inputAction.Handle,
                    ETrackingUniverseOrigin.TrackingUniverseStanding, ref _poseActionData, _poseActionDataSize, 0);
                if (!err.TryPrint("GetDigitalActionData", inputAction.Name))
                    if (_poseActionData.pose.bPoseIsValid)
                        PoseState[inputAction.Name] = _poseActionData.pose.mDeviceToAbsoluteTracking.ToTransform3D();
            }

            for (var s = 0; s < 2; s++)
            {
                if (_inputSourceHandles[s] == 0UL)
                    continue;

                switch (inputAction.Type)
                {
                    case OpenVrInputActionType.Boolean:
                        bool bVal;
                        err = OpenVR.Input.GetDigitalActionData(inputAction.Handle, ref _digitalActionData, _digitalActionDataSize, _inputSourceHandles[s]);
                        if (err.TryPrint("GetDigitalActionData", inputAction.Path, _inputSources[s]))
                            bVal = false;
                        else
                            bVal = _digitalActionData is { bActive: true, bState: true };

                        BooleanState[inputAction.Name][s] = bVal;
                        TryExportInput(inputAction.Name, (LeftRight)s, bVal ? "1" : "0");
                        break;

                    case OpenVrInputActionType.Single:
                    case OpenVrInputActionType.Vector1:
                    case OpenVrInputActionType.Vector2:
                    case OpenVrInputActionType.Vector3:
                        Vector3 v3Val;
                        err = OpenVR.Input.GetAnalogActionData(inputAction.Handle, ref _analogActionData, _analogActionDataSize, _inputSourceHandles[s]);
                        if (err.TryPrint("GetAnalogActionData", inputAction.Path, _inputSources[s]))
                            v3Val = Vector3.Zero;
                        else
                            v3Val = _digitalActionData.bActive ? new Vector3(_analogActionData.x, _analogActionData.y, _analogActionData.z) : Vector3.Zero;

                        Vector3State[inputAction.Name][s] = v3Val;
                        TryExportInput(inputAction.Name, (LeftRight)s, $"{v3Val.x:F6}\n{v3Val.y:F6}\n{v3Val.z:F6}");
                        break;
                }
            }
        }
    }

    private void TryExportInput(string action, LeftRight hand, string value)
    {
        if (!_exportFiles.TryGetValue((hand, action), out var file))
            return;

        file.Seek(0, SeekOrigin.Begin);
        file.Write(Encoding.UTF8.GetBytes(value));
        file.Flush();
    }

    private readonly uint[] _deviceIds = new uint[OpenVR.k_unMaxTrackedDeviceCount];
    public void UpdateDeviceStates()
    {
        DeviceStates.Clear();

        if (TryTrackedDeviceFromIndex(OpenVR.k_unTrackedDeviceIndex_Hmd, out _hmd))
        {
            _hmd.Role = TrackedDeviceRole.Hmd;
            if (_hmd.SoC >= 0)
                DeviceStates.Add(_hmd);
        }

        var numDevs = OpenVR.System.GetSortedTrackedDeviceIndicesOfClass(ETrackedDeviceClass.Controller, _deviceIds, 0);

        for (var i = 0U; i < numDevs; i++)
        {
            if (!TryTrackedDeviceFromIndex(_deviceIds[i], out var device))
                continue;

            var nativeRole = OpenVR.System.GetControllerRoleForTrackedDeviceIndex(_deviceIds[i]);
            if (nativeRole is ETrackedControllerRole.LeftHand or ETrackedControllerRole.RightHand)
            {
                var controllerIdx = nativeRole - ETrackedControllerRole.LeftHand;
                device.Role = TrackedDeviceRole.LeftHand + controllerIdx;
                _controllers[controllerIdx] = device;
                if (device.SoC >= 0)
                    DeviceStates.Add(device);
            }
        }

        numDevs = OpenVR.System.GetSortedTrackedDeviceIndicesOfClass(ETrackedDeviceClass.GenericTracker, _deviceIds, 0);

        for (var i = 0U; i < numDevs; i++)
        {
            if (!TryTrackedDeviceFromIndex(_deviceIds[i], out var device))
                continue;

            device.Role = TrackedDeviceRole.Tracker;
            if (device.SoC >= 0)
                DeviceStates.Add(device);
        }

        DeviceStates.Sort((a, b) => a.Role.CompareTo(b.Role) * 2 + a.Index.CompareTo(b.Index));

        BatteryStatesUpdated?.Invoke(this, EventArgs.Empty);
        _nextDevicesUpdate = DateTime.UtcNow + TimeSpan.FromSeconds(10);
    }

    public void InputState(LeftRight hand, ref InputState state)
    {
        var h = (int)hand;

        if (BooleanState.TryGetValue("Click", out var hands))
            state.Click = hands[h];
        if (BooleanState.TryGetValue("Grab", out hands))
            state.Grab = hands[h];
        if (BooleanState.TryGetValue("AltClick", out hands))
            state.AltClick = hands[h];
        if (BooleanState.TryGetValue("ShowHide", out hands))
            state.ShowHide = hands[h];
        if (BooleanState.TryGetValue("SpaceDrag", out hands))
            state.SpaceDrag = hands[h];
        if (BooleanState.TryGetValue("ClickModifierRight", out hands))
            state.ClickModifierRight = hands[h];
        if (BooleanState.TryGetValue("ClickModifierMiddle", out hands))
            state.ClickModifierMiddle = hands[h];
        if (Vector3State.TryGetValue("Scroll", out var handsVec))
            state.Scroll = handsVec[h].y;
    }

    public void HapticVibration(LeftRight hand, float durationSec, float amplitude, float frequency = 5f)
    {
        var controller = _controllers[(int)hand];
        if (controller.Valid)
            OpenVR.Input.TriggerHapticVibrationAction(_hapticsHandles[(int)hand], 0f, durationSec, frequency, amplitude, controller.Index);
    }

    public void Dispose()
    {
        foreach (var (_, file) in _exportFiles)
            file.Dispose();
    }

    public event EventHandler? BatteryStatesUpdated;
    public Transform3D HmdTransform { get; private set; }

    public Transform3D HandTransform(LeftRight hand) => PoseState[Hands[(uint)hand]];


    private static readonly StringBuilder PropertySb = new((int)OpenVR.k_unMaxPropertyStringSize);
    private static bool TryTrackedDeviceFromIndex(uint deviceIdx, out TrackedDevice device)
    {
        device = new TrackedDevice();
        var lastErr = ETrackedPropertyError.TrackedProp_Success;

        OpenVR.System.GetStringTrackedDeviceProperty(deviceIdx, ETrackedDeviceProperty.Prop_SerialNumber_String, PropertySb, OpenVR.k_unMaxPropertyStringSize, ref lastErr);
        if (lastErr.TryPrint("GetStringTrackedDeviceProperty(Prop_SerialNumber_String)"))
            return false;

        device.Index = deviceIdx;
        PropertySb.Clear();

        device.SoC = OpenVR.System.GetFloatTrackedDeviceProperty(deviceIdx,
            ETrackedDeviceProperty.Prop_DeviceBatteryPercentage_Float, ref lastErr);
        if (lastErr == ETrackedPropertyError.TrackedProp_Success)
        {
            device.Charging = OpenVR.System.GetBoolTrackedDeviceProperty(deviceIdx,
                ETrackedDeviceProperty.Prop_DeviceIsCharging_Bool, ref lastErr);
            if (lastErr.TryPrint("GetBoolTrackedDeviceProperty(Prop_DeviceIsCharging_Bool)"))
                device.SoC = -1;
        }
        else // no battery
            device.SoC = -1;

        device.Valid = true;

        return true;
    }
}

public class OpenVrInputAction
{
    public readonly string Path;
    public readonly string Name;
    public readonly OpenVrInputActionType Type;
    public ulong Handle;

    public OpenVrInputAction(string path, OpenVrInputActionType type)
    {
        Path = path;
        Name = path.Split('/').Last();
        Type = type;
    }

    public void Initialize()
    {
        var err = OpenVR.Input.GetActionHandle(Path, ref Handle);
        err.TryPrint("GetActionHandle", Path);
    }
}

public enum OpenVrInputActionType
{
    Boolean,
    Single,
    Vector1,
    Vector2,
    Vector3,
    Pose
}