using WlxOverlay.Backend;
using WlxOverlay.GFX;
using WlxOverlay.Numerics;
using WlxOverlay.Types;

namespace WlxOverlay.Core.Interactions;

public interface IPointer
{
    public static readonly Vector3[] ModeColors = {
        HexColor.FromRgb(Config.Instance.PrimaryColor ?? Config.DefaultPrimaryColor),
        HexColor.FromRgb(Config.Instance.ShiftColor ?? Config.DefaultShiftColor),
        HexColor.FromRgb(Config.Instance.AltColor ?? Config.DefaultAltColor),
        HexColor.FromRgb("#A0A0A0"),
    };
    
    public Transform3D Transform => XrBackend.Current.Input.HandTransform(Hand);
    
    public LeftRight Hand { get; }

    public void SetLength(float length);
    public void SetColor(Vector3 color);
}