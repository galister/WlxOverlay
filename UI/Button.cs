namespace X11Overlay.UI;

/// <summary>
/// A clickable button with a label in the middle
/// </summary>
public class Button : ButtonBase
{
    public Action? PointerDown;
    public Action? PointerUp;
    
    public Button(string text, int x, int y, uint w, uint h) : base(x, y, w, h)
    {
        _label = new LabelCentered(text, x, y, w, h);
    }

    public override void OnPointerDown()
    {
        base.OnPointerDown();
        PointerDown?.Invoke();
    }

    public override void OnPointerUp()
    {
        base.OnPointerUp();
        PointerUp?.Invoke();
    }
}