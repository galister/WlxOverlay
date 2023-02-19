using WlxOverlay.Core;
using WlxOverlay.GFX;
using WlxOverlay.Numerics;
using WlxOverlay.Overlays.Simple;
using WlxOverlay.Types;
using WlxOverlay.UI;

namespace WlxOverlay.Overlays;

/// <summary>
/// An overlay that shows time and has some buttons
/// </summary>
public class Watch : InteractableOverlay
{
    private static Watch? _instance;
    private readonly Canvas _canvas;
    private readonly List<Control> _batteryControls = new();

    private float _flBrightness = 1f;

    private readonly string _strPose;
    private readonly Vector3 _vec3RelToHand = new(-0.05f, -0.05f, 0.15f);
    private readonly Vector3 _vec3InsideUnit = Vector3.Right;

    private readonly ProgressBar memUsageBar;
    private readonly ProgressBar powerUsageBar;

    public Watch(BaseOverlay keyboard, IList<BaseOverlay> screens) : base("Watch")
    {
        if (_instance != null)
            throw new InvalidOperationException("Can't have more than one Watch!");
        _instance = this;

        _strPose = $"{Config.Instance.WatchHand}Hand";
        if (Config.Instance.WatchHand == LeftRight.Right)
        {
            _vec3RelToHand.x *= -1;
            _vec3InsideUnit.x *= -1;
        }

        WidthInMeters = 0.115f;
        ShowHideBinding = false;
        ZOrder = 67;

        // 400 x 200
        _canvas = new Canvas(400, 284);
        powerUsageBar = new ProgressBar(0.0f, "Power Usage", 2, 244, 400 - 2 * 2, 40);
        memUsageBar = new ProgressBar(0.0f, "Memory Usage", 2, 202, 400 - 2 * 2, 40);
        _canvas.AddControl(memUsageBar);
        _canvas.AddControl(powerUsageBar);

        Canvas.CurrentBgColor = HexColor.FromRgb("#353535");

        _canvas.AddControl(new Panel(0, 0, 400, 200));

        Canvas.CurrentFgColor = HexColor.FromRgb("#FFFFFF");

        Canvas.CurrentFont = FontCollection.Get(46, FontStyle.Bold);
        _canvas.AddControl(new DateTimeLabel("HH:mm", TimeZoneInfo.Local, 19, 107, 200, 50));

        Canvas.CurrentFont = FontCollection.Get(14, FontStyle.Bold);
        _canvas.AddControl(new DateTimeLabel("d", TimeZoneInfo.Local, 20, 80, 200, 50));
        _canvas.AddControl(new DateTimeLabel("dddd", TimeZoneInfo.Local, 20, 60, 200, 50));

        if (Config.Instance.AltTimezone1 != null)
        {

            Canvas.CurrentFgColor = HexColor.FromRgb("#99BBAA");
            var tz = TimeZoneInfo.FindSystemTimeZoneById(Config.Instance.AltTimezone1);
            var tzDisplay = Config.Instance.AltTimezone1.Split('/').Last();

            Canvas.CurrentFont = FontCollection.Get(14, FontStyle.Bold);
            _canvas.AddControl(new Label(tzDisplay, 210, 137, 200, 50));

            Canvas.CurrentFont = FontCollection.Get(24, FontStyle.Bold);
            _canvas.AddControl(new DateTimeLabel("HH:mm", tz, 210, 107, 200, 50));
        }

        if (Config.Instance.AltTimezone2 != null)
        {
            Canvas.CurrentFgColor = HexColor.FromRgb("#AA99BB");
            var tz = TimeZoneInfo.FindSystemTimeZoneById(Config.Instance.AltTimezone2);
            var tzDisplay = Config.Instance.AltTimezone2.Split('/').Last();

            Canvas.CurrentFont = FontCollection.Get(14, FontStyle.Bold);
            _canvas.AddControl(new Label(tzDisplay, 210, 82, 200, 50));

            Canvas.CurrentFont = FontCollection.Get(24, FontStyle.Bold);
            _canvas.AddControl(new DateTimeLabel("HH:mm", tz, 210, 52, 200, 50));
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
                PointerDown = () => Runner.TryStart(psiUp)
            });

        var psiDn = Runner.StartInfoFromArgs(Config.Instance.VolumeDnCmd);
        if (psiDn != null)
            _canvas.AddControl(new Button("-", 327, 52, 46, 32)
            {
                PointerDown = () => Runner.TryStart(psiDn)
            });

        // Bottom row
        Canvas.CurrentBgColor = HexColor.FromRgb("#406050");
        Canvas.CurrentFgColor = HexColor.FromRgb("#AACCBB");

        int bottomRowStart = 0;
        
        if (Config.Instance.ExperimentalFeatures)
        {
            _canvas.AddControl(new Button("C", 2, 2, 36, 36)
            {
                PointerDown = () =>
                {
                    WantVisible = false;
                    Hide();
                    var chaperoneSettings = new ChaperoneSettings(this);
                    OverlayManager.Instance.RegisterChild(chaperoneSettings);
                    chaperoneSettings.Show();
                }
            });
            bottomRowStart = 40;
        }

