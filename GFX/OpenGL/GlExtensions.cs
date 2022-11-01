using Silk.NET.OpenGL;

namespace X11Overlay.GFX.OpenGL;

public static class GlExtensions
{
    public static void Assert(this GLEnum actual, GLEnum expected)
    {
        if (actual != expected)
            throw new ApplicationException($"Expected {expected} but got {actual}");
    }
}