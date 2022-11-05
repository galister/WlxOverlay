using Silk.NET.OpenGL;

namespace X11Overlay.GFX.OpenGL;

public class GlFramebuffer : IDisposable
{
    private readonly uint _handle;
    private readonly GL _gl;
    private readonly GlTexture _texture;

    public GlFramebuffer(GL gl, GlTexture texture)
    {
        _gl = gl;
        _texture = texture;
        
        _handle = _gl.GenFramebuffer();
        _gl.GetError().AssertNone();
    }

    public void Bind()
    {
        _gl.BindFramebuffer(FramebufferTarget.Framebuffer, _handle);
        _gl.FramebufferTexture(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0, _texture.Handle, 0);
        _gl.DrawBuffers(1, DrawBufferMode.ColorAttachment0);
        _gl.CheckFramebufferStatus(FramebufferTarget.Framebuffer).Assert(GLEnum.FramebufferComplete);
    }

    public void Dispose()
    {
        _gl.Dispose();
        _gl.DeleteFramebuffer(_handle);
    }
}