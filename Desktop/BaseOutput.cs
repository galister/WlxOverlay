using WlxOverlay.Numerics;

namespace WlxOverlay.Desktop;

public class BaseOutput
{
    public string Name { get; set; } = null!;
    public Vector2Int Position { get; set; }
    public Vector2Int Size { get; set; }
    
    public BaseOutput() { }
    public BaseOutput(string name) {
        Name = name;
    }

    public override string ToString()
    {
        return Name;
    }
}