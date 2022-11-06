using Silk.NET.OpenGL;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace X11Overlay.GFX.OpenGL;


public class GlTexture : ITexture
{
    internal readonly uint Handle;

    private readonly GL _gl;

    public uint Width { get; private set; }
    public uint Height { get; private set; }

    private readonly bool _dynamic;

    public unsafe GlTexture(GL gl, string path, InternalFormat internalFormat = InternalFormat.Rgba8)
    {
        _gl = gl;
        Handle = _gl.GenTexture();
        _gl.GetError().AssertNone();

        Bind();

        using (var img = Image.Load<Rgba32>(path))
        {
            Allocate(internalFormat, (uint)img.Width, (uint)img.Height, PixelFormat.Rgba, PixelType.UnsignedByte,
                null);

            img.ProcessPixelRows(accessor =>
            {
                var maxY = accessor.Height - 1;
                for (int y = 0; y < accessor.Height; y++)
                {
                    fixed (void* data = accessor.GetRowSpan(y))
                    {
                        gl.TexSubImage2D(TextureTarget.Texture2D, 0, 0, maxY - y, (uint)accessor.Width, 1,
                            PixelFormat.Rgba, PixelType.UnsignedByte, data);
                        _gl.GetError().AssertNone();
                    }
                }
            });
        }

        SetParameters();
    }

    public unsafe GlTexture(GL gl, uint width, uint height, InternalFormat internalFormat = InternalFormat.Rgba8,
        bool dynamic = false)
    {
        _gl = gl;
        _dynamic = dynamic;

        Handle = _gl.GenTexture();
        _gl.GetError().AssertNone();

        Bind();

        //Reserve enough memory from the gpu for the whole image
        Allocate(internalFormat, width, height, PixelFormat.Rgba, PixelType.UnsignedByte, null);
        SetParameters();
    }

    public unsafe GlTexture(GL gl, void* data, uint width, uint height, PixelFormat pixelFormat = PixelFormat.Rgba,
        PixelType pixelType = PixelType.UnsignedByte, InternalFormat internalFormat = InternalFormat.Rgba8)
    {
        _gl = gl;
        Handle = _gl.GenTexture();
        _gl.GetError().AssertNone();
        Bind();

        _gl.PixelStore(GLEnum.PackAlignment, 1);
        _gl.PixelStore(GLEnum.UnpackAlignment, 1);
        //We want the ability to create a texture using data generated from code as well.
        //Setting the data of a texture.
        Allocate(internalFormat, width, height, pixelFormat, pixelType, data);
        SetParameters();
    }

    private void SetParameters()
    {
        //Setting some texture parameters so the texture behaves as expected.
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)GLEnum.ClampToEdge);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)GLEnum.ClampToEdge);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter,
            (int)GLEnum.LinearMipmapLinear);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)GLEnum.Linear);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureBaseLevel, 0);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMaxLevel, 8);
        //Generating mipmaps.
        _gl.GenerateMipmap(TextureTarget.Texture2D);
    }

    private unsafe void Allocate(InternalFormat internalFormat, uint width, uint height,
        PixelFormat pixelFormat, PixelType pixelType, void* data)
    {
        Width = width;
        Height = height;

        _gl.TexImage2D(TextureTarget.Texture2D, 0, (int)internalFormat, width, height, 0, pixelFormat, pixelType, data);
        _gl.GetError().AssertNone();
    }

    public void Bind(TextureUnit textureSlot = TextureUnit.Texture0)
    {
        //When we bind a texture we can choose which texture slot we can bind it to.
        _gl.ActiveTexture(textureSlot);
        _gl.BindTexture(TextureTarget.Texture2D, Handle);
    }

    public void Dispose()
    {
        //In order to dispose we need to delete the opengl handle for the texture.
        _gl.DeleteTexture(Handle);
    }

    public unsafe void LoadRawPixels(IntPtr ptr, GraphicsFormat graphicsFormat)
    {
        var (pf, pt) = GlGraphicsEngine.GraphicsFormatAsInput(graphicsFormat);

        var d = ptr.ToPointer();
        Bind();

        _gl.TexSubImage2D(TextureTarget.Texture2D, 0, 0, 0, Width, Height, pf, pt, d);
        _gl.GetError().AssertNone();
    }

    public uint GetWidth()
    {
        return Width;
    }

    public uint GetHeight()
    {
        return Height;
    }

    public IntPtr GetNativeTexturePtr()
    {
        return (IntPtr)Handle;
    }

    public bool IsDynamic()
    {
        return _dynamic;
    }
}