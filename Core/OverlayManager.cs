using System.Runtime.InteropServices;
using OVRSharp;
using Valve.VR;
using WlxOverlay.Desktop.Wayland;
using WlxOverlay.GFX;
using WlxOverlay.Numerics;
using WlxOverlay.Overlays;
using WlxOverlay.Overlays.Simple;

namespace WlxOverlay.Core;

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

    private readonly object _overlayLock = new();
    private readonly List<BaseOverlay> _overlays = new();
    private readonly List<InteractableOverlay> _interactables = new();
    private readonly List<LaserPointer> _pointers = new();

    private readonly object _lockObject = new();
    private readonly Queue<(DateTime notBefore, Action action)> _scheduledTasks = new();

    private bool _showHideState = false;

    private float _secondsSinceLastVsync;
    private ulong _frameCounter;

    public List<Func<InteractionArgs, InteractionResult>> PointerInteractions = new();
    public float DisplayFrequency;

    public OverlayManager() : base(ApplicationType.Overlay)
    {
        Instance = this;
        Console.WriteLine($"OpenVR Version: {OpenVR.System.GetRuntimeVersion()}");

        var error = EVRInitError.None;
        OpenVR.GetGenericInterface(OpenVR.IVROverlay_Version, ref error);
        if (error != EVRInitError.None)
        {
            Console.WriteLine(OpenVR.GetStringForHmdError(error));
            Environment.Exit(1);
        }
        Console.WriteLine($"{OpenVR.IVROverlay_Version}: pass");

        OpenVR.GetGenericInterface(OpenVR.IVRCompositor_Version, ref error);
        if (error != EVRInitError.None)
        {
            Console.WriteLine(OpenVR.GetStringForHmdError(error));
            Environment.Exit(1);
        }
        Console.WriteLine($"{OpenVR.IVRCompositor_Version}: pass");

        var err = new ETrackedPropertyError();
        DisplayFrequency = OpenVR.System.GetFloatTrackedDeviceProperty(OpenVR.k_unTrackedDeviceIndex_Hmd,
            ETrackedDeviceProperty.Prop_DisplayFrequency_Float, ref err);
        _frameTime = Mathf.Floor(1000f / DisplayFrequency) * 0.001f;

        InputManager.Initialize();

        Console.WriteLine($"HMD running @ {DisplayFrequency} Hz");

        _vrEventSize = (uint)Marshal.SizeOf(typeof(VREvent_t));
    }

    public void ScheduleTask(DateTime notBefore, Action action)
    {
        lock (_lockObject)
        {
            _scheduledTasks.Enqueue((notBefore, action));
        }
    }

    public void RegisterChild(BaseOverlay o)
    {
        lock (_overlayLock)
        {
            _overlays.Add(o);
            if (o is InteractableOverlay io)
                _interactables.Add(io);
            else if (o is LaserPointer lp)
                _pointers.Add(lp);
        }
    }

    public void UnregisterChild(BaseOverlay o)
    {
        lock (_overlayLock)
        {
            _overlays.Remove(o);
            if (o is InteractableOverlay io)
                _interactables.Remove(io);
            else if (o is LaserPointer lp)
                _pointers.Remove(lp);
        }
    }

    public void ShowHide()
    {
        _showHideState = !_showHideState;
        lock (_overlayLock)
        {
            foreach (var overlay in _overlays)
            {   
                if (overlay is Watch w)
                    w.Hidden = false;
                else if (overlay.ShowHideBinding)
                {
                    if (!_showHideState && overlay.Visible)
                        overlay.Hide();
                    else if (_showHideState && !overlay.Visible && overlay.WantVisible)
                        overlay.Show();
                }

            }
        }
    }

    public void SetBrightness(float f)
    {
        lock (_overlayLock)
            foreach (var o in _overlays)
                o.SetBrightness(f);
    }

    private DateTime _nextRoundTrip = DateTime.MinValue;
    private DateTime _nextDeviceUpdate = DateTime.MinValue;
    private VREvent_t _vrEvent;
    private readonly uint _vrEventSize;

    private readonly List<BaseOverlay> _workOverlays = new(MaxInteractableOverlays);

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

        lock (_lockObject)
            while (_scheduledTasks.TryPeek(out var task) && task.notBefore < DateTime.UtcNow)
            {
                _scheduledTasks.Dequeue();
                task.action();
            }

        _workOverlays.Clear();
        lock (_overlayLock)
            _workOverlays.AddRange(_overlays);

        foreach (var o in _workOverlays)
            o.AfterInput(deviceStateUpdated);

        _workOverlays.Clear();
        lock (_overlayLock)
            _workOverlays.AddRange(_interactables.Where(x => x.Visible).Reverse());

        foreach (var pointer in _pointers)
            pointer.TestInteractions(_workOverlays.Cast<InteractableOverlay>());

        _workOverlays.Clear();
        lock (_overlayLock)
            _workOverlays.AddRange(_overlays.Where(o => o is { Visible: false, WantVisible: true, ShowHideBinding: false }));

        foreach (var o in _workOverlays)
            o.Show();

        ChaperoneManager.Instance.Render();

        _workOverlays.Clear();
        lock (_overlayLock)
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

        if (_nextRoundTrip < DateTime.UtcNow)
        {
            WaylandInterface.Instance?.RoundTrip();
            _nextRoundTrip = DateTime.UtcNow.AddSeconds(1);
        }

        FontCollection.CloseHandles();
        PlaySpaceManager.Instance.EndFrame();

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

        _workOverlays.Clear();
        lock (_overlayLock)
            _workOverlays.AddRange(_overlays);

        foreach (var baseOverlay in _workOverlays)
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
