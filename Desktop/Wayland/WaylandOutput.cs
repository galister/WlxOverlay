using WaylandSharp;
using WlxOverlay.Numerics;

namespace WlxOverlay.Desktop.Wayland;

public class WaylandOutput : IDisposable
{
    public readonly WlOutput Handle;

    public readonly uint IdName;
    public string Name { get; private set; } = null!;

    public Vector2Int Size { get; private set; }
    public Vector2Int Position { get; private set; }

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
