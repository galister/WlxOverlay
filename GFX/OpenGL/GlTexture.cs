using System.Runtime.InteropServices;
using System.Text;
using Silk.NET.OpenGL;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using WlxOverlay.Desktop;

namespace WlxOverlay.GFX.OpenGL;


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
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)GLEnum.Linear);
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
        //In order to dispose we need to delete the OpenGL handle for the texture.
        _gl.DeleteTexture(Handle);
    }

    public unsafe void LoadRawImage(IntPtr ptr, GraphicsFormat graphicsFormat, uint newWidth = 0, uint newHeight = 0)
    {
        var (pf, pt) = GlGraphicsEngine.GraphicsFormatAsInput(graphicsFormat);

        if (newWidth > 0)
            Width = newWidth;
        if (newHeight > 0)
            Height = newHeight;

        var d = ptr.ToPointer();
        Bind();

        //_gl.TexImage2D(TextureTarget.Texture2D, 0, InternalFormat.Rgba8, Width, Height, 0, pf, pt, d);
        _gl.TexSubImage2D(TextureTarget.Texture2D, 0, 0, 0, Width, Height, pf, pt, d);
        _gl.GetError().AssertNone();
    }

    public unsafe void LoadRawSubImage(IntPtr ptr, GraphicsFormat graphicsFormat, int xOffset, int yOffset, int width, int height)
    {
        var (pf, pt) = GlGraphicsEngine.GraphicsFormatAsInput(graphicsFormat);

        var d = ptr.ToPointer();
        Bind();

        _gl.TexSubImage2D(TextureTarget.Texture2D, 0, xOffset, yOffset, (uint)width, (uint)height, pf, pt, d);
        _gl.GetError().AssertNone();
    }

    public void CopyTo(ITexture target, uint width = 0, uint height = 0, int srcX = 0, int srcY = 0, int dstX = 0, int dstY = 0)
    {
        var glTarget = (GlTexture)target;

        if (width == 0)
            width = Width;

        if (height == 0)
            height = Height;

        _gl.CopyImageSubData(Handle, GLEnum.Texture2D, 0, srcX, srcY, 0,
            glTarget.Handle, GLEnum.Texture2D, 0, dstX, dstY, 0,
            width, height, 1);
    }

    public void LoadEglImage(IntPtr eglImage, uint width, uint height)
    {
        Bind();

        EGL.ImageTargetTexture2DOES((int)GLEnum.Texture2D, eglImage);
        _gl.GetError().AssertNone();

        Width = width;
        Height = height;
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