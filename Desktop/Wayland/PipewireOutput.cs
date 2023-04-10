using WaylandSharp;
using WlxOverlay.Numerics;

namespace WlxOverlay.Desktop.Wayland;

public class PipewireOutput : WaylandOutput
{
    public uint NodeId { get; set; }

    public PipewireOutput() : base(0, null) { }
}