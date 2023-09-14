using WlxOverlay.Numerics;

namespace WlxOverlay.Desktop;

public class BaseOutput
{
    public uint IdName;

    public static Rect2 OutputRect { get; private set; }

    public string Name { get; set; } = null!;
    public Vector2Int Position { get; set; }
    public Vector2Int Size { get; set; }
    public Transform2D Transform { get; protected set; } = Transform2D.Identity;

    protected BaseOutput() { }
    public BaseOutput(int x11Screen)
    {
        IdName = (uint)x11Screen;
        Name = $"Scr {x11Screen}";
    }

    public virtual void RecalculateTransform()
    {
        Transform = new Transform2D(Size.X, 0, 0, Size.Y, Position.X, Position.Y);
        MergeOutputRect();
    }

    protected void MergeOutputRect()
    {
        var origin = Transform * Vector2.Zero;
        var size = Transform * Vector2.One - origin;
        if (size.x < 0)
        {
            origin.x += size.x;
            size.x = -size.x;
        }

        if (size.y < 0)
        {
            origin.y += size.y;
            size.y = -size.y;
        }

        var rect = new Rect2(origin, size);
        OutputRect = OutputRect.Merge(rect);
    }

    public override string ToString()
    {
        return Name;
    }
}