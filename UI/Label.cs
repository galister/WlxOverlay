using WlxOverlay.GFX;
using WlxOverlay.Numerics;

// ReSharper disable MemberCanBeProtected.Global
// ReSharper disable PropertyCanBeMadeInitOnly.Global

namespace WlxOverlay.UI;

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
            _text = value;
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

    public readonly Font Font;

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

        var curX = X;
        foreach (var c in _text)
        {
            var g = Font.GetTexture(c);
            if (g == null)
                continue;

            GraphicsEngine.UiRenderer.DrawFont(g, FgColor, curX, Y, g.Texture.GetWidth(), g.Texture.GetHeight());

            curX += g.AdvX;
        }
    }
}