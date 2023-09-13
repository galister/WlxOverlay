using Tmds.Linux;
using WlxOverlay.Backend;
using WlxOverlay.Capture.PipeWire;
using WlxOverlay.Desktop;
using WlxOverlay.GFX;
using WlxOverlay.GFX.OpenGL;
using WlxOverlay.Types;

namespace WlxOverlay.Capture;

public class PipeWireCapture : IDesktopCapture
{
    private static readonly IReadOnlyDictionary<DrmFormat, uint> ToSpaFormats = new Dictionary<DrmFormat, uint>
    {
        [DrmFormat.DRM_FORMAT_XBGR8888] = 7U,  // SPA_VIDEO_FORMAT_RGBx
        [DrmFormat.DRM_FORMAT_XRGB8888] = 8U,  // SPA_VIDEO_FORMAT_BGRx
        [DrmFormat.DRM_FORMAT_ABGR8888] = 11U, // SPA_VIDEO_FORMAT_RGBA
        [DrmFormat.DRM_FORMAT_ARGB8888] = 12U, // SPA_VIDEO_FORMAT_BGRA
    };
    private static readonly IReadOnlyDictionary<int, DrmFormat> FromSpaFormats = new Dictionary<int, DrmFormat>
    {
        [7] = DrmFormat.DRM_FORMAT_XBGR8888,  // SPA_VIDEO_FORMAT_RGBx
        [8] = DrmFormat.DRM_FORMAT_XRGB8888,  // SPA_VIDEO_FORMAT_BGRx
        [11] = DrmFormat.DRM_FORMAT_ABGR8888, // SPA_VIDEO_FORMAT_RGBA
        [12] = DrmFormat.DRM_FORMAT_ARGB8888, // SPA_VIDEO_FORMAT_BGRA
    };

    private readonly uint _nodeId;
    private readonly string _name;
    private uint _width;
    private uint _height;

    private nint _handle;

    private nint _onFrameHandle;
    private OnFrameDelegate? _onFrameDelegate;

    private nint _eglImage;
    private nint _lastEglImage;

    private readonly object _attribsLock = new();
    private readonly nint[] _attribs = new nint[47];

    private static string? _pwVersion;

    private static IntPtr _dmaBufFormats = IntPtr.Zero;

    public PipeWireCapture(BaseOutput output, uint nodeId)
    {
        _nodeId = nodeId;
        _name = output.Name;
        _width = (uint)output.Size.X;
        _height = (uint)output.Size.Y;
    }

    public static void Load()
    {
        _pwVersion = Marshal.PtrToStringAnsi(pw_get_library_version());
        Console.WriteLine("PipeWire version: " + _pwVersion);

        if (!EGL.IsDmabufSupported())
        {
            Console.WriteLine("EGL extensions not loaded, DMA-Buf will not be used.");
            return;
        }

        if (string.Compare(_pwVersion, "0.3.33", StringComparison.Ordinal) >= 0)
            LoadDmaBufFormats();
    }

    private static unsafe void LoadDmaBufFormats()
    {
        var numFormats = 0;
        if (EGL.QueryDmaBufFormatsEXT(EGL.Display, 0, null, &numFormats) != EglEnum.True)
            return;

        var formatValues = stackalloc DrmFormat[numFormats + 1];

        if (EGL.QueryDmaBufFormatsEXT(EGL.Display, numFormats, formatValues, &numFormats) != EglEnum.True)
            return;

        var validFormats = Enum.GetValues<DrmFormat>();
        var validatedFormats = new List<DrmFormat>();
        for (var i = 0; i < numFormats; i++)
        {
            var format = formatValues[i];
            if (!validFormats.Contains(format))
                continue;
            validatedFormats.Add(format);
        }

        Console.WriteLine("Using DMA-Buf with supported formats: " + string.Join(", ", validatedFormats));

        var container = (format_collection*)Marshal.AllocHGlobal(Marshal.SizeOf<format_collection>());

        container->num_formats = validatedFormats.Count;
        container->formats = (capture_format*)Marshal.AllocHGlobal(container->num_formats * Marshal.SizeOf<capture_format>()).ToPointer();

        for (var i = 0; i < validatedFormats.Count; i++)
        {
            var format = validatedFormats[i];

            var numModifiers = 0;

            if (EGL.QueryDmaBufModifiersEXT(EGL.Display, format, 0, null, IntPtr.Zero, &numModifiers) != EglEnum.True)
                return;

            var modifiers = (ulong*)Marshal.AllocHGlobal(Marshal.SizeOf<ulong>() * numModifiers);
            if (EGL.QueryDmaBufModifiersEXT(EGL.Display, format, numModifiers, modifiers, IntPtr.Zero, &numModifiers) != EglEnum.True)
                return;

            container->formats[i].format = ToSpaFormats[format];
            container->formats[i].num_modifiers = numModifiers;
            container->formats[i].modifiers = modifiers;
        }

        _dmaBufFormats = (IntPtr)container;
    }

