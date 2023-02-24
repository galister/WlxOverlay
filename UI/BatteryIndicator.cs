using WlxOverlay.Core;
using WlxOverlay.GFX;
using WlxOverlay.Numerics;

namespace WlxOverlay.UI;

public class BatteryIndicator : Panel
{
    private static readonly Vector3 BgColorCharging = HexColor.FromRgb("#204070");
    private static readonly Vector3 BgColorDischarging = HexColor.FromRgb("#406040");
    private static readonly Vector3 BgColorCritical = HexColor.FromRgb("#604040");

    private readonly Label _label;
    private readonly Panel _bg2;

    public BatteryIndicator(TrackedDevice status, int x, int y, uint w, uint h) : base(x, y, w, h)
    {
        if (status.Charging)
            BgColor = BgColorCharging;
        else if (status.SoC < 0.15)
            BgColor = BgColorCritical;
        else
            BgColor = BgColorDischarging;

        _bg2 = new Panel(x + 1, y + 1, (uint)Mathf.Max(1, w * status.SoC), h - 2) { BgColor = BgColor * 2 };
        _label = new LabelCentered(LabelString(status), x, y, w, h) { FgColor = Vector3.Zero };
    }

    public override void SetCanvas(Canvas canvas)
    {
        base.SetCanvas(canvas);
        _bg2.SetCanvas(canvas);
        _label.SetCanvas(canvas);
    }

    private static string LabelString(TrackedDevice status)
    {
        var s = status.Role.ToString()[..1];

        var percent = Mathf.Clamp((int)(status.SoC * 100f), 0, 99);
        return s + percent;
    }

    public override void Render()
    {
        base.Render();
        _bg2.Render();
        _label.Render();
    }
}