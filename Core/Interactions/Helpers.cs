using WlxOverlay.Numerics;

namespace WlxOverlay.Core.Interactions;

public class InteractionArgs
{
    public Transform3D HandTransform;
    public PointerMode Mode;
    public LeftRight Hand;
    public InputState Now;
    public InputState Before;
}

public struct InteractionResult
{
    public bool Handled;
    public float Length;
    public Vector3 Color;

    public static readonly InteractionResult Unhandled = new() { Handled = false };
    public static readonly InteractionResult OK = new() { Handled = true };
}

public class PointerHit
{
    public readonly IPointer pointer;
    public PointerMode modifier;
    public bool isPrimary;
    public float distance;
    public Vector2 uv;
    public Vector3 point;
    public Vector3 normal;

    public PointerHit(IPointer p)
    {
        pointer = p;
    }

    public override string ToString()
    {
        return $"{pointer.Hand} at {uv} ({point})";
    }
}

public enum PointerMode : uint
{
    Left,
    Right,
    Middle,
}

public enum LeftRight : uint
{
    Left = 0U,
    Right = 1U
}
