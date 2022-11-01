using Valve.VR;
using X11Overlay.Core;
using X11Overlay.GFX;
using X11Overlay.Types;

namespace X11Overlay.Overlays;

/// <summary>
/// A small overlay that follows the one mouse cursor.
/// </summary>
public class DesktopCursor : BaseOverlay
{
    public static DesktopCursor Instance = null!;
    private bool _visibleThisFrame = false;
    
    public DesktopCursor() : base("Cursor")
    {
        if (Instance != null)
            throw new InvalidOperationException("Can't have more than one DesktopCursor!");
        
        Instance = this;
        ZOrder = 66;
        WidthInMeters = 0.02f;
        ShowHideBinding = false;
    }

    public override void Initialize()
    {
        Texture = GraphicsEngine.Instance.TextureFromFile("Resources/arrow.png");
        
        var controllerTip = InputManager.HmdTransform;
        var centerPoint = controllerTip.TranslatedLocal(Vector3.Forward);
        
        
        Transform = controllerTip.LookingAt(centerPoint.origin, Vector3.Up);
        Transform.origin = centerPoint.origin;
        
        base.Initialize();
    }

    public void MoveTo(Vector3 origin, Vector3 center)
    {
        Transform.origin = origin;
        LookAtHmd();
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