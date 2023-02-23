using System.Runtime.InteropServices;
using OVRSharp;
using Valve.VR;
using X11Overlay.Desktop.Wayland;
using X11Overlay.GFX;
using X11Overlay.Numerics;
using X11Overlay.Overlays;
using X11Overlay.Overlays.Simple;

namespace X11Overlay.Core;

public class OverlayManager : Application
{
    public static OverlayManager Instance = null!;
    public const int MaxInteractableOverlays = 16;

    public static OverlayManager Initialize()
    {
        return Instance = new OverlayManager();
    }

    private bool _running = true;
    private readonly float _frameTime;

    private readonly List<BaseOverlay> _overlays = new();
    private readonly List<InteractableOverlay> _interactables = new();
    private readonly List<LaserPointer> _pointers = new();
    private bool _showHideState = false;

    private float _secondsSinceLastVsync;
    private ulong _frameCounter;

    public OverlayManager() : base(ApplicationType.Overlay)
    {
        Instance = this;

        var error = EVRInitError.None;
        OpenVR.GetGenericInterface(OpenVR.IVROverlay_Version, ref error);
        if (error != EVRInitError.None)
        {
            Console.WriteLine(OpenVR.GetStringForHmdError(error));
            Environment.Exit(1);
        }
        Console.WriteLine("IVROverlay: pass");

        OpenVR.GetGenericInterface(OpenVR.IVRCompositor_Version, ref error);
        if (error != EVRInitError.None)
        {
            Console.WriteLine(OpenVR.GetStringForHmdError(error));
            Environment.Exit(1);
        }
        Console.WriteLine("IVRCompositor: pass");

        var err = new ETrackedPropertyError();
        var displayFrequency = OpenVR.System.GetFloatTrackedDeviceProperty(OpenVR.k_unTrackedDeviceIndex_Hmd,
            ETrackedDeviceProperty.Prop_DisplayFrequency_Float, ref err);
        _frameTime = Mathf.Floor(1000f / displayFrequency) * 0.001f;

        Console.WriteLine($"HMD running @ {displayFrequency} Hz");

        InputManager.Initialize();

        _vrEventSize = (uint)Marshal.SizeOf(typeof(VREvent_t));
    }

    public void RegisterChild(BaseOverlay o)
    {
        _overlays.Add(o);
        if (o is InteractableOverlay io)
            _interactables.Add(io);
        else if (o is LaserPointer lp)
            _pointers.Add(lp);
    }

    public void UnregisterChild(BaseOverlay o)
    {
        _overlays.Remove(o);
        if (o is InteractableOverlay io)
            _interactables.Remove(io);
        else if (o is LaserPointer lp)
            _pointers.Remove(lp);
    }

    public void ShowHide()
    {
        _showHideState = !_showHideState;
        foreach (var overlay in _overlays.Where(x => x.ShowHideBinding))
        {
            if (!_showHideState && overlay.Visible)
                overlay.Hide();
            else if (_showHideState && !overlay.Visible && overlay.WantVisible)
                overlay.Show();

        }
    }

    public void SetBrightness(float f)
    {
        foreach (var o in _overlays)
            o.SetBrightness(f);
    }

    private DateTime _nextDeviceUpdate = DateTime.MinValue;
    private VREvent_t _vrEvent;
    private readonly uint _vrEventSize;

    private List<BaseOverlay> _workOverlays = new(MaxInteractableOverlays);

    public void Update()
    {
        if (!_running)
        {
            Destroy();
            return;
        }

        InputManager.Instance.UpdateInput();
        var deviceStateUpdated = false;

        if (_nextDeviceUpdate < DateTime.UtcNow)
        {
            InputManager.Instance.UpdateDeviceStates();
            _nextDeviceUpdate = DateTime.UtcNow.AddSeconds(10);
            deviceStateUpdated = true;
        }

        foreach (var o in _overlays)
            o.AfterInput(deviceStateUpdated);

        _workOverlays.Clear();
        _workOverlays.AddRange(_interactables.Where(x => x.Visible));
        foreach (var pointer in _pointers)
            pointer.TestInteractions(_workOverlays.Cast<InteractableOverlay>());

        _workOverlays.Clear();
        _workOverlays.AddRange(_overlays.Where(o => o is { Visible: false, WantVisible: true, ShowHideBinding: false }));
        foreach (var o in _workOverlays)
            o.Show();

        _workOverlays.Clear();
        _workOverlays.AddRange(_overlays.Where(o => o.Visible));
        foreach (var o in _workOverlays)
            o.Render();

        while (OVRSystem.PollNextEvent(ref _vrEvent, _vrEventSize))
        {
            switch ((EVREventType)_vrEvent.eventType)
            {
                case EVREventType.VREvent_Quit:
                    Destroy();
                    return;

                case EVREventType.VREvent_TrackedDeviceActivated:
                case EVREventType.VREvent_TrackedDeviceDeactivated:
                case EVREventType.VREvent_TrackedDeviceUpdated:
                    _nextDeviceUpdate = DateTime.MinValue;
                    break;
            }
        }

        WaylandInterface.Instance?.RoundTrip();

        // Use this instead of vsync to prevent glfw from using up the entire CPU core
        WaitForEndOfFrame();
    }

    public void Stop()
    {
        _running = false;
    }

    private void Destroy()
    {
        Console.WriteLine("Shutting down.");

        foreach (var baseOverlay in _overlays)
            baseOverlay.Dispose();

        OpenVR.Shutdown();
        GraphicsEngine.Instance.Shutdown();
    }

    private void WaitForEndOfFrame()
    {
        var timeToWait = TimeSpan.Zero;
        if (OpenVR.System.GetTimeSinceLastVsync(ref _secondsSinceLastVsync, ref _frameCounter))
            timeToWait = TimeSpan.FromSeconds(_frameTime - _secondsSinceLastVsync);

        if (timeToWait > TimeSpan.Zero)
            Thread.Sleep(timeToWait);
    }
}
