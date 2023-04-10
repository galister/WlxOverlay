using WaylandSharp;
using WlxOverlay.Numerics;

namespace WlxOverlay.Desktop.Wayland;

public class PipewireOutput : WaylandOutput
{
    public uint NodeId { get; set; }

    public PipewireOutput() : base(0, null) { }
    
    public override void RecalculateTransform()
    {
        Transform = new Transform2D(LogicalSize.X, 0, 0, LogicalSize.Y, Position.X, Position.Y);

        switch (WlTransform)
        {
            case WlOutputTransform._90:
            case WlOutputTransform._270:
            case WlOutputTransform.Flipped90:
            case WlOutputTransform.Flipped270:
                // some compositors return the already rotated size,
                // while others return the native size.
                if (Size.Y < Size.X)
                    Size = new Vector2Int(Size.Y, Size.X);
                break;
        } 
    }
}