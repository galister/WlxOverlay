using System.Runtime.InteropServices;
using System.Text;
using Newtonsoft.Json;
using Valve.VR;
using WlxOverlay.Numerics;
using WlxOverlay.Overlays;
using WlxOverlay.Types;

namespace WlxOverlay.Core;

public class InputManager : IDisposable
{
    internal static InputManager Instance = null!;

    private string? _actionSet;
    private ulong _actionSetHandle;

    public static readonly Dictionary<string, bool[]> BooleanState = new();
    public static readonly Dictionary<string, Vector3[]> Vector3State = new();
    public static readonly Dictionary<string, Transform3D> PoseState = new();
    public static readonly List<TrackedDevice> DeviceStates = new();

    public static Transform3D HmdTransform;

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

    private VRActiveActionSet_t[]? _activeActionSets;
    private readonly uint _activeActionSetsSize;

    private readonly Dictionary<(LeftRight hand, string action), FileStream> _exportFiles = new();

    private readonly TrackedDevice[] _controllers = new TrackedDevice[2];
    private TrackedDevice _hmd;

    private InputManager()
    {
        _digitalActionDataSize = (uint)Marshal.SizeOf(typeof(InputDigitalActionData_t));
        _analogActionDataSize = (uint)Marshal.SizeOf(typeof(InputAnalogActionData_t));
        _poseActionDataSize = (uint)Marshal.SizeOf(typeof(InputPoseActionData_t));

        _activeActionSetsSize = (uint)Marshal.SizeOf(typeof(VRActiveActionSet_t));
    }

    public static void Initialize()
    {
        if (Instance != null)
            throw new InvalidOperationException("Can't have more than one InputManager!");
        Instance = new InputManager();

        var error = EVRInitError.None;
        OpenVR.GetGenericInterface(OpenVR.IVRInput_Version, ref error);
        if (error != EVRInitError.None)
        {
            Console.WriteLine(OpenVR.GetStringForHmdError(error));
            Environment.Exit(1);
        }
        Console.WriteLine($"{OpenVR.IVRInput_Version}: pass");

        Instance.LoadActionSets();
        Instance.InitExportFileHandles();
    }

