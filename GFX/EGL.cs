using Silk.NET.GLFW;

// ReSharper disable InconsistentNaming

namespace WlxOverlay.GFX;

public static class EGL
{
    private static bool _initialized;
    
    public static readonly EglEnum[,] DmaBufAttribs = {
        { EglEnum.DmaBufPlane0FdExt, EglEnum.DmaBufPlane0OffsetExt, EglEnum.DmaBufPlane0PitchExt, EglEnum.DmaBufPlane0ModifierLoExt, EglEnum.DmaBufPlane0ModifierHiExt },
        { EglEnum.DmaBufPlane1FdExt, EglEnum.DmaBufPlane1OffsetExt, EglEnum.DmaBufPlane1PitchExt, EglEnum.DmaBufPlane1ModifierLoExt, EglEnum.DmaBufPlane1ModifierHiExt },
        { EglEnum.DmaBufPlane2FdExt, EglEnum.DmaBufPlane2OffsetExt, EglEnum.DmaBufPlane2PitchExt, EglEnum.DmaBufPlane2ModifierLoExt, EglEnum.DmaBufPlane2ModifierHiExt },
        { EglEnum.DmaBufPlane3FdExt, EglEnum.DmaBufPlane3OffsetExt, EglEnum.DmaBufPlane3PitchExt, EglEnum.DmaBufPlane3ModifierLoExt, EglEnum.DmaBufPlane3ModifierHiExt },
    };

    public static IntPtr Display { get; private set; }

    public static void UseExisting(IntPtr eglDisplay)
    {
        if (_initialized)
            return;
        _initialized = true;
        
        LoadPfn("eglQueryDmaBufFormatsEXT", ref QueryDmaBufFormatsEXT);
        LoadPfn("eglQueryDmaBufModifiersEXT", ref QueryDmaBufModifiersEXT);

        Display = eglDisplay;
    }
    
    public static void Initialize()
    {
        if (_initialized)
            return;
        _initialized = true;
        
        LoadPfn("eglGetPlatformDisplayEXT", ref eglGetPlatformDisplayEXT);

        if (LoadPfn("glEGLImageTargetTexture2DOES", ref ImageTargetTexture2DOES, false))
        {
            Display = eglGetPlatformDisplayEXT(EglEnum.PlatformWaylandExt, IntPtr.Zero, IntPtr.Zero);

            if (Display == IntPtr.Zero)
            {
                Console.WriteLine("eglGetPlatformDisplayEXT returned EGL_NO_DISPLAY!");
                Display = GetDisplay(IntPtr.Zero);
            }
        }
        else
        {
            Console.WriteLine("Could not get function pointer to glEGLImageTargetTexture2DOES");
            Display = GetDisplay(IntPtr.Zero);
        }

        if (Display == IntPtr.Zero)
        {
            Console.WriteLine("eglGetDisplay returned EGL_NO_DISPLAY!");
            throw new ApplicationException("Could not get EGL display!");
        }

        if (Initialize(Display, out var major, out var minor) == EglEnum.False)
            throw new ApplicationException("eglInitialize returned EGL_FALSE!");

        GlfwProvider.GLFW.Value.WindowHint(WindowHintContextApi.ContextCreationApi, ContextApi.EglContextApi);

        Console.WriteLine($"EGL {major}.{minor} initialized.");
    }

    private static bool LoadPfn<T>(string name, ref T target, bool throwIfMissing = true) where T : Delegate
    {
        var pfn = GetProcAddress(name);
        if (pfn == IntPtr.Zero)
        {
            if (throwIfMissing)
                throw new ApplicationException($"Could not get function pointer to {name}");
            return false;
        }
        target = Marshal.GetDelegateForFunctionPointer<T>(pfn);
        return true;
    }

    [DllImport("libEGL.so.1", CharSet = CharSet.Ansi, EntryPoint = "eglCreateImage")]
    public static extern IntPtr CreateImage(IntPtr dpy, IntPtr ctx, EglEnum target, IntPtr buffer, IntPtr[] attrib_list);

    [DllImport("libEGL.so.1", CharSet = CharSet.Ansi, EntryPoint = "eglDestroyImage")]
    public static extern IntPtr DestroyImage(IntPtr dpy, IntPtr image);

