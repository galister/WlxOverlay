using Silk.NET.OpenGL;

namespace X11Overlay.GFX.OpenGL;


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
        Bind();
        _gl.GetError().AssertNone();
        fixed (void* d = data)
        {
            _gl.BufferData(bufferType, (nuint) (data.Length * sizeof(TDataType)), d, BufferUsageARB.StaticDraw);
        }
        _gl.GetError().AssertNone();
    }

    public unsafe void Data(Span<TDataType> data)
    {
        Bind();
        _gl.GetError().AssertNone();
        fixed (void* d = data)
        {
            _gl.BufferData(_bufferType, (nuint) (data.Length * sizeof(TDataType)), d, BufferUsageARB.StaticDraw);
        }
        _gl.GetError().AssertNone();
    } 

    public void Bind()
    {
        _gl.BindBuffer(_bufferType, _handle);
    }

    public void Dispose()
    {
        _gl.DeleteBuffer(_handle);
    }
}