    private void LoadActionSets()
    {
        if (!Config.TryGetFile("actions.json", out var actionsPath, true))
            return;

        var err = OpenVR.Input.SetActionManifestPath(actionsPath);
        if (err != EVRInputError.None)
        {
            Console.WriteLine($"SetActionManifestPath: {err}");
            Environment.Exit(1);
        }

        var actionsJson = JsonConvert.DeserializeObject<Actions>(File.ReadAllText(actionsPath));
        _actionSet = actionsJson?.action_sets?.FirstOrDefault()?.name;
        if (_actionSet == null)
        {
            Console.WriteLine("Could not load action.json");
            Environment.Exit(1);
        }

        var stringToActionType = Enum.GetValues<OpenVrInputActionType>()
            .ToDictionary(x => x.ToString().ToLowerInvariant());
        foreach (var action in actionsJson!.actions!)
        {
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
        if (err != EVRInputError.None)
        {
            Console.WriteLine($"GetActionSetHandle {_actionSet}: {err}");
            Environment.Exit(1);
        }

        _activeActionSets = new VRActiveActionSet_t[]
        {
            new()
            {
                ulActionSet = _actionSetHandle,
                ulRestrictedToDevice = OpenVR.k_ulInvalidInputValueHandle,
            }
        };

        GetInputSourceHandles();
    }

    private void GetInputSourceHandles()
    {
        for (var i = 0; i < _inputSources.Length; i++)
        {
            var path = _inputSources[i];
            var handle = 0UL;
            var err = OpenVR.Input.GetInputSourceHandle(path, ref handle);
            if (err != EVRInputError.None)
                Console.WriteLine($"GetInputSources {path}: {err}");

            _inputSourceHandles[i] = handle;
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
    public void UpdateInput()
    {
        var cErr = OpenVR.Compositor.GetLastPoses(_poses, _gamePoses);
        if (cErr != EVRCompositorError.None)
        {
            Console.WriteLine($"GetLastPoses: {cErr}");
            return;
        }

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

        EVRInputError err;
        err = OpenVR.Input.UpdateActionState(_activeActionSets, _activeActionSetsSize);
        if (err != EVRInputError.None)
        {
            Console.WriteLine($"UpdateActionState {_actionSet}: {err}");
            return;
        }

        foreach (var inputAction in _inputActions)
        {
            if (inputAction.Type == OpenVrInputActionType.Pose)
            {
                err = OpenVR.Input.GetPoseActionDataForNextFrame(inputAction.Handle,
                    ETrackingUniverseOrigin.TrackingUniverseStanding, ref _poseActionData, _poseActionDataSize, 0);
                if (err != EVRInputError.None)
                    Console.WriteLine($"GetDigitalActionData {inputAction.Name}: {err}");
                else
                {
                    if (_poseActionData.pose.bPoseIsValid)
                        PoseState[inputAction.Name] = _poseActionData.pose.mDeviceToAbsoluteTracking.ToTransform3D();
                }
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
                        if (err != EVRInputError.None)
                        {
                            Console.WriteLine($"GetDigitalActionData {inputAction.Path} on {_inputSources[s]}: {err}");
                            bVal = false;
                        }
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
                        if (err != EVRInputError.None)
                        {
                            Console.WriteLine($"GetAnalogActionData {inputAction.Path} on {_inputSources[s]}: {err}");
                            v3Val = Vector3.Zero;
                        }
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

        if (TrackedDevice.TryCreateFromIndex(OpenVR.k_unTrackedDeviceIndex_Hmd, out _hmd))
        {
            _hmd.Role = TrackedDeviceRole.Hmd;
            DeviceStates.Add(_hmd);
        }

        var numDevs = OpenVR.System.GetSortedTrackedDeviceIndicesOfClass(ETrackedDeviceClass.Controller, _deviceIds, 0);

        for (var i = 0U; i < numDevs; i++)
        {
            if (!TrackedDevice.TryCreateFromIndex(_deviceIds[i], out var device))
                continue;

            var nativeRole = OpenVR.System.GetControllerRoleForTrackedDeviceIndex(_deviceIds[i]);
            if (nativeRole is ETrackedControllerRole.LeftHand or ETrackedControllerRole.RightHand)
            {
                var controllerIdx = nativeRole - ETrackedControllerRole.LeftHand;
                device.Role = TrackedDeviceRole.LeftHand + controllerIdx;
                _controllers[controllerIdx] = device;
                DeviceStates.Add(device);
            }
        }

        numDevs = OpenVR.System.GetSortedTrackedDeviceIndicesOfClass(ETrackedDeviceClass.GenericTracker, _deviceIds, 0);

        for (var i = 0U; i < numDevs; i++)
        {
            if (!TrackedDevice.TryCreateFromIndex(_deviceIds[i], out var device))
                continue;

            device.Role = TrackedDeviceRole.Tracker;
            DeviceStates.Add(device);
        }

        DeviceStates.Sort((a,b) => a.Role.CompareTo(b.Role) * 2 + a.Index.CompareTo(b.Index));
        GC.Collect();
    }

    public void Dispose()
    {
        foreach (var (_, file) in _exportFiles)
            file.Dispose();
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
        if (err != EVRInputError.None)
            Console.WriteLine($"GetActionHandle {Path}: {err}");
    }
}

public struct TrackedDevice
{
    public bool Valid;
    public uint Index;
    public float SoC;
    public bool Charging;
    public TrackedDeviceRole Role;

    private static readonly StringBuilder Sb = new((int)OpenVR.k_unMaxPropertyStringSize);
    public static bool TryCreateFromIndex(uint deviceIdx, out TrackedDevice device)
    {
        device = new TrackedDevice();
        var lastErr = ETrackedPropertyError.TrackedProp_Success;

        OpenVR.System.GetStringTrackedDeviceProperty(deviceIdx, ETrackedDeviceProperty.Prop_SerialNumber_String, Sb, OpenVR.k_unMaxPropertyStringSize, ref lastErr);
        if (TryPrintError(lastErr, "GetStringTrackedDeviceProperty(Prop_SerialNumber_String)"))
            return false;

        device.Index = deviceIdx;
        Sb.Clear();

        device.SoC = OpenVR.System.GetFloatTrackedDeviceProperty(deviceIdx,
            ETrackedDeviceProperty.Prop_DeviceBatteryPercentage_Float, ref lastErr);
        if (lastErr == ETrackedPropertyError.TrackedProp_Success)
        {
            device.Charging = OpenVR.System.GetBoolTrackedDeviceProperty(deviceIdx,
                ETrackedDeviceProperty.Prop_DeviceIsCharging_Bool, ref lastErr);
            if (TryPrintError(lastErr, "GetBoolTrackedDeviceProperty(Prop_DeviceIsCharging_Bool)"))
                device.SoC = -1;
        }
        else // no battery
            device.SoC = -1;

        device.Valid = true;
        
        return true;
    }

    private static bool TryPrintError(ETrackedPropertyError err, string message)
    {
        if (err != ETrackedPropertyError.TrackedProp_Success)
        {
            Console.WriteLine("[Err] " + err + " while " + message);
            return true;
        }
        return false;
    }
}

public enum TrackedDeviceRole
{
    None,
    Hmd,
    LeftHand,
    RightHand,
    Tracker
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