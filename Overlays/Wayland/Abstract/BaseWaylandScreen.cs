using WlxOverlay.Desktop;
using WlxOverlay.Desktop.Wayland;
using WlxOverlay.Desktop.Wayland.Base;
using WlxOverlay.GFX;
using WlxOverlay.Overlays.Simple;

namespace WlxOverlay.Overlays.Wayland.Abstract;

/// <summary>
/// Base of all Wayland screens.
/// </summary>
public abstract class BaseWaylandScreen<T> : BaseScreen<T> where T: BaseOutput
{
    protected BaseWaylandScreen(T output) : base(output)
    {
    }

    protected override void Initialize()
    {
        base.Initialize();

        Texture = GraphicsEngine.Instance.EmptyTexture((uint)Screen.Size.X, (uint)Screen.Size.Y, internalFormat: GraphicsFormat.RGB8, dynamic: true);

        UpdateInteractionTransform();
        UploadCurvature();
    }

    protected override bool MoveMouse(PointerHit hitData)
    {
        if (UInp == null)
            return false;

        var uv = hitData.uv;
        var posX = uv.x * Screen.Size.X + Screen.Position.X;
        var posY = uv.y * Screen.Size.Y + Screen.Position.Y;
        var rectSize = WaylandInterface.Instance!.OutputRect.Size;

        var mulX = UInput.Extent / rectSize.x;
        var mulY = UInput.Extent / rectSize.y;

        UInp.MouseMove((int)(posX * mulX), (int)(posY * mulY));
        return true;
    }

    public override string ToString()
    {
        return Screen.Name;
    }

    public override void Dispose()
    {
        Texture?.Dispose();
        base.Dispose();
    }
}
