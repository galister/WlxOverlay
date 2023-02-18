using WlxOverlay.Core;
using WlxOverlay.GFX;
using WlxOverlay.Numerics;

namespace WlxOverlay.UI;

public class BatteryIndicator : ProgressBar
{
    private static readonly Vector3 BgColorCharging = HexColor.FromRgb("#204070");
    private static readonly Vector3 BgColorDischarging = HexColor.FromRgb("#406040");
    private static readonly Vector3 BgColorCritical = HexColor.FromRgb("#604040");

    public BatteryIndicator(TrackedDevice status, int x, int y, uint w, uint h) : base(status.SoC, LabelString(status), x, y, w, h)
    {
        if (status.Charging)
            BgColor = BgColorCharging;
        else if (status.SoC < 0.15)
            BgColor = BgColorCritical;
        else
            BgColor = BgColorDischarging;
    }

    private static string LabelString(TrackedDevice status)
    {
        var s = status.Role.ToString()[..1];
        var percent = Mathf.Clamp((int)(status.SoC * 100f), 0, 99);
        return s + percent;
    }
}