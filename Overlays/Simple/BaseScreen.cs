using X11Overlay.Core;
using X11Overlay.Numerics;

namespace X11Overlay.Overlays.Simple;

/// <summary>
/// An overlay that displays a screen, moves the mouse and sends mouse events.
/// </summary>
public class BaseScreen<T> : GrabbableOverlay
{
    protected readonly T Screen;

    public BaseScreen(T screen) : base($"Screen_{screen}")
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