using WaylandSharp;
using WlxOverlay.Numerics;

namespace WlxOverlay.Desktop.Wayland;

public class PipewireOutput : WaylandOutput
{
    public uint NodeId { get; set; }

    public PipewireOutput() : base(0, null) { }
    
    public override void RecalculateTransform()
    {
        if (LogicalSize.Y > LogicalSize.X && Size.X > Size.Y)
            Size = new Vector2Int(Size.Y, Size.X);
        
        Transform = new Transform2D(LogicalSize.X, 0, 0, LogicalSize.Y, Position.X, Position.Y);
    }
}