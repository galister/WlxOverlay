namespace WlxOverlay.UI;

/// <summary>
/// A clickable button with a label in the middle
/// </summary>
public class Button : ButtonBase
{
    public Action<Button>? PointerDown;
    public Action<Button>? PointerUp;

    public Button(string text, int x, int y, uint w, uint h) : base(x, y, w, h)
    {
        _label = new LabelCentered(text, x, y, w, h);
    }

    public override void OnPointerDown()
    {
        base.OnPointerDown();
        PointerDown?.Invoke(this);
    }

    public override void OnPointerUp()
    {
        base.OnPointerUp();
        PointerUp?.Invoke(this);
    }
}
