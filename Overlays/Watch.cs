using WlxOverlay.Backend;
using WlxOverlay.Core;
using WlxOverlay.Core.Interactions;
using WlxOverlay.GFX;
using WlxOverlay.GUI;
using WlxOverlay.Numerics;
using WlxOverlay.Types;

namespace WlxOverlay.Overlays;

/// <summary>
/// An overlay that shows time and has some buttons
/// </summary>
public class Watch : BaseOverlay, IInteractable
{
    private static Watch? _instance;
    private readonly Canvas _canvas;
    private readonly List<Control> _batteryControls = new();

    private float _flBrightness = 1f;

    internal LeftRight Hand;
    internal Vector3 Vec3RelToHand = new(-0.05f, -0.05f, 0.15f);
    internal Vector3 Vec3InsideUnit = Vector3.Right;

    public bool Hidden;

    private bool _batteryStateUpdated;

    public Watch(BaseOverlay keyboard) : base("Watch")
    {
        if (_instance != null)
            throw new InvalidOperationException("Can't have more than one Watch!");
        _instance = this;

        Hand = Config.Instance.WatchHand;
        if (Config.Instance.WatchHand == LeftRight.Right)
        {
            Vec3RelToHand.x *= -1;
            Vec3InsideUnit.x *= -1;
        }

        XrBackend.Current.Input.BatteryStatesUpdated += (_, _) 
            => _batteryStateUpdated = true;

        WidthInMeters = 0.115f;
        ShowHideBinding = false;
        ZOrder = 67;

        // 400 x 200
        _canvas = new Canvas(400, 200);

        Canvas.CurrentBgColor = HexColor.FromRgb("#353535");

        _canvas.AddControl(new Panel(0, 0, 400, 200));

        Canvas.CurrentFgColor = HexColor.FromRgb("#FFFFFF");

        var timeFormat = Config.Instance.Use12hTime ? "hh:mm" : "HH:mm";

        Canvas.CurrentFont = FontCollection.Get(46, FontStyle.Bold);
        _canvas.AddControl(new DateTimeLabel(timeFormat, TimeZoneInfo.Local, 19, 107, 200, 50));

        Canvas.CurrentFont = FontCollection.Get(14, FontStyle.Bold);
        _canvas.AddControl(new DateTimeLabel("d", TimeZoneInfo.Local, 20, 80, 200, 50));
        _canvas.AddControl(new DateTimeLabel("dddd", TimeZoneInfo.Local, 20, 60, 200, 50));
        if (Config.Instance.Use12hTime)
          _canvas.AddControl(new DateTimeLabel("tt", TimeZoneInfo.Local, 175, 107, 200, 50));

        if (Config.Instance.AltTimezone1 != null)
        {

            Canvas.CurrentFgColor = HexColor.FromRgb("#99BBAA");
            var tz = TimeZoneInfo.FindSystemTimeZoneById(Config.Instance.AltTimezone1);
            var tzDisplay = Config.Instance.AltTimezone1.Split('/').Last();

            Canvas.CurrentFont = FontCollection.Get(14, FontStyle.Bold);
            _canvas.AddControl(new Label(tzDisplay, 210, 137, 200, 50));
            if (Config.Instance.Use12hTime) {
              Canvas.CurrentFont = FontCollection.Get(9, FontStyle.Bold);
              _canvas.AddControl(new DateTimeLabel("tt", tz, 294, 107, 200, 50));
            }

            Canvas.CurrentFont = FontCollection.Get(24, FontStyle.Bold);
            _canvas.AddControl(new DateTimeLabel(timeFormat, tz, 210, 107, 200, 50));
        }

        if (Config.Instance.AltTimezone2 != null)
        {
            Canvas.CurrentFgColor = HexColor.FromRgb("#AA99BB");
            var tz = TimeZoneInfo.FindSystemTimeZoneById(Config.Instance.AltTimezone2);
            var tzDisplay = Config.Instance.AltTimezone2.Split('/').Last();

            Canvas.CurrentFont = FontCollection.Get(14, FontStyle.Bold);
            _canvas.AddControl(new Label(tzDisplay, 210, 82, 200, 50));
            if (Config.Instance.Use12hTime) {
              Canvas.CurrentFont = FontCollection.Get(9, FontStyle.Bold);
              _canvas.AddControl(new DateTimeLabel("tt", tz, 294, 52, 200, 50));
            }

            Canvas.CurrentFont = FontCollection.Get(24, FontStyle.Bold);
            _canvas.AddControl(new DateTimeLabel(timeFormat, tz, 210, 52, 200, 50));
        }

        // Volume controls

        Canvas.CurrentBgColor = HexColor.FromRgb("#222222");
        Canvas.CurrentFgColor = HexColor.FromRgb("#AAAAAA");
        Canvas.CurrentFont = FontCollection.Get(14, FontStyle.Bold);

        _canvas.AddControl(new Panel(325, 114, 50, 36));
        _canvas.AddControl(new Panel(325, 50, 50, 36));
        _canvas.AddControl(new Label("Vol", 334, 94, 50, 30));

        Canvas.CurrentBgColor = HexColor.FromRgb("#505050");

        var psiUp = Runner.StartInfoFromArgs(Config.Instance.VolumeUpCmd);
        if (psiUp != null)
            _canvas.AddControl(new Button("+", 327, 116, 46, 32)
            {
                PointerDown = _ => Runner.TryStart(psiUp)
            });

        var psiDn = Runner.StartInfoFromArgs(Config.Instance.VolumeDnCmd);
        if (psiDn != null)
            _canvas.AddControl(new Button("-", 327, 52, 46, 32)
            {
                PointerDown = _ => Runner.TryStart(psiDn)
            });

        // Bottom row
        Canvas.CurrentBgColor = HexColor.FromRgb("#406050");
        Canvas.CurrentFgColor = HexColor.FromRgb("#CCBBAA");

        int bottomRowStart;

        _canvas.AddControl(new Button("☰", 2, 2, 36, 36)
        {
            PointerDown = _ =>
            {
                Hidden = true;
                Hide();
                var advSettings = new AdvancedSettings(this);
                OverlayRegistry.Register(advSettings);
                advSettings.Show();
            }
        });
        bottomRowStart = 40;

        var screens = OverlayRegistry.ListOverlays().Where(x => x is DesktopOverlay).ToList();

        var numButtons = screens.Count + 1;
        var btnWidth = (400 - bottomRowStart) / numButtons;

        Canvas.CurrentBgColor = HexColor.FromRgb("#406050");
        Canvas.CurrentFgColor = HexColor.FromRgb("#AACCBB");

        var kbPushedAt = DateTime.MinValue;
        _canvas.AddControl(new Button("Kbd", bottomRowStart + 2, 2, (uint)btnWidth - 4U, 36)
        {
            PointerDown = _ =>
            {
                kbPushedAt = DateTime.UtcNow;
            },
            PointerUp = _ =>
            {
                if ((DateTime.UtcNow - kbPushedAt).TotalSeconds > 2)
                    keyboard.ResetTransform();
                else
                    keyboard.ToggleVisible();
            }
        });

        Canvas.CurrentBgColor = HexColor.FromRgb("#405060");
        Canvas.CurrentFgColor = HexColor.FromRgb("#AACCBB");
        Canvas.CurrentFgColor = HexColor.FromRgb("#AABBCC");

        for (var s = 1; s <= screens.Count; s++)
        {
            var screen = screens[s - 1];
            var screenName = screen.ToString() ?? "UNK";

            var pushedAt = DateTime.MinValue;
            _canvas.AddControl(new Button(screenName, btnWidth * s + bottomRowStart + 2, 2, (uint)btnWidth - 4U, 36)
            {
                PointerDown = _ =>
                {
                    pushedAt = DateTime.UtcNow;
                },
                PointerUp = _ =>
                {
                    if ((DateTime.UtcNow - pushedAt).TotalSeconds > 2)
                        screen.ResetTransform();
                    else
                        screen.ToggleVisible();
                }
            });
        }

        _canvas.BuildInteractiveLayer();
    }

