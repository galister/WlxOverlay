using WlxOverlay.Desktop.Wayland;
using WlxOverlay.Numerics;

namespace WlxOverlay.Overlays.Wayland;

public class PipeWireScreenSingle : PipeWireScreen
{
    protected override Rect2 OutputRect { get; }

    public PipeWireScreenSingle(PipewireOutput screen) : base(screen)
    {
        OutputRect = new Rect2(0, 0, screen.Size.X, screen.Size.Y);
    }
}