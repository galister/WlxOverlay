using X11Overlay.GFX;
using X11Overlay.Types;

namespace X11Overlay.UI;

/// <summary>
/// A rectangle with a background color
/// </summary>
public class Panel : Control
{
    public Vector3 BgColor;

    public Panel(int x, int y, uint w, uint h) : base(x, y, w, h)
    {
        BgColor = Canvas.CurrentBgColor;
    }

    public override void Render(ITexture canvasTex)
    {
        canvasTex.Clear(BgColor, X, Y, Width, Height);
    }
}