using X11Overlay.GFX;

namespace X11Overlay.UI;

public class LabelCentered : Label
{
    private int _textWidth = -1;
    
    public LabelCentered(string text, int x, int y, uint w, uint h) : base(text, x, y, w, h)
    {
    }
    
    public override void Render()
    {
        if (Text == null)
            return;
        
        if (Dirty)
        {
            _textWidth = Text.Sum(x => Font.GetTexture(x)?.AdvX ?? 0);
            Dirty = false;
        }

        var y = (int)(Y + Height / 2 - Font.Size() / 2);
        var curX = (int)(X + Width / 2 - _textWidth / 2);
        foreach (var c in Text)
        {
            var g = Font.GetTexture(c);
            if (g == null)
            {
                curX += Font.Size() / 3;
                continue;
            }

            GraphicsEngine.UiRenderer.DrawFont(g, FgColor, curX, y, g.Texture.GetWidth(), g.Texture.GetHeight());
            
            curX += g.AdvX;
        }
    }
}