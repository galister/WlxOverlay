using WaylandSharp;
using WlxOverlay.Desktop;
using WlxOverlay.Desktop.Wayland;
using WlxOverlay.GFX;
using WlxOverlay.Overlays.Simple;

namespace WlxOverlay.Overlays.Wayland;

public abstract class BaseWaylandScreen : BaseScreen<WaylandOutput>
{
    protected readonly WlDisplay Display;
    protected WlOutput? Output;

    protected BaseWaylandScreen(WaylandOutput output) : base(output)
    {
        Display = WlDisplay.Connect();

        var reg = Display.GetRegistry();

        reg.Global += (_, e) =>
        {
            if (e.Interface == WlInterface.WlOutput.Name)
            {
                if (e.Name == Screen.IdName)
                    Output = reg.Bind<WlOutput>(e.Name, e.Interface, e.Version);
            }
            else OnGlobal(reg, e);
        };

        reg.GlobalRemove += (_, e) =>
        {
            if (e.Name == Screen.IdName)
                Dispose();
        };

        Display.Roundtrip();
    }

    protected abstract void OnGlobal(WlRegistry reg, WlRegistry.GlobalEventArgs e);

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
