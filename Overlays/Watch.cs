using X11Overlay.Core;
using X11Overlay.GFX;
using X11Overlay.Numerics;
using X11Overlay.Overlays.Simple;
using X11Overlay.Types;
using X11Overlay.UI;

namespace X11Overlay.Overlays;

/// <summary>
/// An overlay that shows time and has some buttons
/// </summary>
public class Watch : InteractableOverlay
{
    private static Watch? _instance;
    
    private readonly Canvas _canvas;

    private readonly Vector3 _localPosition = new(-0.05f, -0.05f, 0.15f);

    private readonly List<Control> _batteryControls = new();

    private readonly BaseOverlay _keyboard;
    private readonly BaseOverlay[] _screens;
    
    public Watch(BaseOverlay keyboard, IEnumerable<BaseOverlay> screens) : base("Watch")
    {
        if (_instance != null)
            throw new InvalidOperationException("Can't have more than one Watch!");
        _instance = this;
        
        _keyboard = keyboard;
        _screens = screens.ToArray();
        
        WidthInMeters = 0.115f;
        ShowHideBinding = false;
        ZOrder = 67;

        // 400 x 200
        _canvas = new Canvas(400, 200);

        Canvas.CurrentBgColor = HexColor.FromRgb("#353535");
        
        _canvas.AddControl(new Panel(0, 0, 400, 200));

        Canvas.CurrentFgColor = HexColor.FromRgb("#FFFFFF");
        
        Canvas.CurrentFont = new Font("LiberationSans-Bold.ttf", 46);
        _canvas.AddControl(new DateTimeLabel("HH:mm", TimeZoneInfo.Local, 19, 107, 200, 50));

        var b14Pt = Canvas.CurrentFont = new Font("LiberationSans-Bold.ttf", 14);
        _canvas.AddControl(new DateTimeLabel("d", TimeZoneInfo.Local, 20, 80, 200, 50));
        _canvas.AddControl(new DateTimeLabel("dddd", TimeZoneInfo.Local, 20, 60, 200, 50));

        Font? b24Pt = null;
        
        if (Config.Instance.AltTimezone1 != null)
        {
            
            Canvas.CurrentFgColor = HexColor.FromRgb("#99BBAA");
            var tz = TimeZoneInfo.FindSystemTimeZoneById(Config.Instance.AltTimezone1);
            var tzDisplay = Config.Instance.AltTimezone1.Split('/').Last();
            
            Canvas.CurrentFont = b14Pt;
            _canvas.AddControl(new Label(tzDisplay, 210, 137, 200, 50));
            
            b24Pt = Canvas.CurrentFont = new Font("LiberationSans-Bold.ttf", 24);
            _canvas.AddControl(new DateTimeLabel("HH:mm", tz, 210, 107, 200, 50));
        }
        
        if (Config.Instance.AltTimezone2 != null)
        {
            Canvas.CurrentFgColor = HexColor.FromRgb("#AA99BB");
            var tz = TimeZoneInfo.FindSystemTimeZoneById(Config.Instance.AltTimezone2);
            var tzDisplay = Config.Instance.AltTimezone2.Split('/').Last();

            Canvas.CurrentFont = b14Pt;
            _canvas.AddControl(new Label(tzDisplay, 210, 82, 200, 50));

            b24Pt ??= new Font("LiberationSans-Bold.ttf", 24);
            Canvas.CurrentFont = b24Pt;
            _canvas.AddControl(new DateTimeLabel("HH:mm", tz, 210, 52, 200, 50));
        }

        // Volume controls
        
        Canvas.CurrentBgColor = HexColor.FromRgb("#222222");
        Canvas.CurrentFgColor = HexColor.FromRgb("#AAAAAA");
        Canvas.CurrentFont = b14Pt;

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
        
        var numButtons = _screens.Length + 1;
        var btnWidth = 400 / numButtons;
        
        Canvas.CurrentBgColor = HexColor.FromRgb("#406050");
        Canvas.CurrentFgColor = HexColor.FromRgb("#AACCBB");

        _canvas.AddControl(new Button("Kbd", 2, 2, (uint)btnWidth - 4U, 36)
        {
            PointerDown = () => _keyboard.ToggleVisible()
        });

        Canvas.CurrentBgColor = HexColor.FromRgb("#405060");        
        Canvas.CurrentFgColor = HexColor.FromRgb("#AACCBB");
        Canvas.CurrentFgColor = HexColor.FromRgb("#AABBCC");

        for (var s = 1; s <= _screens.Length; s++)
        {
            var screen = _screens[s - 1];
            var pushedAt = DateTime.MinValue;
            _canvas.AddControl(new Button($"Scr {s}", btnWidth * s + 2, 2, (uint)btnWidth - 4U, 36)
            {
                PointerDown = () =>
                {
                    pushedAt = DateTime.UtcNow;
                    screen.ToggleVisible();
                },
                PointerUp = () =>
                {
                    if ((DateTime.UtcNow - pushedAt).TotalSeconds > 2)
                        screen.ResetPosition();
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
        
        var numStates = InputManager.BatteryStates.Count;

        if (numStates > 0)
        {
            var stateWidth = 400 / numStates;

            for (var s = 0; s < numStates; s++)
            {
                var state = InputManager.BatteryStates[s];
                var indicator = new BatteryIndicator(state, stateWidth * s + 2, 162, (uint)stateWidth - 4U, 36);
                _canvas.AddControl(indicator);
                _batteryControls.Add(indicator);
            }
        }
        _canvas.MarkDirty();
    }

    public override void Initialize()
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
        
        var controller = InputManager.PoseState["LeftHand"];
        var tgt = controller.TranslatedLocal(Vector3.Right).TranslatedLocal(_localPosition);
        Transform = controller.TranslatedLocal(_localPosition).LookingAt(tgt.origin, -controller.basis.y);

        UploadTransform();

        var toHmd = (InputManager.HmdTransform.origin - Transform.origin).Normalized();
        var alpha = MathF.Log(0.7f, Transform.basis.z.Dot(toHmd)) - 1f;
        alpha = Mathf.Clamp(alpha, 0f, 1f);
        if (alpha < float.Epsilon)
        {
            if (Visible) 
                Hide();
        }
        else
        {
            Alpha = alpha;
            if (!Visible)
                Show();
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
}