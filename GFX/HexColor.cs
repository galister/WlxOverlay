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
}