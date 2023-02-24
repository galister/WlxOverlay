using Silk.NET.OpenGL;

namespace WlxOverlay.GFX.OpenGL;

public static class GlExtensions
{
    public static void AssertNone(this GLEnum error)
    {
        if (error != GLEnum.None)
            throw new ApplicationException($"[Err] {error}");
    }

    public static void Assert(this GLEnum error, GLEnum expected)
    {
        if (error != expected)
            throw new ApplicationException($"[Err] Expected {expected}, but got {error}");
    }

}