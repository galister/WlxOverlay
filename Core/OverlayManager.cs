using OVRSharp;
using Valve.VR;
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
    
    public void Update()
    {
        InputManager.Instance.UpdateInput();
        var deviceStateUpdated = false;
        
        if (_nextDeviceUpdate < DateTime.UtcNow)
        {
            InputManager.Instance.UpdateDeviceStates();
            _nextDeviceUpdate = DateTime.UtcNow.AddSeconds(5);
            deviceStateUpdated = true;
        }
        
        foreach (var o in _overlays) 
            o.AfterInput(deviceStateUpdated);

        foreach (var pointer in _pointers)
            pointer.TestInteractions(_interactables.Where(o => o.Visible));
    }
    
    public void Render()
    {
        foreach (var o in _overlays.Where(o => !o.Visible && o.WantVisible && !o.ShowHideBinding)) 
            o.Show();

        foreach (var o in _overlays.Where(o => o.Visible)) 
            o.Render();
    }

    public void WaitForEndOfFrame()
    {
        if (OpenVR.System.GetTimeSinceLastVsync(ref _secondsSinceLastVsync, ref _frameCounter))
        {
            var wait = TimeSpan.FromSeconds(_frameTime - _secondsSinceLastVsync);
            if (wait.Ticks > 0)
                Thread.Sleep(wait);
        }
    }
}