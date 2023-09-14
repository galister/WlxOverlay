using System.Diagnostics.CodeAnalysis;
using WlxOverlay.Desktop;
using WlxOverlay.GFX;
using WlxOverlay.Numerics;
using WlxOverlay.Types;

namespace WlxOverlay.Capture;

public class XshmCapture : IDesktopCapture
{
    private static ITexture? _mouseTex;
    private static Vector2Int _mousePos;
    private static bool _mousePosSet;

    public static int NumScreens()
    {
        return wlxshm_num_screens();
    }

    private readonly BaseOutput _screen;
    private readonly IntPtr _handle;
    private readonly uint _bufSize;

    private bool _running;

    public XshmCapture(BaseOutput output)
    {
        Vector2Int size = new(), pos = new();

        _handle = wlxshm_create((int)output.IdName, ref size, ref pos);
        output.Size = size;
        output.Position = pos;
        output.RecalculateTransform();
        _screen = output;

        if (_handle == IntPtr.Zero)
            throw new ApplicationException("Could not initialize Xorg screen capture!");

        _bufSize = (uint)(size.X * size.Y * 4U);
    }

    public void Initialize()
    {
        _mouseTex ??= GraphicsEngine.Instance.TextureFromFile(
            Path.Combine(Config.ResourcesFolder, "arrow.png"));

        wlxshm_capture_start(_handle);
        _running = true;
    }

    public unsafe bool TryApplyToTexture(ITexture texture)
    {
        var retVal = false;
        var buf = wlxshm_capture_frame(_handle);
        if (buf != null && buf->length == _bufSize)
        {
            texture.LoadRawImage(buf->buffer, GraphicsFormat.BGRA8);
            retVal = true;
        }

        if (!_mousePosSet)
        {
            wlxshm_mouse_pos_global(_handle, ref _mousePos);
            _mousePosSet = true;
        }

        var mouse = new Vector2Int(_mousePos.X - _screen.Position.X, _mousePos.Y - _screen.Position.Y);

        if (mouse.X >= 0 && mouse.X < _screen.Size.X && mouse.Y >= 0 && mouse.Y < _screen.Size.Y)
        {
            var w = _mouseTex!.GetWidth() * (_screen.Size.X / 4096f);
            var h = _mouseTex.GetHeight() * (_screen.Size.X / 4096f);
            var x = mouse.X - w * 0.5f;
            var y = mouse.Y - h * 0.5f;

            GraphicsEngine.Renderer.Begin(texture);
            GraphicsEngine.Renderer.DrawSprite(_mouseTex, x, y, w, h);
            GraphicsEngine.Renderer.End();
        }
        return retVal;
    }

    public static void ResetMouse()
    {
        _mousePosSet = false;
    }

    public void Pause()
    {
        if (!_running)
            return;
        wlxshm_capture_end(_handle);
        _running = false;
    }

    public void Resume()
    {
        wlxshm_capture_start(_handle);
        _running = true;
    }

    public void Dispose()
    {
        Pause();
        wlxshm_destroy(_handle);
    }

    [DllImport("libwlxshm.so", CallingConvention = CallingConvention.Cdecl)]
    private static extern IntPtr wlxshm_create(int screen, ref Vector2Int size, ref Vector2Int pos);

    [DllImport("libwlxshm.so", CallingConvention = CallingConvention.Cdecl)]
    private static extern void wlxshm_destroy(IntPtr handle);

    [DllImport("libwlxshm.so", CallingConvention = CallingConvention.Cdecl)]
    private static extern int wlxshm_capture_start(IntPtr handle);

    [DllImport("libwlxshm.so", CallingConvention = CallingConvention.Cdecl)]
    private static extern void wlxshm_capture_end(IntPtr handle);

    [DllImport("libwlxshm.so", CallingConvention = CallingConvention.Cdecl)]
    private static extern unsafe buf_t* wlxshm_capture_frame(IntPtr handle);

    [DllImport("libwlxshm.so", CallingConvention = CallingConvention.Cdecl)]
    private static extern void wlxshm_mouse_pos_global(IntPtr handle, ref Vector2Int pos);

    [DllImport("libwlxshm.so", CallingConvention = CallingConvention.Cdecl)]
    private static extern int wlxshm_num_screens();

    [StructLayout(LayoutKind.Sequential)]
    [SuppressMessage("ReSharper", "FieldCanBeMadeReadOnly.Local")]
    private struct buf_t
    {
        public int length;
        public IntPtr buffer;
    }
}