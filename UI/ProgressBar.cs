using X11Overlay.Numerics;

namespace X11Overlay.UI;

public class ProgressBar : Panel
{
    private readonly Label _label;
    private readonly Panel _progressBar;

    public ProgressBar(float progress, string labelText, int x, int y, uint w, uint h) : base(x, y, w, h)
    {
        _progressBar = new Panel(x + 1, y + 1, (uint)Mathf.Max(1, w * progress), h - 2) { BgColor = BgColor * 2 };
        _label = new LabelCentered(labelText, x, y, w, h) { FgColor = Vector3.Zero };
    }

    public override void SetCanvas(Canvas canvas)
    {
        base.SetCanvas(canvas);
        _progressBar.SetCanvas(canvas);
        _label.SetCanvas(canvas);
    }

    public override void Render()
    {
        base.Render();
        _progressBar.Render();
        _label.Render();
    }
}