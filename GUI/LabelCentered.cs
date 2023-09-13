using WlxOverlay.GFX;

namespace WlxOverlay.GUI;

public class LabelCentered : Label
{
    private List<int> _textSizes = new();

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
            _textSizes.AddRange(lines.Select(l => Font.GetTextSize(l).w));
            Dirty = false;
        }

        var totalTextHeight = Font.Size() + Font.LineSpacing() * (lines.Length - 1);

        var curY = (int)(Y + Height / 2 - totalTextHeight / 2);

        for (var i = 0; i < lines.Length; i++)
        {
            var curX = (int)(X + Width / 2 - _textSizes[i] / 2);

            foreach (var g in Font.GetTextures(lines[i]))
            {
                if (g == null)
                {
                    curX += Font.Size() / 3;
                    continue;
                }

                GraphicsEngine.Renderer.DrawFont(g, FgColor, curX, curY, g.Texture.GetWidth(), g.Texture.GetHeight());

                curX += g.AdvX;
            }
            curY -= Font.LineSpacing();
        }
    }
}
