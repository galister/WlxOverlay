using System.Drawing;
using WlxOverlay.Numerics;

namespace WlxOverlay.GFX;

public class HexColor
{
    public static Vector3 FromRgb(string str)
    {
        var c = ColorTranslator.FromHtml(str);
        return new Vector3(c.R, c.G, c.B) / byte.MaxValue;
    }
    
    private const float DIV12 = 1f / 12.92f;
    private const float DIV1 = 1f / 1.055f;
    private const float THRESHOLD = 0.04045f;
    public static Vector3 FromSrgb(string str)
    {
        var c = ColorTranslator.FromHtml(str);
        return new Vector3(
            c.R < THRESHOLD ? c.R * DIV12 : Mathf.Pow((c.R + 0.055f) * DIV1, 2.4f),
            c.G < THRESHOLD ? c.G * DIV12 : Mathf.Pow((c.G + 0.055f) * DIV1, 2.4f),
            c.B < THRESHOLD ? c.B * DIV12 : Mathf.Pow((c.B + 0.055f) * DIV1, 2.4f)
            ) / byte.MaxValue;
    }
}