using Silk.NET.OpenGL;

namespace WlxOverlay.GFX.OpenGL;


public class GlBuffer<TDataType> : IDisposable
    where TDataType : unmanaged
{
    private uint _handle;
    private BufferTargetARB _bufferType;
    private GL _gl;

    public unsafe GlBuffer(GL gl, Span<TDataType> data, BufferTargetARB bufferType)
    {
        _gl = gl;
        _bufferType = bufferType;

        _handle = _gl.GenBuffer();
        _gl.DebugAssertSuccess();
        
        Bind();
        fixed (void* d = data)
        {
            _gl.BufferData(bufferType, (nuint)(data.Length * sizeof(TDataType)), d, BufferUsageARB.StaticDraw);
        }
        _gl.DebugAssertSuccess();
        
        Unbind();
    }

    public unsafe void Data(Span<TDataType> data)
    {
        Bind();
        fixed (void* d = data)
        {
            _gl.BufferData(_bufferType, (nuint)(data.Length * sizeof(TDataType)), d, BufferUsageARB.StaticDraw);
        }
        _gl.DebugAssertSuccess();
        Unbind();
    }

    public void Bind()
    {
        _gl.BindBuffer(_bufferType, _handle);
        _gl.DebugAssertSuccess();
    }

    public void Unbind()
    {
        _gl.BindBuffer(_bufferType, 0);
        _gl.DebugAssertSuccess();
    }

    public void Dispose()
    {
        _gl.DeleteBuffer(_handle);
        _gl.DebugAssertSuccess();
    }
}