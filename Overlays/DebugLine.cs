#if DEBUG

using WlxOverlay.Backend;
using WlxOverlay.Backend.OVR;
using WlxOverlay.Core;
using WlxOverlay.GFX;
using WlxOverlay.Numerics;

namespace WlxOverlay.Overlays;

/// <summary>
/// Use this to help visualize issues during development
/// </summary>
public class DebugLine : BaseLine
{
    private static readonly Dictionary<Vector3, DebugLine> Lines = new();

    public static void Draw(string hexColor, Vector3 start, Vector3 end)
    {
        Draw(HexColor.FromRgb(hexColor), start, end);
    }

    public static void Draw(Vector3 color, Vector3 start, Vector3 end)
    {
        if (!Lines.TryGetValue(color, out var line))
            line = Lines[color] = new DebugLine(color);

        line.SetPoints(start, end);
        if (!line.Visible)
            line.Show();
        line.Render();
    }

    private DebugLine(Vector3 color) : base($"Debug-{color}")
    {
        ZOrder = 100;
        WidthInMeters = 0.002f;
        WantVisible = true;
        Color = color;
    }

    public override void SetPoints(Vector3 start, Vector3 end, bool upload = true)
    {
        base.SetPoints(start, end, false);

        var hmd = XrBackend.Current.Input.HmdTransform;
        // billboard towards hmd
        var viewDirection = hmd.origin - start;

        var x1 = Transform.basis.z.Dot(viewDirection);
        var x2 = Transform.basis.x.Dot(viewDirection);

        var pies = (x1 - 1) * -0.5f * Mathf.Pi;
        if (x2 < 0)
            pies *= -1;

        Transform = Transform.RotatedLocal(Vector3.Up, pies);

        if (upload)
            UploadTransform();
    }
}

#endif