    [DllImport("libEGL.so.1", CharSet = CharSet.Ansi, EntryPoint = "eglGetDisplay")]
    private static extern IntPtr GetDisplay(IntPtr display);

    [DllImport("libEGL.so.1", CharSet = CharSet.Ansi, EntryPoint = "eglInitialize")]
    private static extern EglEnum Initialize(IntPtr display, out int major, out int minor);

    [DllImport("libEGL.so.1", CharSet = CharSet.Ansi, EntryPoint = "eglGetError")]
    public static extern EglEnum GetError();

    [DllImport("libEGL.so.1", CharSet = CharSet.Ansi, EntryPoint = "eglGetProcAddress")]
    public static extern IntPtr GetProcAddress([MarshalAs(UnmanagedType.LPStr)] string procName);
    
    [DllImport("libGL.so.1", CharSet = CharSet.Ansi, EntryPoint = "glXGetProcAddress")]
    public static extern IntPtr GlXGetProcAddress([MarshalAs(UnmanagedType.LPStr)] string procName);

    public static glEGLImageTargetTexture2DOES ImageTargetTexture2DOES = null!;

    public static eglQueryDmaBufModifiersEXT QueryDmaBufModifiersEXT = null!;
    public static eglQueryDmaBufFormatsEXT QueryDmaBufFormatsEXT = null!;
    private static eglGetPlatformDisplayEXTDelegate eglGetPlatformDisplayEXT = null!;
    public unsafe delegate EglEnum eglQueryDmaBufFormatsEXT(IntPtr dpy, int maxFormats, DrmFormat* formats, int* numFormats);
    public unsafe delegate EglEnum eglQueryDmaBufModifiersEXT(IntPtr dpy, DrmFormat format, int maxModifiers, ulong* modifiers, IntPtr externalOnly, int* numModifiers);

    private delegate IntPtr eglGetPlatformDisplayEXTDelegate(EglEnum platform, IntPtr nativeDevice, IntPtr attribs);

    public delegate void glEGLImageTargetTexture2DOES(int target, IntPtr eglImage);
}

