using System.Runtime.InteropServices;
using Newtonsoft.Json;
using Valve.VR;
using X11Overlay.Types;

namespace X11Overlay.Core;

public class InputManager
{
    internal static InputManager Instance = null!;
    
    private string? _actionSet;
    private ulong _actionSetHandle;
    
    public static readonly Dictionary<string, bool[]> BooleanState = new();
    public static readonly Dictionary<string, Vector2[]> Vector2State = new();
    public static readonly Dictionary<string, Transform3D> PoseState = new();

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
        
        OpenVR.GetGenericInterface(OpenVR.IVRCompositor_Version, ref error);
        if (error != EVRInitError.None)
        {
            Console.WriteLine(OpenVR.GetStringForHmdError(error));
            Environment.Exit(1);
        }
        Console.WriteLine("IVRCompositor: pass");
        
        Instance.LoadActionSets();
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

            if (inputAction.Type == OpenVrInputActionType.Boolean)
                BooleanState[inputAction.Name] = new[] { false, false };

            if (inputAction.Type == OpenVrInputActionType.Vector2)
                Vector2State[inputAction.Name] = new[] { Vector2.Zero, Vector2.Zero };
            
            if (inputAction.Type == OpenVrInputActionType.Pose)
                PoseState[inputAction.Name] = Transform3D.Identity;
            
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

                        err = OpenVR.Input.GetDigitalActionData(inputAction.Handle, ref _digitalActionData, _digitalActionDataSize, _inputSourceHandles[s]);
                        if (err != EVRInputError.None)
                        {
                            Console.WriteLine($"GetDigitalActionData {inputAction.Path} on {_inputSources[s]}: {err}");
                            BooleanState[inputAction.Name][s] = false;
                        }
                        else
                            BooleanState[inputAction.Name][s] = _digitalActionData.bActive && _digitalActionData.bState;
                        break;
                    
                    case OpenVrInputActionType.Vector2:
                        err = OpenVR.Input.GetAnalogActionData(inputAction.Handle, ref _analogActionData, _analogActionDataSize, _inputSourceHandles[s]);
                        if (err != EVRInputError.None)
                        {
                            Console.WriteLine($"GetAnalogActionData {inputAction.Path} on {_inputSources[s]}: {err}");
                            Vector2State[inputAction.Name][s] = Vector2.Zero;
                        }
                        else
                            Vector2State[inputAction.Name][s] = _digitalActionData.bActive ? new Vector2(_analogActionData.x, _analogActionData.y) : Vector2.Zero;
                        break;
                }
            }
        }
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

public enum OpenVrInputActionType
{
    Boolean,
    Vector2,
    Pose
}