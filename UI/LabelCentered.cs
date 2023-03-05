using WlxOverlay.GFX;

namespace WlxOverlay.UI;

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
            _textWidth = Font.GetTextWidth(Text);
            Dirty = false;
        }

        var y = (int)(Y + Height / 2 - Font.Size() / 2);
        var curX = (int)(X + Width / 2 - _textWidth / 2);
        
        foreach (var g in Font.GetTextures(Text))
        {
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