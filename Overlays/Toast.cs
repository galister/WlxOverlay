using WlxOverlay.Core;
using WlxOverlay.GFX;
using WlxOverlay.Numerics;
using WlxOverlay.Overlays.Simple;
using WlxOverlay.Types;
using WlxOverlay.UI;

namespace WlxOverlay.Overlays;

public class Toast : BaseOverlay
{
    private readonly string _title;
    private readonly string? _content;
    private readonly uint _height;
    private static int _counter;
    private static readonly FontCollection Font = FontCollection.Get(16, FontStyle.Bold);

    private const int Padding = 50;
    private Canvas? _canvas;
    private DateTime _fadeStart;
    private DateTime _fadeEnd;
    private TimeSpan _totalFadeTime;

    public Toast(string title, string? content, float opacity, uint height, float timeout) : base($"Toast{_counter++}")
    {
        ZOrder = 90;
        _title = title;
        _content = content;
        _height = height;
        ShowHideBinding = false;
        WantVisible = true;
        Alpha = opacity < float.Epsilon ? 1f : opacity;
        _fadeStart = DateTime.UtcNow.AddSeconds(timeout);
        _fadeEnd = _fadeStart.AddSeconds(Config.Instance.NotificationsFadeTime);
        _totalFadeTime = TimeSpan.FromSeconds(timeout + Config.Instance.NotificationsFadeTime);
    }

    protected override void Initialize()
    {
        var width = (uint)Font.GetTextWidth(_content ?? _title) + Padding;
        WidthInMeters = width / 2000f;

        _canvas = new Canvas(width, _height);

        Canvas.CurrentFont = Font;
        Canvas.CurrentBgColor = HexColor.FromRgb("#353535");
        Canvas.CurrentFgColor = HexColor.FromRgb("#aaaaaa");
        _canvas.AddControl(new Panel(0, 0, width, _height));

        if (_content == null)
        {
            _canvas.AddControl(new LabelCentered(_title, 0, 0, width, _height));
        }
        else
        {
            _canvas.AddControl(new LabelCentered(_content, 0, 0, width, _height - 36U));

            Canvas.CurrentBgColor = HexColor.FromRgb("#666666");
            _canvas.AddControl(new Panel(0, (int)_height - 36, width, _height));
            Canvas.CurrentFgColor = HexColor.FromRgb("#000000");
            _canvas.AddControl(new LabelCentered(_title, 0, (int)_height - 36, width, 36U));
        }
        Texture = _canvas.Initialize();

        base.Initialize();
    }

    protected internal override void AfterInput(bool batteryStateUpdated)
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

        base.AfterInput(batteryStateUpdated);
        Transform = InputManager.HmdTransform
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