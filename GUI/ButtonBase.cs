using WlxOverlay.Core.Interactions;
using WlxOverlay.Numerics;

namespace WlxOverlay.GUI;

public class ButtonBase : Panel
{
    protected Label _label = null!;

    private Vector3 _baseBgColor;

    protected bool IsHovered;
    protected bool IsClicked;

    protected ButtonBase(int x, int y, uint w, uint h) : base(x, y, w, h)
    {
        _baseBgColor = Canvas.CurrentBgColor;
    }

    public void SetBgColor(Vector3 color)
    {
        _baseBgColor = color;
        Canvas?.MarkDirty();
    }

    public void SetText(string text)
    {
        _label.Text = text;
    }

    public override void SetCanvas(Canvas canvas)
    {
        base.SetCanvas(canvas);
        _label.SetCanvas(canvas);
    }

    public virtual void OnPointerEnter(LeftRight hand)
    {
        IsHovered = true;
        Canvas?.MarkDirty();
    }

    public virtual void OnPointerExit()
    {
        IsHovered = false;
        Canvas?.MarkDirty();
    }

    public virtual void OnPointerDown()
    {
        IsClicked = true;
        Canvas?.MarkDirty();
    }

    public virtual void OnPointerUp()
    {
        IsClicked = false;
        Canvas?.MarkDirty();
    }

    public override void Render()
    {
        BgColor = IsClicked
            ? _baseBgColor * 2f
            : IsHovered
                ? _baseBgColor * 1.5f
                : _baseBgColor;

        base.Render();
        _label.Render();
    }
}