    public void SwapHands()
    {
        Hand = LeftRight.Left == Hand ? LeftRight.Right : LeftRight.Left;
        Vec3RelToHand.x *= -1;
        Vec3InsideUnit.x *= -1;
    }

    private void OnBatteryStatesUpdated()
    {
        foreach (var c in _batteryControls)
            _canvas.RemoveControl(c);
        _batteryControls.Clear();

        var states = XrBackend.Current.GetBatteryStates();
        
        if (states.Count > 0)
        {
            var stateWidth = 400 / states.Count;

            for (var s = 0; s < states.Count; s++)
            {
                var device = states[s];

                var indicator = new BatteryIndicator(device, stateWidth * s + 2, 162, (uint)stateWidth - 4U, 36);
                _canvas.AddControl(indicator);
                _batteryControls.Add(indicator);
            }
        }
        _canvas.MarkDirty();
    }

    protected override void Initialize()
    {
        Texture = _canvas.Initialize();
        base.Initialize();
    }

    protected internal override void Render()
    {
        _canvas.Render();

        base.Render();
    }

    protected internal override void AfterInput()
    {
        base.AfterInput();

        var controller = XrBackend.Current.Input.HandTransform(Hand);
        var tgt = controller.TranslatedLocal(Vec3InsideUnit).TranslatedLocal(Vec3RelToHand);
        Transform = controller.TranslatedLocal(Vec3RelToHand).LookingAt(tgt.origin, -controller.basis.y);

        UploadTransform();

        var toHmd = (XrBackend.Current.Input.HmdTransform.origin - Transform.origin).Normalized();
        var unclampedAlpha = MathF.Log(0.7f, Transform.basis.z.Dot(toHmd)) - 1f;
        Alpha = Mathf.Clamp(unclampedAlpha, 0f, 1f);
        if (Alpha < 0.1)
        {
            if (Visible)
                Hide();
        }
        else
        {
            if (!Visible){
                Show();
            }
            UploadAlpha();
        }

        if (_batteryStateUpdated)
        {
            OnBatteryStatesUpdated();
            _batteryStateUpdated = false;
        }
    }

    public override void Show()
    {
        if (!Hidden)
            base.Show();
    }

    public void OnPointerDown(PointerHit hitData)
    {
        var action = _canvas.OnPointerDown(hitData.uv, hitData.pointer.Hand);
        hitData.pointer.AddReleaseAction(action);
    }

    public void OnPointerUp(PointerHit hitData)
    {
    }

    public void OnPointerHover(PointerHit hitData)
    {
        _canvas.OnPointerHover(hitData.uv, hitData.pointer.Hand);
    }

    public void OnPointerLeft(LeftRight hand)
    {
        _canvas.OnPointerLeft(hand);
    }

    public void OnScroll(PointerHit hitData, float value)
    {
        var lastColorMultiplier = _flBrightness;
        _flBrightness = Mathf.Clamp(_flBrightness + Mathf.Pow(value, 3) * 0.25f, 0.1f, 1f);
        if (Math.Abs(lastColorMultiplier - _flBrightness) > float.Epsilon)
            OverlayRegistry.Execute(x => x.SetBrightness(_flBrightness));
    }
}