    /// <summary>
    /// Call this from BaseOverlay.Render()
    /// </summary>
    public unsafe bool TryApplyToTexture(ITexture texture)
    {
        var retVal = false;
        if (texture is not GlTexture glTexture)
            return retVal;

        if (_lastEglImage != IntPtr.Zero)
            EGL.DestroyImage(EGL.Display, _lastEglImage);

        lock (_attribsLock)
        {
            if (_attribs[0] == (nint)EglEnum.Width)
            {
                _lastEglImage = _eglImage;
                _eglImage = EGL.CreateImage(EGL.Display, IntPtr.Zero, EglEnum.LinuxDmaBufExt, IntPtr.Zero, _attribs);
                var error = EGL.GetError();
                if (error != EglEnum.Success)
                    throw new ApplicationException($"{error} on eglCreateImage!");

                glTexture.Resize(_width, _height);
                glTexture.LoadEglImage(_eglImage, _width, _height);
                retVal = true;
            }
            else if (_attribs[0] == (nint)spa_data_type.SPA_DATA_MemPtr)
            {
                var fmt = Config.Instance.WaylandColorSwap
                    ? GraphicsFormat.RGBA8
                    : GraphicsFormat.BGRA8;

                glTexture.Resize(_width, _height);
                texture.LoadRawImage(_attribs[1], fmt, _width, _height);
                retVal = true;
            }
            else if (_attribs[0] == (nint)spa_data_type.SPA_DATA_MemFd)
            {
                var fmt = Config.Instance.WaylandColorSwap
                    ? GraphicsFormat.RGBA8
                    : GraphicsFormat.BGRA8;

                var len = (int)_attribs[2];
                var off = (uint)_attribs[3];
                var map = LibC.mmap(null, len, LibC.PROT_READ, LibC.MAP_SHARED, (int)_attribs[1], off);

                glTexture.Resize(_width, _height);
                texture.LoadRawImage(new IntPtr(map), fmt, _width, _height);

                LibC.munmap(map, len);
                retVal = true;
            }

            _attribs[0] = IntPtr.Zero;
        }
        return retVal;
    }

    public unsafe void Initialize()
    {
        var fps = (uint)XrBackend.Current.DisplayFrequency;

        _onFrameDelegate = OnFrame;
        _onFrameHandle = Marshal.GetFunctionPointerForDelegate(_onFrameDelegate);
        _handle = wlxpw_initialize(_name, _nodeId, fps, _dmaBufFormats, _onFrameHandle);
    }

    public void Pause()
    {
        if (_dmaBufFormats == IntPtr.Zero)
            wlxpw_set_active(_handle, 0U);
    }

    public void Resume()
    {
        if (_dmaBufFormats == IntPtr.Zero)
            wlxpw_set_active(_handle, 1U);
    }

    private unsafe void OnFrame(spa_buffer* pb, spa_video_info* info)
    {
        lock (_attribsLock)
        {
            _width = info->raw.size.width;
            _height = info->raw.size.height;

            switch (pb->datas[0].type)
            {
                case spa_data_type.SPA_DATA_DmaBuf:
                    {
                        var planes = pb->n_datas;

                        var format = FromSpaFormats[info->raw.format];

                        var i = 0;
                        _attribs[i++] = (nint)EglEnum.Width;
                        _attribs[i++] = (nint)_width;
                        _attribs[i++] = (nint)EglEnum.Height;
                        _attribs[i++] = (nint)_height;
                        _attribs[i++] = (nint)EglEnum.LinuxDrmFourccExt;
                        _attribs[i++] = (int)format;

                        for (var p = 0U; p < planes; p++)
                        {
                            _attribs[i++] = (nint)EGL.DmaBufAttribs[p, 0];
                            _attribs[i++] = (nint)pb->datas[p].fd;
                            _attribs[i++] = (nint)EGL.DmaBufAttribs[p, 1];
                            _attribs[i++] = (nint)pb->datas[p].chunk->offset;
                            _attribs[i++] = (nint)EGL.DmaBufAttribs[p, 2];
                            _attribs[i++] = pb->datas[p].chunk->stride;
                            _attribs[i++] = (nint)EGL.DmaBufAttribs[p, 3];
                            _attribs[i++] = (nint)(info->raw.modifier & 0xFFFFFFFF);
                            _attribs[i++] = (nint)EGL.DmaBufAttribs[p, 4];
                            _attribs[i++] = (nint)(info->raw.modifier >> 32);
                        }

                        _attribs[i] = (nint)EglEnum.None;
                        break;
                    }
                case spa_data_type.SPA_DATA_MemFd:
                    {
                        _attribs[0] = (nint)spa_data_type.SPA_DATA_MemFd;
                        _attribs[1] = (nint)pb->datas[0].fd;
                        _attribs[2] = (nint)pb->datas[0].chunk->size;
                        _attribs[3] = (nint)pb->datas[0].chunk->offset;
                        break;
                    }
                case spa_data_type.SPA_DATA_MemPtr:
                    {
                        _attribs[0] = (nint)spa_data_type.SPA_DATA_MemPtr;
                        _attribs[1] = pb->datas[0].data;
                        break;
                    }
            }
        }
    }

    public void Dispose()
    {
        wlxpw_destroy(_handle);
    }

    [DllImport("libwlxpw.so", CallingConvention = CallingConvention.Cdecl)]
    private static extern IntPtr pw_get_library_version();

    [DllImport("libwlxpw.so", CallingConvention = CallingConvention.Cdecl)]
    private static extern nint wlxpw_initialize(string name, uint nodeId, uint fps, IntPtr captureFormats, IntPtr onFrame);

    [DllImport("libwlxpw.so", CallingConvention = CallingConvention.Cdecl)]
    private static extern void wlxpw_set_active(nint handle, uint active);

    [DllImport("libwlxpw.so", CallingConvention = CallingConvention.Cdecl)]
    private static extern void wlxpw_destroy(nint handle);

    private unsafe delegate void OnFrameDelegate(spa_buffer* pb, spa_video_info* info);

    private unsafe struct format_collection
    {
        public int num_formats;
        public capture_format* formats;
    }

    private unsafe struct capture_format
    {
        public uint format;
        public int num_modifiers;
        public ulong* modifiers;
    }
}