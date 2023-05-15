using WlxOverlay.GFX;

namespace WlxOverlay.UI;

public class LabelCentered : Label
{
    private List<(int w, int h)> _textSizes = new();

    public LabelCentered(string text, int x, int y, uint w, uint h) : base(text, x, y, w, h)
    {
    }

    public override void Render()
    {
        if (Text == null)
            return;

        var lines = Text.Split('\n');

        if (Dirty)
        {
            _textSizes.Clear();
            _textSizes.AddRange(lines.Select(l => Font.GetTextSize(l)));
            Dirty = false;
        }

        var linesHeight = Font.Size() + Font.LineSpacing() * (lines.Length - 1);
        var curY = (int)(Y + Height / 2 - linesHeight / 2);

        for (var i = 0; i < lines.Length; i++) 
        {
            var curX = (int)(X + Width / 2 - _textSizes[i].w / 2);

            foreach (var g in Font.GetTextures(lines[i]))
            {
                if (g == null)
                {
                    curX += Font.Size() / 3;
                    continue;
                }

                GraphicsEngine.UiRenderer.DrawFont(g, FgColor, curX, curY, g.Texture.GetWidth(), g.Texture.GetHeight());

                curX += g.AdvX;
            }
            curY += Font.LineSpacing();
        }
    }
}
