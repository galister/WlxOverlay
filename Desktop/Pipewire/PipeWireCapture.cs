using Tmds.Linux;
using WlxOverlay.Core;
using WlxOverlay.GFX;
using WlxOverlay.GFX.OpenGL;
using WlxOverlay.Types;

namespace WlxOverlay.Desktop.Pipewire;

public class PipeWireCapture : IDisposable
{
    private readonly uint _nodeId;
    private readonly string _name;
    private readonly uint _width;
    private readonly uint _height;

    private nint _handle;

    private nint _onFrameHandle;
    private OnFrameDelegate _onFrameDelegate;

    private nint _eglImage;
    private nint _lastEglImage;

    private readonly object _attribsLock = new();
    private readonly nint[] _attribs = new nint[47];

    public PipeWireCapture(uint nodeId, string name, uint width, uint height)
    {
        _nodeId = nodeId;
        _name = name;
        _width = width;
        _height = height;
    }


    /// <summary>
    /// Call this from BaseOverlay.Render()
    /// </summary>
    public unsafe void ApplyToTexture(ITexture texture)
    {
        if (texture is not GlTexture glTexture)
            return;

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

                glTexture.LoadEglImage(_eglImage, _width, _height);
            }
            else if (_attribs[0] == (nint)spa_data_type.SPA_DATA_MemPtr)
            {
                var fmt = Config.Instance.WaylandColorSwap
                    ? GraphicsFormat.RGBA8
                    : GraphicsFormat.BGRA8;

                texture.LoadRawImage(_attribs[1], fmt, _width, _height);
            }
            else if (_attribs[0] == (nint)spa_data_type.SPA_DATA_MemFd)
            {
                var fmt = Config.Instance.WaylandColorSwap
                    ? GraphicsFormat.RGBA8
                    : GraphicsFormat.BGRA8;

                var len = (int)_attribs[2];
                var map = LibC.mmap(null, len, LibC.PROT_READ, LibC.MAP_SHARED, (int)_attribs[1], 0);

                texture.LoadRawImage(new IntPtr(map), fmt, _width, _height);

                LibC.munmap(map, len);
            }

            _attribs[0] = IntPtr.Zero;
        }
    }

    public unsafe void InitializeAsync()
    {
        _onFrameDelegate = OnFrame;
        _onFrameHandle = Marshal.GetFunctionPointerForDelegate(_onFrameDelegate);
        _handle = wlxpw_initialize(_name, _nodeId, (int)OverlayManager.Instance.DisplayFrequency, _onFrameHandle);
    }

    private DrmFormat SpaFormatToFourCC(int fmt)
    {
        switch (fmt)
        {
            case 7:
                return DrmFormat.DRM_FORMAT_ARGB8888;
            case 8:
                return DrmFormat.DRM_FORMAT_ABGR8888;
        }
        throw new ArgumentException($"Unknown value: {fmt}", nameof(fmt));
    }

    private unsafe void OnFrame(spa_buffer* pb, spa_video_info* info)
    {
        switch (pb->datas[0].type)
        {
            case spa_data_type.SPA_DATA_DmaBuf:
                {
                    var planes = pb->n_datas;

                    var i = 0;
                    _attribs[i++] = (nint)EglEnum.Width;
                    _attribs[i++] = (nint)_width;
                    _attribs[i++] = (nint)EglEnum.Height;
                    _attribs[i++] = (nint)_height;
                    _attribs[i++] = (nint)EglEnum.LinuxDrmFourccExt;
                    _attribs[i++] = (nint)SpaFormatToFourCC(info->raw.format);

                    for (var p = 0U; p < planes; p++)
                    {
                        _attribs[i++] = (nint)EGL.DmaBufAttribs[p, 0];
                        _attribs[i++] = (nint)pb->datas[p].fd;
                        _attribs[i++] = (nint)EGL.DmaBufAttribs[p, 1];
                        _attribs[i++] = (nint)pb->datas[p].chunk->offset;
                        _attribs[i++] = (nint)EGL.DmaBufAttribs[p, 2];
                        _attribs[i++] = (nint)pb->datas[p].chunk->stride;
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

    public void Dispose()
    {
        wlxpw_destroy(_handle);
    }

    [DllImport("libwlxpw.so", CallingConvention = CallingConvention.Cdecl)]
    private static extern nint wlxpw_initialize(string name, uint nodeId, int hz, IntPtr onFrame);

    [DllImport("libwlxpw.so", CallingConvention = CallingConvention.Cdecl)]
    private static extern void wlxpw_destroy(nint handle);

    private unsafe delegate void OnFrameDelegate(spa_buffer* pb, spa_video_info* info);
}