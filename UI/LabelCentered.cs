using WlxOverlay.GFX;

namespace WlxOverlay.UI;

public class LabelCentered : Label
{
    private int _textWidth = -1;
    private int _nLines = -1;

    public LabelCentered(string text, int x, int y, uint w, uint h) : base(text, x, y, w, h)
    {
    }

    public override void Render()
    {
        if (Text == null)
            return;

        if (Dirty)
        {
            // TODO: Make text width dependent on wrapping too, Max it
            string[] lines = Text.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
            string longestLine = lines.OrderByDescending(line => line.Length).First();
            _textWidth = Font.GetTextWidth(longestLine);
            _nLines = lines.Length;

            Dirty = false;
        }

        var y = (int)(Y + Height / 2 - (Font.Size()) / 2);
        y += (Font.Size() / 2) * (_nLines-1);
        var startX = (int)(X + Width / 2 - _textWidth / 2);
        var curX = startX;
        var lineHeight = (int)(Font.Size() * 1.5);

        foreach (var g in Font.GetTextures(Text))
        {
            if (g == null)
            {
                curX += Font.Size() / 3;
                continue;
            }
            if (char.ConvertFromUtf32(g.Ch) == "\n")
            {
                curX = startX;
                y -= lineHeight;
                continue;
            }

            GraphicsEngine.UiRenderer.DrawFont(g, FgColor, curX, y, g.Texture.GetWidth(), g.Texture.GetHeight());

            curX += g.AdvX;
        }

    }
}