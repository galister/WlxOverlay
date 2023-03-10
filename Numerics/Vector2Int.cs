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

    public bool Equals(Vector2Int other)
    {
        return X == other.X && Y == other.Y;
    }

    public static bool operator ==(Vector2Int left, Vector2Int right)
    {
        return left.Equals(right);
    }

    public static bool operator !=(Vector2Int left, Vector2Int right)
    {
        return !left.Equals(right);
    }

    public static explicit operator Vector2(Vector2Int vi)
    {
        return new Vector2(vi.X, vi.Y);
    }

    public override string ToString() => $"{X}x{Y}";

    public override bool Equals(object obj)
    {
        throw new NotImplementedException();
    }
}