using WlxOverlay.Numerics;

namespace WlxOverlay.Desktop.Wayland.Base;

public class BaseOutput
{
    public string Name { get; set; } = null!;
    public Vector2Int Position { get; set; }
    public Vector2Int Size { get; set; }

    public override string ToString()
    {
        return Name;
    }
}