public enum EglEnum
{
    AlphaSize = 0x3021,
    BadAccess = 0x3002,
    BadAlloc = 0x3003,
    BadAttribute = 0x3004,
    BadConfig = 0x3005,
    BadContext = 0x3006,
    BadCurrentSurface = 0x3007,
    BadDisplay = 0x3008,
    BadMatch = 0x3009,
    BadNativePixmap = 0x300A,
    BadNativeWindow = 0x300B,
    BadParameter = 0x300C,
    BadSurface = 0x300D,
    BlueSize = 0x3022,
    BufferSize = 0x3020,
    ConfigCaveat = 0x3027,
    ConfigId = 0x3028,
    CoreNativeEngine = 0x305B,
    DepthSize = 0x3025,
    DontCare = -1,
    Draw = 0x3059,
    Extensions = 0x3055,
    False = 0,
    GreenSize = 0x3023,
    Height = 0x3056,
    LargestPbuffer = 0x3058,
    Level = 0x3029,
    MaxPbufferHeight = 0x302A,
    MaxPbufferPixels = 0x302B,
    MaxPbufferWidth = 0x302C,
    NativeRenderable = 0x302D,
    NativeVisualId = 0x302E,
    NativeVisualType = 0x302F,
    None = 0x3038,
    NonConformantConfig = 0x3051,
    NotInitialized = 0x3001,
    PbufferBit = 0x0001,
    PixmapBit = 0x0002,
    Read = 0x305A,
    RedSize = 0x3024,
    Samples = 0x3031,
    SampleBuffers = 0x3032,
    SlowConfig = 0x3050,
    StencilSize = 0x3026,
    Success = 0x3000,
    SurfaceType = 0x3033,
    TransparentBlueValue = 0x3035,
    TransparentGreenValue = 0x3036,
    TransparentRedValue = 0x3037,
    TransparentRgb = 0x3052,
    TransparentType = 0x3034,
    True = 1,
    Vendor = 0x3053,
    Version = 0x3054,
    Width = 0x3057,
    WindowBit = 0x0004,
    BackBuffer = 0x3084,
    BindToTextureRgb = 0x3039,
    BindToTextureRgba = 0x303A,
    ContextLost = 0x300E,
    MinSwapInterval = 0x303B,
    MaxSwapInterval = 0x303C,
    MipmapTexture = 0x3082,
    MipmapLevel = 0x3083,
    NoTexture = 0x305C,
    Texture2D = 0x305F,
    TextureFormat = 0x3080,
    TextureRgb = 0x305D,
    TextureRgba = 0x305E,
    TextureTarget = 0x3081,
    AlphaFormat = 0x3088,
    AlphaFormatNonpre = 0x308B,
    AlphaFormatPre = 0x308C,
    AlphaMaskSize = 0x303E,
    BufferPreserved = 0x3094,
    BufferDestroyed = 0x3095,
    ClientApis = 0x308D,
    Colorspace = 0x3087,
    ColorspaceSRgb = 0x3089,
    ColorspaceLinear = 0x308A,
    ColorBufferType = 0x303F,
    ContextClientType = 0x3097,
    DisplayScaling = 10000,
    HorizontalResolution = 0x3090,
    LuminanceBuffer = 0x308F,
    LuminanceSize = 0x303D,
    OpenglEsBit = 0x0001,
    OpenvgBit = 0x0002,
    OpenglEsApi = 0x30A0,
    OpenvgApi = 0x30A1,
    OpenvgImage = 0x3096,
    PixelAspectRatio = 0x3092,
    RenderableType = 0x3040,
    RenderBuffer = 0x3086,
    RgbBuffer = 0x308E,
    SingleBuffer = 0x3085,
    SwapBehavior = 0x3093,
    Unknown = -1,
    VerticalResolution = 0x3091,
    Conformant = 0x3042,
    ContextClientVersion = 0x3098,
    MatchNativePixmap = 0x3041,
    OpenglEs2Bit = 0x0004,
    VgAlphaFormat = 0x3088,
    VgAlphaFormatNonpre = 0x308B,
    VgAlphaFormatPre = 0x308C,
    VgAlphaFormatPreBit = 0x0040,
    VgColorspace = 0x3087,
    VgColorspaceSRgb = 0x3089,
    VgColorspaceLinear = 0x308A,
    VgColorspaceLinearBit = 0x0020,
    DefaultDisplay = 0,
    MultisampleResolveBoxBit = 0x0200,
    MultisampleResolve = 0x3099,
    MultisampleResolveDefault = 0x309A,
    MultisampleResolveBox = 0x309B,
    OpenglApi = 0x30A2,
    OpenglBit = 0x0008,
    SwapBehaviorPreservedBit = 0x0400,
    ContextMajorVersion = 0x3098,
    ContextMinorVersion = 0x30FB,
    ContextOpenglProfileMask = 0x30FD,
    ContextOpenglResetNotificationStrategy = 0x31BD,
    NoResetNotification = 0x31BE,
    LoseContextOnReset = 0x31BF,
    ContextOpenglCoreProfileBit = 0x00000001,
    ContextOpenglCompatibilityProfileBit = 0x00000002,
    ContextOpenglDebug = 0x31B0,
    ContextOpenglForwardCompatible = 0x31B1,
    ContextOpenglRobustAccess = 0x31B2,
    OpenglEs3Bit = 0x00000040,
    ClEventHandle = 0x309C,
    SyncClEvent = 0x30FE,
    SyncClEventComplete = 0x30FF,
    SyncPriorCommandsComplete = 0x30F0,
    SyncType = 0x30F7,
    SyncStatus = 0x30F1,
    SyncCondition = 0x30F8,
    Signaled = 0x30F2,
    Unsignaled = 0x30F3,
    SyncFlushCommandsBit = 0x0001,
    TimeoutExpired = 0x30F5,
    ConditionSatisfied = 0x30F6,
    SyncFence = 0x30F9,
    GlColorspace = 0x309D,
    GlColorspaceSrgb = 0x3089,
    GlColorspaceLinear = 0x308A,
    GlRenderbuffer = 0x30B9,
    GlTexture2D = 0x30B1,
    GlTextureLevel = 0x30BC,
    GlTexture3D = 0x30B2,
    GlTextureZoffset = 0x30BD,
    GlTextureCubeMapPositiveX = 0x30B3,
    GlTextureCubeMapNegativeX = 0x30B4,
    GlTextureCubeMapPositiveY = 0x30B5,
    GlTextureCubeMapNegativeY = 0x30B6,
    GlTextureCubeMapPositiveZ = 0x30B7,
    GlTextureCubeMapNegativeZ = 0x30B8,
    ImagePreserved = 0x30D2,
    LinuxDmaBufExt = 0x3270,
    LinuxDrmFourccExt = 0x3271,
    DmaBufPlane0FdExt = 0x3272,
    DmaBufPlane0OffsetExt = 0x3273,
    DmaBufPlane0PitchExt = 0x3274,
    DmaBufPlane1FdExt = 0x3275,
    DmaBufPlane1OffsetExt = 0x3276,
    DmaBufPlane1PitchExt = 0x3277,
    DmaBufPlane2FdExt = 0x3278,
    DmaBufPlane2OffsetExt = 0x3279,
    DmaBufPlane2PitchExt = 0x327A,
    YuvColorSpaceHintExt = 0x327B,
    SampleRangeHintExt = 0x327C,
    YuvChromaHorizontalSitingHintExt = 0x327D,
    YuvChromaVerticalSitingHintExt = 0x327E,
    ItuRec601Ext = 0x327F,
    ItuRec709Ext = 0x3280,
    ItuRec2020Ext = 0x3281,
    YuvFullRangeExt = 0x3282,
    YuvNarrowRangeExt = 0x3283,
    YuvChromaSiting0Ext = 0x3284,
    YuvChromaSiting05Ext = 0x3285,
    DmaBufPlane3FdExt = 0x3440,
    DmaBufPlane3OffsetExt = 0x3441,
    DmaBufPlane3PitchExt = 0x3442,
    DmaBufPlane0ModifierLoExt = 0x3443,
    DmaBufPlane0ModifierHiExt = 0x3444,
    DmaBufPlane1ModifierLoExt = 0x3445,
    DmaBufPlane1ModifierHiExt = 0x3446,
    DmaBufPlane2ModifierLoExt = 0x3447,
    DmaBufPlane2ModifierHiExt = 0x3448,
    DmaBufPlane3ModifierLoExt = 0x3449,
    DmaBufPlane3ModifierHiExt = 0x344A,
    PlatformWaylandExt = 0x31D8,
    PlatformX11Ext = 0x31D5,
    PlatformX11ScreenExt = 0x31D6,
    PlatformXcbExt = 0x31DC,
    PlatformXcbScreenExt = 0x31DE,
    ContextMajorVersionKhr = 0x3098,
    ContextMinorVersionKhr = 0x30FB,
    ContextFlagsKhr = 0x30FC,
    ContextOpenglProfileMaskKhr = 0x30FD,
    ContextOpenglResetNotificationStrategyKhr = 0x31BD,
    NoResetNotificationKhr = 0x31BE,
    LoseContextOnResetKhr = 0x31BF,
    ContextOpenglDebugBitKhr = 0x00000001,
    ContextOpenglForwardCompatibleBitKhr = 0x00000002,
    ContextOpenglRobustAccessBitKhr = 0x00000004,
    ContextOpenglCoreProfileBitKhr = 0x00000001,
    ContextOpenglCompatibilityProfileBitKhr = 0x00000002,
    OpenglEs3BitKhr = 0x00000040,
    ImagePreservedKhr = 0x30D2,
    NativePixmapKhr = 0x30B0,
    PlatformWaylandKhr = 0x31D8,
    PlatformX11Khr = 0x31D5,
    PlatformX11ScreenKhr = 0x31D6,
}

public enum DrmFormat
{
    DRM_FORMAT_ARGB8888 = 0x34325241,
    DRM_FORMAT_ABGR8888 = 0x34324241,
    DRM_FORMAT_XRGB8888 = 0x34325258,
    DRM_FORMAT_XBGR8888 = 0x34324258
}

