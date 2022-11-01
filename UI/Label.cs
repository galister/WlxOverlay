using X11Overlay.GFX;
using X11Overlay.Types;

namespace X11Overlay.UI;

/// <summary>
/// An UI element that renders text.
/// </summary>
public class Label : Control
{
    public Font Font;
    public string Text;
    public Vector3 FgColor;

    public Label(string text, int x, int y, uint w, uint h) : base(x ,y ,w, h)
    {
        Font = Canvas.CurrentFont!;
        FgColor = Canvas.CurrentFgColor;
        
        Text = text;
    }

    public override void Render(ITexture canvasTex)
    {
        var curX = X;
        foreach (var c in Text)
        {
            var fontTex = Font.GetTexture(c);
            if (fontTex == null)
                continue;
            
            canvasTex.Draw(fontTex, curX, Y);
            
            curX += (int)fontTex.GetWidth();
        }
    }
}