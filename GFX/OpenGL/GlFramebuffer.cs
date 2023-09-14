using Silk.NET.OpenGL;

namespace WlxOverlay.GFX.OpenGL;

public class GlFramebuffer : IDisposable
{
    private readonly uint _handle;
    private readonly GL _gl;
    private readonly uint _texture;

    public GlFramebuffer(GL gl, uint texture)
    {
        _gl = gl;
        _texture = texture;

        _handle = _gl.GenFramebuffer();
        _gl.DebugAssertSuccess();
    }

    public GlFramebuffer(GL gl, GlTexture texture)
    {
        _gl = gl;
        _texture = texture.Handle;

        _handle = _gl.GenFramebuffer();
        _gl.DebugAssertSuccess();
    }

    public void Bind()
    {
        _gl.BindFramebuffer(FramebufferTarget.Framebuffer, _handle);
        _gl.DebugAssertSuccess();

        _gl.FramebufferTexture(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0, _texture, 0);
        _gl.DebugAssertSuccess();

        _gl.DrawBuffers(1, DrawBufferMode.ColorAttachment0);
        _gl.DebugAssertSuccess();

        _gl.CheckFramebufferStatus(FramebufferTarget.Framebuffer).Assert(GLEnum.FramebufferComplete);
        _gl.DebugAssertSuccess();
    }

    public void Dispose()
    {
        _gl.DeleteFramebuffer(_handle);
        _gl.DebugAssertSuccess();
    }
}