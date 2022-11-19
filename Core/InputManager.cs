using System.Runtime.InteropServices;
using System.Text;
using Newtonsoft.Json;
using Valve.VR;
using X11Overlay.Numerics;
using X11Overlay.Overlays;
using X11Overlay.Types;

namespace X11Overlay.Core;

public class InputManager : IDisposable
{
    internal static InputManager Instance = null!;
    
    private string? _actionSet;
    private ulong _actionSetHandle;
    
    public static readonly Dictionary<string, bool[]> BooleanState = new();
    public static readonly Dictionary<string, Vector3[]> Vector3State = new();
    public static readonly Dictionary<string, Transform3D> PoseState = new();
    public static readonly List<BatteryStatus> BatteryStates = new();

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
        Console.WriteLine("IVRInput: pass");

        Instance.LoadActionSets();
        Instance.InitExportFileHandles();
    }

    private void LoadActionSets()
    {
        var actionsPath = Path.Combine(Directory.GetCurrentDirectory(), "actions.json");

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

    public void UpdateInput()
    {
        var cErr = OpenVR.Compositor.GetLastPoses(_poses, _gamePoses);
        if (cErr != EVRCompositorError.None)
        {
            Console.WriteLine($"GetLastPoses: {cErr}");
            return;
        }
        if (_poses[OpenVR.k_unTrackedDeviceIndex_Hmd].bPoseIsValid)
            HmdTransform = _poses[OpenVR.k_unTrackedDeviceIndex_Hmd].mDeviceToAbsoluteTracking.ToTransform3D();


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
                            bVal = _digitalActionData.bActive && _digitalActionData.bState;

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
    public void UpdateBatteryStates()
    {
        var numDevs = OpenVR.System.GetSortedTrackedDeviceIndicesOfClass(ETrackedDeviceClass.Controller, _deviceIds, 0);
        var lastErr = ETrackedPropertyError.TrackedProp_Success;
        
        BatteryStates.Clear();
            
        for (var i = 0U; i < numDevs; i++)
        {
            var bs = new BatteryStatus();
            
            bs.SoC = OpenVR.System.GetFloatTrackedDeviceProperty(_deviceIds[i],
                ETrackedDeviceProperty.Prop_DeviceBatteryPercentage_Float, ref lastErr);
            if (TryPrintError(lastErr, "GetFloatTrackedDeviceProperty(Prop_DeviceBatteryPercentage_Float)"))
                continue;
            
            bs.Charging = OpenVR.System.GetBoolTrackedDeviceProperty(_deviceIds[i],
                ETrackedDeviceProperty.Prop_DeviceIsCharging_Bool, ref lastErr);
            if (TryPrintError(lastErr, "GetBoolTrackedDeviceProperty(Prop_DeviceIsCharging_Bool)"))
                continue;

            bs.Role = OpenVR.System.GetControllerRoleForTrackedDeviceIndex(_deviceIds[i]);
            BatteryStates.Add(bs);
        }
        
        numDevs = OpenVR.System.GetSortedTrackedDeviceIndicesOfClass(ETrackedDeviceClass.GenericTracker, _deviceIds, 0);
        
        for (var i = 0U; i < numDevs; i++)
        {
            var bs = new BatteryStatus();
            
            bs.SoC = OpenVR.System.GetFloatTrackedDeviceProperty(_deviceIds[i],
                ETrackedDeviceProperty.Prop_DeviceBatteryPercentage_Float, ref lastErr);
            if (TryPrintError(lastErr, "GetFloatTrackedDeviceProperty(Prop_DeviceBatteryPercentage_Float)"))
                continue;
            
            bs.Charging = OpenVR.System.GetBoolTrackedDeviceProperty(_deviceIds[i],
                ETrackedDeviceProperty.Prop_DeviceIsCharging_Bool, ref lastErr);
            if (TryPrintError(lastErr, "GetBoolTrackedDeviceProperty(Prop_DeviceIsCharging_Bool)"))
                continue;

            bs.Role = ETrackedControllerRole.OptOut;

            BatteryStates.Add(bs);
        }
        
        BatteryStates.Sort((a, b) => a.Role.CompareTo(b.Role));
    }

    private bool TryPrintError(ETrackedPropertyError err, string message)
    {
        if (err != ETrackedPropertyError.TrackedProp_Success)
        {
            Console.WriteLine("[Err] " + err + " while " + message);
            return true;
        }
        return false;
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

public struct BatteryStatus
{
    public float SoC;
    public bool Charging;
    public ETrackedControllerRole Role;
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