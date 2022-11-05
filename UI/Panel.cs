using X11Overlay.GFX;
using X11Overlay.Numerics;

namespace X11Overlay.UI;

/// <summary>
/// A rectangle with a background color
/// </summary>
public class Panel : Control
{
    private Vector3 _bgColor;

    public Vector3 BgColor
    {
        get => _bgColor;
        set
        {
            _bgColor = value;
            Dirty = true;
            Canvas?.MarkDirty();
        }
    }

    public Panel(int x, int y, uint w, uint h) : base(x, y, w, h)
    {
        _bgColor = Canvas.CurrentBgColor;
    }

    public override void Render()
    {
        GraphicsEngine.UiRenderer.DrawColor(BgColor, X, Y, Width, Height);
    }
}