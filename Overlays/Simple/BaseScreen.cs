using X11Overlay.Core;
using X11Overlay.Numerics;
using X11Overlay.Overlays.Simple;
using X11Overlay.Screen.Interop;

namespace X11Overlay.Overlays;

/// <summary>
/// An overlay that displays a screen, moves the mouse and sends mouse events.
/// </summary>
public class BaseScreen : GrabbableOverlay
{
    protected readonly int Screen;
    
    public BaseScreen(int screen) : base($"Screen{screen}")
    {
        WidthInMeters = 1;
        Screen = screen;
    }

    protected override void Initialize()
    {
        var hmd = InputManager.HmdTransform;
        var centerPoint = hmd.TranslatedLocal(SpawnPosition);

        LocalScale = new Vector3(2, -2, 2);
        CurveWhenUpright = true;

        Transform = hmd.LookingAt(centerPoint.origin, hmd.basis.y * hmd.basis.Inverse()).ScaledLocal(LocalScale);
        Transform.origin = centerPoint.origin;
        OnOrientationChanged();
        
        base.Initialize();
    }

    protected internal override void OnClickWhileHeld()
    {
        SnapUpright = !SnapUpright;
        
        base.OnClickWhileHeld();
    }

    protected internal override void OnAltClickWhileHeld()
    { 
        // TODO high quality overlays
        
        base.OnAltClickWhileHeld();
    }
}