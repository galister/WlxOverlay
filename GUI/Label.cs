using WlxOverlay.GFX;
using WlxOverlay.Numerics;

// ReSharper disable MemberCanBeProtected.Global
// ReSharper disable PropertyCanBeMadeInitOnly.Global

namespace WlxOverlay.GUI;

/// <summary>
/// An UI element that renders text.
/// </summary>
public class Label : Control
{
    private string? _text;
    private Vector3 _fgColor;

    public string? Text
    {
        get => _text;
        set
        {
            _text = value?.ReplaceLineEndings("\n");
            Dirty = true;
            Canvas?.MarkDirty();
        }
    }

    public Vector3 FgColor
    {
        get => _fgColor;
        set
        {
            _fgColor = value;
            Dirty = true;
            Canvas?.MarkDirty();
        }
    }

    public readonly FontCollection Font;

    public Label(string? text, int x, int y, uint w, uint h) : base(x, y, w, h)
    {
        Font = Canvas.CurrentFont!;
        _fgColor = Canvas.CurrentFgColor;
        _text = text;
    }

    public override void Render()
    {
        if (_text == null)
            return;

        var lines = _text.Split('\n');

        var curY = Y + Font.LineSpacing() * (lines.Length - 1);
        foreach (var line in lines)
        {
            var curX = X;
            foreach (var g in Font.GetTextures(line))
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
