using Silk.NET.OpenGL;
using Silk.NET.Maths;
using Silk.NET.Windowing;
using Silk.NET.Windowing.Glfw;
using Valve.VR;
using X11Overlay.Core;

namespace X11Overlay.GFX.OpenGL;

public sealed class GlGraphicsEngine : IGraphicsEngine
{
    private GL Gl = null!;
    private IWindow _window = null!;

    public static GlShader BlendShader = null!;

    public GlGraphicsEngine()
    {
        if (GraphicsEngine.Instance != null)
            throw new InvalidOperationException("Another GraphicsEngine exists.");
        GraphicsEngine.Instance = this;
        
        var ctx = GL.CreateDefaultContext("libGL.so");
        if (ctx == null)
            throw new ApplicationException("Could not initialize GL Context!");
    }

    public void StartEventLoop()
    {
        var options = WindowOptions.Default;
        options.IsVisible = false;
        options.Size = new Vector2D<int>(800, 600);
        options.Title = "X11Overlay";
        options.API = new GraphicsAPI(ContextAPI.OpenGL, ContextProfile.Core, ContextFlags.Default, new APIVersion(4, 5));
        options.VSync = false;
        options.UpdatesPerSecond = 90;
        options.FramesPerSecond = 45;
        
        GlfwWindowing.Use();
        
        _window = Window.Create(options);
        _window.Load += OnLoad;
        _window.Render += OnRender;
        _window.Run();
    }

    private void OnLoad()
    {
        Gl = GL.GetApi(_window.GLContext);
        Gl.Enable(EnableCap.Texture2D);

        Console.WriteLine($"GL Context initialized: {Gl.GetError()}");
        GraphicsEngine.Ready = true;

        BlendShader = new GlShader(Gl, "Shaders/blend.vert", "Shaders/blend.frag");
    }

    private void OnRender(double _)
    {
        OverlayManager.Instance.Update();
        OverlayManager.Instance.Render();
    }

    public ITexture TextureFromFile(string path, GraphicsFormat internalFormat = GraphicsFormat.RGBA8)
    {
        var internalFmt = GraphicsFormatAsInternal(internalFormat);
        return new GlTexture(Gl, path, internalFmt);
    }

    public ITexture EmptyTexture(uint width, uint height, GraphicsFormat internalFormat = GraphicsFormat.RGBA8, bool dynamic = false)
    {
        var internalFmt = GraphicsFormatAsInternal(internalFormat);
        return new GlTexture(Gl, width, height, internalFmt, dynamic);
    }

    public ITexture TextureFromRaw(uint width, uint height, GraphicsFormat inputFormat, IntPtr data,
        GraphicsFormat internalFormat = GraphicsFormat.RGBA8)
    {
        unsafe
        {
            var internalFmt = GraphicsFormatAsInternal(internalFormat);
            var (pixelFmt, pixelType) = GraphicsFormatAsInput(inputFormat);
            return new GlTexture(Gl, data.ToPointer(), width, height, pixelFmt, pixelType, internalFmt);
        }
    }
    
    public unsafe ITexture TextureFromRaw(uint width, uint height, GraphicsFormat inputFormat, Span<byte> data,
        GraphicsFormat internalFormat = GraphicsFormat.RGBA8)
    {
        var internalFmt = GraphicsFormatAsInternal(internalFormat);
        var (pixelFmt, pixelType) = GraphicsFormatAsInput(inputFormat);
        fixed (void* ptr = &data[0])
        {
            return new GlTexture(Gl, ptr, width, height, pixelFmt, pixelType, internalFmt);
        }
    }

    public ETextureType GetTextureType()
    {
        return ETextureType.OpenGL;
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