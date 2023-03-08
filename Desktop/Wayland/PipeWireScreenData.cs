using WlxOverlay.Numerics;

namespace WlxOverlay.Desktop.Wayland;

public class PipeWireScreenData
{
    public string Name { get; set; }
    public uint NodeId { get; set; }
    public IntPtr Fd { get; set; }
    public Vector2Int Position { get; set; }
    public Vector2Int Size { get; set; }
}