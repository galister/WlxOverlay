using X11Overlay.Numerics;

namespace X11Overlay.UI;

public class ProgressBar : Panel
{
    private readonly Label _label;
    private readonly Panel _progressBar;
    private float _progress = 0.0f;

    public float Progress
    {
        get
        {
            return _progress;
        }
        set
        {
            _progress = value;
            _progressBar.Width = (uint)Mathf.Max(1, this.Width * _progress);
            Canvas?.MarkDirty();
        }
    }
    public string? LabelText
    {
        get
        {
            return _label.Text;
        }
        set
        {
            _label.Text = value;
        }
    }
    public Vector3 Color
    {
        get
        {
            return BgColor;
        }
        set
        {
            BgColor = value;
            _progressBar.BgColor = BgColor * 1.5f;
        }
    }
    public Vector3 LabelColor
    {
        get
        {
            return _label.FgColor;
        }
        set
        {
            _label.FgColor = value;
        }
    }

    public ProgressBar(float progress, string labelText, int x, int y, uint w, uint h) : base(x, y, w, h)
    {
        _progressBar = new Panel(x + 1, y + 1, w - 2, h - 2);
        _label = new LabelCentered(labelText, x, y, w, h);
        this.Progress = progress;
        this.LabelColor = Vector3.Zero;
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