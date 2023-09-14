using OVRSharp;
using Valve.VR;
using WlxOverlay.Numerics;
using WlxOverlay.Overlays;
using WlxOverlay.Types;

namespace WlxOverlay.Backend.OVR;

public class OVRBackend : Application, IXrBackend
{
    public float DisplayFrequency { get; }

    private readonly float _frameTime;

    private readonly OVRInput _input;
    public IXrInput Input => _input;

    private DateTime _nextDeviceUpdate = DateTime.MinValue;

    private VREvent_t _vrEvent;
    private readonly uint _vrEventSize;

    private float _secondsSinceLastVsync;
    private ulong _frameCounter;

    public OVRBackend() : base(ApplicationType.Overlay)
    {
        Console.WriteLine($"OpenVR Version: {OpenVR.System.GetRuntimeVersion()}");

        if (Config.Instance.NoAutoStart)
            OVRManifestInstaller.EnsureUninstalled();
        else
            OVRManifestInstaller.EnsureInstalled();

        var error = EVRInitError.None;
        OpenVR.GetGenericInterface(OpenVR.IVROverlay_Version, ref error);
        if (error.TryPrint())
            Environment.Exit(1);
        Console.WriteLine($"{OpenVR.IVROverlay_Version}: pass");

        OpenVR.GetGenericInterface(OpenVR.IVRCompositor_Version, ref error);
        if (error.TryPrint())
            Environment.Exit(1);
        Console.WriteLine($"{OpenVR.IVRCompositor_Version}: pass");

        var err = new ETrackedPropertyError();
        DisplayFrequency = OpenVR.System.GetFloatTrackedDeviceProperty(OpenVR.k_unTrackedDeviceIndex_Hmd,
            ETrackedDeviceProperty.Prop_DisplayFrequency_Float, ref err);
        _frameTime = Mathf.Floor(1000f / DisplayFrequency) * 0.001f;

        _input = new OVRInput();

        Console.WriteLine($"HMD running @ {DisplayFrequency} Hz");

        _vrEventSize = (uint)Marshal.SizeOf(typeof(VREvent_t));
    }

    public IList<TrackedDevice> GetBatteryStates() => _input.DeviceStates;
    public void Initialize()
    {
    }

    public LoopShould BeginFrame()
    {
        while (OVRSystem.PollNextEvent(ref _vrEvent, _vrEventSize))
        {
            switch ((EVREventType)_vrEvent.eventType)
            {
                case EVREventType.VREvent_Quit:
                    return LoopShould.Quit;

                case EVREventType.VREvent_TrackedDeviceActivated:
                case EVREventType.VREvent_TrackedDeviceDeactivated:
                case EVREventType.VREvent_TrackedDeviceUpdated:
                    _nextDeviceUpdate = DateTime.MinValue;
                    break;
            }
        }

        _input.Update();

        if (_nextDeviceUpdate < DateTime.UtcNow)
        {
            _input.UpdateDeviceStates();
            _nextDeviceUpdate = DateTime.UtcNow.AddSeconds(10);
        }

        return LoopShould.Render;
    }

    public void EndFrame(LoopShould _)
    {
        var timeToWait = TimeSpan.Zero;
        if (OpenVR.System.GetTimeSinceLastVsync(ref _secondsSinceLastVsync, ref _frameCounter))
            timeToWait = TimeSpan.FromSeconds(_frameTime - _secondsSinceLastVsync);

        if (timeToWait > TimeSpan.Zero)
            Thread.Sleep(timeToWait);
    }

    private HmdMatrix34_t matrix34_T;
    public void SetZeroPose(Vector3 offset)
    {
        if (!OpenVR.ChaperoneSetup.GetWorkingStandingZeroPoseToRawTrackingPose(ref matrix34_T))
        {
            Console.WriteLine("ERR: Failed to get Zero-Pose");
            return;
        }

        var universe = OVRExtensions.ToTransform3D(matrix34_T);
        universe.origin = offset;

        OVRExtensions.CopyTo(universe, ref matrix34_T);
        OpenVR.ChaperoneSetup.SetWorkingStandingZeroPoseToRawTrackingPose(ref matrix34_T);
        OpenVR.ChaperoneSetup.CommitWorkingCopy(EChaperoneConfigFile.Live);
    }


    private readonly string[] colorChannels =
    {
        OpenVR.k_pch_SteamVR_HmdDisplayColorGainR_Float,
        OpenVR.k_pch_SteamVR_HmdDisplayColorGainG_Float,
        OpenVR.k_pch_SteamVR_HmdDisplayColorGainB_Float
    };
    private readonly float[] channelMin = { 0.1f, 0f, 0f };

    public void AdjustGain(int ch, float amount)
    {
        var key = colorChannels[ch];
        var min = channelMin[ch];

        EVRSettingsError err = new();
        var cur = OpenVR.Settings.GetFloat(OpenVR.k_pch_SteamVR_Section, key, ref err);

        if (err != EVRSettingsError.None)
        {
            var msg = OpenVR.Settings.GetSettingsErrorNameFromEnum(err);
            Console.WriteLine($"Err: Could not get {key}: {msg}");
            return;
        }
        var val = Mathf.Clamp(cur + amount, min, 1f);
        OpenVR.Settings.SetFloat(OpenVR.k_pch_SteamVR_Section, key, val, ref err);
        if (err != EVRSettingsError.None)
        {
            var msg = OpenVR.Settings.GetSettingsErrorNameFromEnum(err);
            Console.WriteLine($"Err: Could not set {key}: {msg}");
        }
    }

    public IOverlay CreateOverlay(BaseOverlay overlay)
    {
        return new OVROverlay(overlay);
    }

    public void Destroy()
    {
        OpenVR.Shutdown();
    }
}