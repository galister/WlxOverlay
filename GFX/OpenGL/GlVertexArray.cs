using Silk.NET.OpenGL;

namespace WlxOverlay.GFX.OpenGL;


public class GlVertexArray<TVertexType, TIndexType> : IDisposable
    where TVertexType : unmanaged
    where TIndexType : unmanaged
{
    private readonly uint _handle;
    private readonly GL _gl;

    public GlVertexArray(GL gl, GlBuffer<TVertexType> vbo, GlBuffer<TIndexType> ebo)
    {
        _gl = gl;

        _handle = _gl.GenVertexArray();
        _gl.DebugAssertSuccess();

        Bind();
        vbo.Bind();
        ebo.Bind();
    }

    public unsafe void VertexAttributePointer(uint index, int count, VertexAttribPointerType type, uint vertexSize, int offSet)
    {
        _gl.VertexAttribPointer(index, count, type, false, vertexSize * (uint)sizeof(TVertexType), (void*)(offSet * sizeof(TVertexType)));
        _gl.DebugAssertSuccess();
        _gl.EnableVertexAttribArray(index);
        _gl.DebugAssertSuccess();
    }

    public void Bind()
    {
        _gl.BindVertexArray(_handle);
        _gl.DebugAssertSuccess();
    }

    public void Dispose()
    {
        _gl.DeleteVertexArray(_handle);
        _gl.DebugAssertSuccess();
    }
}