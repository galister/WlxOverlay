namespace WlxOverlay.Numerics;

public struct Vector2Int
{
    public int X;
    public int Y;

    public Vector2Int()
    {
        X = 0;
        Y = 0;
    }

    public Vector2Int(int x, int y)
    {
        X = x;
        Y = y;
    }

    public override string ToString() => $"{X}x{Y}";
}