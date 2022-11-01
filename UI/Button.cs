using X11Overlay.GFX;
using X11Overlay.Types;

namespace X11Overlay.UI;

public class Button : Panel
{
    public string Text;

    public Font Font;

    public Vector3 BgColorHover;
    public Vector3 BgColorClick;
    public Vector3 BgColorInactive;
    
    public Vector3 FgColor;
    public Vector3 FgColorInactive;

    public Button(string text, int x, int y, uint w, uint h) : base(x, y, w, h)
    {
        Text = text;
        
        Font = Canvas.CurrentFont!;

        BgColorHover = Canvas.CurrentBgColorHover;
        BgColorClick = Canvas.CurrentBgColorClick;
        BgColorInactive = Canvas.CurrentBgColorInactive;

        FgColor = Canvas.CurrentFgColor;
        FgColorInactive = Canvas.CurrentFgColorInactive;
    }
}