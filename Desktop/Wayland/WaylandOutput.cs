using WaylandSharp;
using WlxOverlay.Numerics;

namespace WlxOverlay.Desktop.Wayland;

public class WaylandOutput : BaseOutput, IDisposable
{
    public WlOutput? Handle;

    public uint IdName;

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

    public virtual void Dispose()
    {
        Handle?.Dispose();
    }
}
