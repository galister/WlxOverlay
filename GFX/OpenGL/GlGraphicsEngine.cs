using Silk.NET.OpenGL;
using Silk.NET.Maths;
using Silk.NET.Windowing;
using Silk.NET.Windowing.Glfw;
using Valve.VR;
using X11Overlay.Core;

namespace X11Overlay.GFX.OpenGL;

public sealed class GlGraphicsEngine : IGraphicsEngine
{
    private GL _gl = null!;
    private IWindow _window = null!;

    public static GlShader SpriteShader = null!;
    public static GlShader ColorShader = null!;
    public static GlShader FontShader = null!;

    public GlGraphicsEngine()
    {
        if (GraphicsEngine.Instance != null)
            throw new InvalidOperationException("Another GraphicsEngine exists.");
        GraphicsEngine.Instance = this;
    }

    public void StartEventLoop()
    {
        var options = WindowOptions.Default;
        options.IsVisible = false;
        options.Size = new Vector2D<int>(800, 600);
        options.Title = "X11Overlay";
        options.API = new GraphicsAPI(ContextAPI.OpenGL, ContextProfile.Core, ContextFlags.Default, new APIVersion(4, 5));
        options.VSync = false;

        GlfwWindowing.Use();
        
        _window = Window.Create(options);
        _window.Load += OnLoad;
        _window.Render += OnRender;
        _window.Run();
    }

    private void OnLoad()
    {
        _gl = GL.GetApi(_window.GLContext);
        _gl.Enable(EnableCap.Texture2D);
        _gl.Enable(EnableCap.Blend);

        _gl.GetError();
        Console.WriteLine("GL Context initialized");

        SpriteShader = new GlShader(_gl, "Shaders/common.vert", "Shaders/sprite.frag");
        ColorShader = new GlShader(_gl, "Shaders/common.vert", "Shaders/color.frag");
        FontShader = new GlShader(_gl, "Shaders/common.vert", "Shaders/font.frag");

        GraphicsEngine.UiRenderer = new GlUiRenderer(_gl);
    }

    private void OnRender(double _)
    {
        OverlayManager.Instance.Update();
        
        // Use this instead of vsync to prevent glfw from using up the entire CPU core
        OverlayManager.Instance.WaitForEndOfFrame();
    }

    public ITexture TextureFromFile(string path, GraphicsFormat internalFormat = GraphicsFormat.RGBA8)
    {
        var internalFmt = GraphicsFormatAsInternal(internalFormat);
        return new GlTexture(_gl, path, internalFmt);
    }

    public ITexture EmptyTexture(uint width, uint height, GraphicsFormat internalFormat = GraphicsFormat.RGBA8, bool dynamic = false)
    {
        var internalFmt = GraphicsFormatAsInternal(internalFormat);
        return new GlTexture(_gl, width, height, internalFmt, dynamic);
    }

    public ITexture TextureFromRaw(uint width, uint height, GraphicsFormat inputFormat, IntPtr data,
        GraphicsFormat internalFormat = GraphicsFormat.RGBA8)
    {
        unsafe
        {
            var internalFmt = GraphicsFormatAsInternal(internalFormat);
            var (pixelFmt, pixelType) = GraphicsFormatAsInput(inputFormat);
            return new GlTexture(_gl, data.ToPointer(), width, height, pixelFmt, pixelType, internalFmt);
        }
    }
    
    public unsafe ITexture TextureFromRaw(uint width, uint height, GraphicsFormat inputFormat, Span<byte> data,
        GraphicsFormat internalFormat = GraphicsFormat.RGBA8)
    {
        var internalFmt = GraphicsFormatAsInternal(internalFormat);
        var (pixelFmt, pixelType) = GraphicsFormatAsInput(inputFormat);
        fixed (void* ptr = &data[0])
        {
            return new GlTexture(_gl, ptr, width, height, pixelFmt, pixelType, internalFmt);
        }
    }

    public ETextureType GetTextureType()
    {
        return ETextureType.OpenGL;
    }

    public void Shutdown()
    {
        _window.Close();
    }

    internal static (PixelFormat pf, PixelType pt) GraphicsFormatAsInput(GraphicsFormat format)
    {
        return format switch
        {
            GraphicsFormat.RGBA8 => (PixelFormat.Rgba, PixelType.UnsignedByte),
            GraphicsFormat.BGRA8 => (PixelFormat.Bgra, PixelType.UnsignedByte),
            GraphicsFormat.RGB8 => (PixelFormat.Rgb, PixelType.UnsignedByte),
            GraphicsFormat.BGR8 => (PixelFormat.Bgr, PixelType.UnsignedByte),
            GraphicsFormat.R8 => (PixelFormat.Red, PixelType.UnsignedByte),
            GraphicsFormat.R16 => (PixelFormat.Red, PixelType.UnsignedShort),
            GraphicsFormat.R32 => (PixelFormat.Red, PixelType.UnsignedInt),
            GraphicsFormat.RG8 => (PixelFormat.RG, PixelType.UnsignedByte),
            _ => throw new ArgumentOutOfRangeException(nameof(format), format, null)
        };
    }
    
    internal static InternalFormat GraphicsFormatAsInternal(GraphicsFormat format)
    {
        return format switch
        {
            GraphicsFormat.RGBA8 => InternalFormat.Rgba8,
            GraphicsFormat.RGB8 => InternalFormat.Rgb8,
            GraphicsFormat.RG8 => InternalFormat.RG8,
            GraphicsFormat.R8 => InternalFormat.R8,
            _ => throw new ArgumentOutOfRangeException(nameof(format), format, null)
        };
    }
}