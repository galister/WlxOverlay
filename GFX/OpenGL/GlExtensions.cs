using Silk.NET.OpenGL;

namespace WlxOverlay.GFX.OpenGL;

public static class GlExtensions
{
    [Conditional("DEBUG")]
    public static void DebugAssertSuccess(this GL gl)
    {
        var error = gl.GetError();
        if (error != GLEnum.None)
            Console.WriteLine($"[Err] {error}");
    }

    public static void Assert(this GLEnum error, GLEnum expected)
    {
        if (error != expected)
            throw new ApplicationException($"[Err] Expected {expected}, but got {error}");
    }

}