        var numButtons = screens.Count + 1;
        var btnWidth = (400-bottomRowStart) / numButtons;

        Canvas.CurrentBgColor = HexColor.FromRgb("#406050");
        Canvas.CurrentFgColor = HexColor.FromRgb("#AACCBB");

        var kbPushedAt = DateTime.MinValue;
        _canvas.AddControl(new Button("Kbd", bottomRowStart + 2, 2, (uint)btnWidth - 4U, 36)
        {
            PointerDown = () =>
            {
                kbPushedAt = DateTime.UtcNow;
            },
            PointerUp = () =>
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
                PointerDown = () =>
                {
                    pushedAt = DateTime.UtcNow;
                },
                PointerUp = () =>
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

    private void OnBatteryStatesUpdated()
    {
        foreach (var c in _batteryControls)
            _canvas.RemoveControl(c);
        _batteryControls.Clear();

        var numStates = InputManager.DeviceStatesSorted.Count;

        if (numStates > 0)
        {
            var stateWidth = 400 / numStates;

            for (var s = 0; s < numStates; s++)
            {
                var device = InputManager.DeviceStatesSorted[s];
                if (device.Role == TrackedDeviceRole.None || device.SoC < 0)
                    continue;

                var indicator = new BatteryIndicator(device, stateWidth * s + 2, 162, (uint)stateWidth - 4U, 36);
                _canvas.AddControl(indicator);
                _batteryControls.Add(indicator);
            }
        }
        _canvas.MarkDirty();
    }

    // Material colors, taken from https://m2.material.io/resources/color/
    private static readonly Vector3 BgColorGreen = HexColor.FromRgb("#33691e");
    private static readonly Vector3 BgColorYellow = HexColor.FromRgb("#f57f17");
    private static readonly Vector3 BgColorRed = HexColor.FromRgb("#880e4f");
    private static readonly Vector3 BgColorBlue = HexColor.FromRgb("#0d47a1");

    public static Vector3 ColorizeFraction(float val) => val switch
    {
        (> 0.0f) and (<= 0.5f) => BgColorGreen,
        (> 0.5f) and (<= 0.8f) => BgColorYellow,
        (> 0.8f) and (<= 1.0f) => BgColorRed,
        _ => BgColorBlue,
    };

    public void OnGpuStatsUpdated(float memUsage, float memTotal, float power, float powerLimit)
    {
        lock (_canvas)
        {
            {
                float powerPercent = power / powerLimit;
                powerUsageBar.Progress = powerPercent;
                powerUsageBar.LabelText = $"{power}W";
                powerUsageBar.Color = ColorizeFraction(powerPercent);
            }
            {
                float memPercent = memUsage / memTotal;
                memUsageBar.Progress = memPercent;
                memUsageBar.LabelText = $"{memUsage}MiB/{memTotal}MiB";
                memUsageBar.Color = ColorizeFraction(memPercent);
            }
        }
    }

    protected override void Initialize()
    {
        Texture = _canvas.Initialize();

        UpdateInteractionTransform();
        base.Initialize();
    }

    protected internal override void Render()
    {
        _canvas.Render();

        base.Render();
    }

    protected internal override void AfterInput(bool batteryStateUpdated)
    {
        base.AfterInput(batteryStateUpdated);

        var controller = InputManager.PoseState[_strPose];
        var tgt = controller.TranslatedLocal(_vec3InsideUnit).TranslatedLocal(_vec3RelToHand);
        Transform = controller.TranslatedLocal(_vec3RelToHand).LookingAt(tgt.origin, -controller.basis.y);

        UploadTransform();

        var toHmd = (InputManager.HmdTransform.origin - Transform.origin).Normalized();
        var unclampedAlpha = MathF.Log(0.7f, Transform.basis.z.Dot(toHmd)) - 1f;
        Alpha = Mathf.Clamp(unclampedAlpha, 0f, 1f);
        if (Alpha < float.Epsilon)
        {
            if (Visible)
                Hide();
        }
        else
        {
            if (!Visible)
                Show();
            else
                UploadAlpha();
        }

        if (batteryStateUpdated)
            OnBatteryStatesUpdated();
    }

    protected internal override void OnPointerDown(PointerHit hitData)
    {
        base.OnPointerDown(hitData);
        var action = _canvas.OnPointerDown(hitData.uv, hitData.hand);
        hitData.pointer.ReleaseAction = action;
    }

    protected internal override void OnPointerHover(PointerHit hitData)
    {
        base.OnPointerHover(hitData);
        _canvas.OnPointerHover(hitData.uv, hitData.hand);
    }

    protected internal override void OnPointerLeft(LeftRight hand)
    {
        base.OnPointerLeft(hand);
        _canvas.OnPointerLeft(hand);
    }

    protected internal override void OnScroll(PointerHit hitData, float value)
    {
        base.OnScroll(hitData, value);

        var lastColorMultiplier = _flBrightness;
        _flBrightness = Mathf.Clamp(_flBrightness + Mathf.Pow(value, 3) * 0.25f, 0.1f, 1f);
        if (Math.Abs(lastColorMultiplier - _flBrightness) > float.Epsilon)
            OverlayManager.Instance.SetBrightness(_flBrightness);
    }
}
