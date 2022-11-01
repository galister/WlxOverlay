using OVRSharp;
using Valve.VR;
using X11Overlay.Overlays;

namespace X11Overlay.Core;

public class OverlayManager : Application
{
    public static OverlayManager Instance = null!;
    public const int MaxInteractableOverlays = 16;

    public static OverlayManager Initialize()
    {
        return Instance = new OverlayManager();
    }
    
    private readonly List<BaseOverlay> _overlays = new();
    private readonly List<InteractableOverlay> _interactables = new();
    private readonly List<LaserPointer> _pointers = new();

    private bool _showHideState = false;
    
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
            overlay.WantVisible = _showHideState;
            if (!_showHideState && overlay.Visible)
                overlay.Hide();
        }
    }
    
    public void Update()
    {
        InputManager.Instance.UpdateInput();
        
        foreach (var o in _overlays) 
            o.AfterInput();

        foreach (var pointer in _pointers)
            pointer.TestInteractions(_interactables);
    }
    
    public void Render()
    {
        foreach (var o in _overlays.Where(o => o.WantVisible && !o.Visible))
            o.Show();

        foreach (var o in _overlays.Where(o => o.Visible)) 
            o.Render();
    }
}