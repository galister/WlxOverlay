using WlxOverlay.Backend;
using WlxOverlay.Backend.OVR;
using WlxOverlay.Core;
using WlxOverlay.GFX;
using WlxOverlay.GUI;
using WlxOverlay.Numerics;
using WlxOverlay.Types;

namespace WlxOverlay.Overlays;

public class Toast : BaseOverlay
{
    private readonly string _title;
    private readonly string? _content;
    private static int _counter;
    private static readonly FontCollection Font = FontCollection.Get(16, FontStyle.Bold);

    private const int Padding = 50;
    private const int PaddingY = 50 - 36;
    private Canvas? _canvas;
    private DateTime _fadeStart;
    private DateTime _fadeEnd;
    private TimeSpan _totalFadeTime;

    public Toast(string title, string? content, float opacity, float timeout) : base($"Toast{_counter++}")
    {
        ZOrder = 90;
        _title = title;
        _content = content;
        ShowHideBinding = false;
        WantVisible = true;
        Alpha = opacity < float.Epsilon ? 1f : opacity;
        _fadeStart = DateTime.UtcNow.AddSeconds(timeout);
        _fadeEnd = _fadeStart.AddSeconds(Config.Instance.NotificationsFadeTime);
        _totalFadeTime = TimeSpan.FromSeconds(timeout + Config.Instance.NotificationsFadeTime);
    }

    protected override void Initialize()
    {
        int w, h;

        if (_content == null)
        {
            (w, h) = Font.GetTextSize(_title);
        }
        else
        {
            var (w1, _) = Font.GetTextSize(_title);
            var (w2, h2) = Font.GetTextSize(_content);
            w = (int)Math.Max(w1, w2);
            h = (int)h2 + 50;
        }

        var width = (uint)(w + Padding);
        var height = (uint)h;

        WidthInMeters = width / 2000f;

        _canvas = new Canvas(width, height);

        Canvas.CurrentFont = Font;
        Canvas.CurrentBgColor = HexColor.FromRgb("#353535");
        Canvas.CurrentFgColor = HexColor.FromRgb("#aaaaaa");
        _canvas.AddControl(new Panel(0, 0, width, height));

        if (_content == null)
        {
            _canvas.AddControl(new LabelCentered(_title, 0, 0, width, height));
        }
        else
        {
            _canvas.AddControl(new Label(_content, Padding / 2, PaddingY / 2, (uint)w, height - 36U));

            Canvas.CurrentBgColor = HexColor.FromRgb("#666666");
            _canvas.AddControl(new Panel(0, h - 36, width, height));
            Canvas.CurrentFgColor = HexColor.FromRgb("#000000");
            _canvas.AddControl(new LabelCentered(_title, 0, h - 36, width, 36U));
        }
        Texture = _canvas.Initialize();

        base.Initialize();
    }

    protected internal override void AfterInput()
    {
        if (_fadeEnd < DateTime.UtcNow)
        {
            Dispose();
            return;
        }

        if (_fadeStart < DateTime.UtcNow)
        {
            Alpha = (float)(_fadeEnd - DateTime.UtcNow).TotalSeconds / Config.Instance.NotificationsFadeTime;
            UploadAlpha();
        }

        var moveProgress = (float)((_fadeEnd - DateTime.UtcNow) / _totalFadeTime);

        base.AfterInput();
        Transform = XrBackend.Current.Input.HmdTransform
            .TranslatedLocal(Vector3.Forward * (0.5f + 0.05f * moveProgress))
            .TranslatedLocal(Vector3.Down * 0.2f);

        UploadTransform();
    }

    protected internal override void Render()
    {
        _canvas!.Render();
        base.Render();
    }

    public override void Dispose()
    {
        _canvas!.Dispose();
        base.Dispose();
    }
}
