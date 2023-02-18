using X11Overlay.Core;
using X11Overlay.GFX;
using X11Overlay.Numerics;
using X11Overlay.Overlays.Simple;

namespace X11Overlay.Overlays;

/// <summary>
/// A small overlay that follows the one mouse cursor.
/// </summary>
public class DesktopCursor : BaseOverlay
{
    public static DesktopCursor Instance = null!;
    private bool _visibleThisFrame;

    public DesktopCursor() : base("Cursor")
    {
        if (Instance != null)
            throw new InvalidOperationException("Can't have more than one DesktopCursor!");

        Instance = this;
        ZOrder = 66;
        WidthInMeters = 0.01f;
        ShowHideBinding = false;
    }

    protected override void Initialize()
    {
        Texture = GraphicsEngine.Instance.TextureFromFile("Resources/arrow.png");

        var controllerTip = InputManager.HmdTransform;
        var centerPoint = controllerTip.TranslatedLocal(Vector3.Forward);

        Transform = controllerTip.LookingAt(centerPoint.origin, Vector3.Up);
        Transform.origin = centerPoint.origin;

        base.Initialize();
    }

    public void MoveTo(Transform3D moveToTransform)
    {
        Transform = moveToTransform;
        _visibleThisFrame = true;

        if (!Visible)
        {
            Show();
        }
        UploadTransform();
    }

    protected internal override void Render()
    {
        if (!_visibleThisFrame)
        {
            Hide();
        }
        _visibleThisFrame = false;
    }
}