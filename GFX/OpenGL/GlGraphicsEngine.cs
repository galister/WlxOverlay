using Silk.NET.Core;
using Silk.NET.OpenGL;
using Silk.NET.Maths;
using Silk.NET.OpenXR;
using Silk.NET.Windowing;
using Silk.NET.Windowing.Glfw;
using Valve.VR;
using WlxOverlay.Core;
using WlxOverlay.Types;

namespace WlxOverlay.GFX.OpenGL;

public sealed class GlGraphicsEngine : IGraphicsEngine
{
    private GL _gl = null!;
    private IWindow _window = null!;

    public static GlShader SpriteShader = null!;
    public static GlShader ColorShader = null!;
    public static GlShader FontShader = null!;
    public static GlShader SrgbShader = null!;
    public static GlShader QuadShader = null!;

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
        options.Title = "WlxOverlay";
        options.API = new GraphicsAPI(ContextAPI.OpenGL, ContextProfile.Core, ContextFlags.ForwardCompatible, new APIVersion(4, 5));
        options.VSync = false;

        GlfwWindowing.Use();

        _window = Window.Create(options);
        _window.Load += OnLoad;
        _window.Render += OnRender;
        _window.Run();
    }

    private void OnLoad()
    {
        _gl = GL.GetApi(_window);
        
        _gl.Enable(EnableCap.Texture2D);
        _gl.DebugAssertSuccess();
        
        //_gl.Enable(EnableCap.DepthTest);
        //_gl.DebugAssertSuccess();
        
        _gl.Enable(EnableCap.Blend);
        _gl.DebugAssertSuccess();

        _gl.GetError();
        Console.WriteLine("GL Context initialized");

        var vertShader = GetShaderPath("common.vert");
        SpriteShader = new GlShader(_gl, vertShader, GetShaderPath("sprite.frag"));
        ColorShader = new GlShader(_gl, vertShader, GetShaderPath("color.frag"));
        FontShader = new GlShader(_gl, vertShader, GetShaderPath("font.frag"));
        SrgbShader = new GlShader(_gl, vertShader, GetShaderPath("srgb.frag"));
        QuadShader = new GlShader(_gl, vertShader, GetShaderPath("tex-color.frag"));

        GraphicsEngine.Renderer = new GlRenderer(_gl);
        MainLoop.Initialize();
    }

    public GraphicsBinding XrGraphicsBinding()
    {
        var glfwWindow = _window.Native!.Glfw!.Value;
        var display = glfwGetEGLDisplay();
        var handle = glfwGetEGLContext(glfwWindow);
        var config = IntPtr.Zero;

        // Hack: GLFW does not expose EGLConfig, but we know that it comes right before the context handle.
        for (var i = 0; i < 1000; i++) unsafe
        {
            var lp = (IntPtr*) IntPtr.Add(glfwWindow, i);
            var val = *lp;
            if (val == handle)
            {
                config = *(IntPtr*)IntPtr.Add(glfwWindow, i - 8).ToPointer();
                break;
            }
        }

        if (config == IntPtr.Zero)
            throw new ApplicationException("Could not find EGLConfig");

        GetProcAddress getProcAddress = EGL.GetProcAddress;
        
        var binding = new GraphicsBindingEGLMNDX
        {
            Type = StructureType.GraphicsBindingEglMndx,
            Display = display,
            Config = config,
            Context = handle,
            GetProcAddress = new PfnVoidFunction(getProcAddress),
        };
        return binding;
    }
    
    private delegate IntPtr GetProcAddress(string s);

    public GlStereoRenderer CreateStereoRenderer()
    {
        return new GlStereoRenderer(_gl);
    }

    private string GetShaderPath(string shader)
    {
        return Path.Combine(Config.AppDir, "Shaders", shader);
    }

    private void OnRender(double _)
    {
        MainLoop.Update();
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

    public ITexture TextureFromHandle(IntPtr handle, uint width, uint height,
        GraphicsFormat internalFormat = GraphicsFormat.RGBA8)
    {
        return new GlTexture(_gl, handle, width, height, GraphicsFormatAsInternal(internalFormat));
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
            GraphicsFormat.RGB_Float => (PixelFormat.Rgb, PixelType.Float),
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
    
    [DllImport("libglfw.so")]
    public static extern IntPtr glfwGetEGLDisplay();
    
    [DllImport("libglfw.so")]
    public static extern IntPtr glfwGetEGLContext(IntPtr glfwWindow);
}