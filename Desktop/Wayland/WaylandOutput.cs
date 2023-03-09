using WaylandSharp;
using WlxOverlay.Desktop.Wayland.Base;
using WlxOverlay.Numerics;

namespace WlxOverlay.Desktop.Wayland;

public class WaylandOutput : BaseOutput, IDisposable
{
    public readonly WlOutput Handle;

    public readonly uint IdName;

    public WaylandOutput(uint idName, WlOutput handle)
    {
        IdName = idName;
        Handle = handle;
    }

    internal void SetPosition(object? _, ZxdgOutputV1.LogicalPositionEventArgs e)
    {
        Position = new Vector2Int(e.X, e.Y);
    }

    internal void SetSize(object? _, ZxdgOutputV1.LogicalSizeEventArgs e)
    {
        Size = new Vector2Int(e.Width, e.Height);
    }

    internal void SetName(object? _, ZxdgOutputV1.NameEventArgs e)
    {
        Name = e.Name;
    }

    public override string ToString()
    {
        return Name;
    }

    public void Dispose()
    {
        Handle.Dispose();
    }
}
