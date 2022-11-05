using X11Overlay.Numerics;

namespace X11Overlay.UI;

public class ButtonBase : Panel
{
    protected Label _label = null!;

    private readonly Vector3 _baseBgColor;

    protected bool IsHovered;
    protected bool IsClicked;

    protected ButtonBase(int x, int y, uint w, uint h) : base(x, y, w, h)
    {
        _baseBgColor = Canvas.CurrentBgColor;
    }
    
    public override void SetCanvas(Canvas canvas)
    {
        base.SetCanvas(canvas);
        _label.SetCanvas(canvas);
    }

    public virtual void OnPointerEnter()
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