using WlxOverlay.Numerics;

namespace WlxOverlay.Desktop;

public class BaseOutput
{
    public string Name { get; set; } = null!;
    public Vector2Int Position { get; set; }
    public Vector2Int Size { get; set; }
    public Transform2D Transform { get; protected set; } = Transform2D.Identity;
    
    public BaseOutput() { }
    public BaseOutput(string name)
    {
        Name = name;
    }
    
    public virtual void RecalculateTransform()
    {
        Transform = Transform2D.Identity
            .Translated(new Vector2(Position.X, Position.Y))
            .ScaledLocal(new Vector2(Size.X, Size.Y));
    }

    public override string ToString()
    {
        return Name;
    }
}