using System.Runtime.InteropServices;
using Tmds.Linux;
using WaylandSharp;
using X11Overlay.GFX;
using static Tmds.Linux.LibC;

namespace X11Overlay.Desktop.Wayland.Frame
{
    public class ScreenCopyFrame : IWaylandFrame
    {
        private readonly WlShm _shm;
        private readonly ZwlrScreencopyFrameV1 _frame;

        private uint _width;
        private uint _height;
        private uint _size;
        private int _fd;
        private WlShmPool? _pool;
        private WlBuffer? _buffer;

        private bool _disposed;

        private CaptureStatus _status;

        public ScreenCopyFrame(WlOutput output, ZwlrScreencopyManagerV1 screencopyManager, WlShm shm)
        {
            _frame = screencopyManager.CaptureOutput(1, output);
            _frame.Buffer += OnBuffer;
            _frame.Ready += OnReady;
            _frame.Failed += OnFailed;
            _shm = shm;
        }

        public CaptureStatus GetStatus() => _status;

        public unsafe void ApplyToTexture(ITexture texture)
        {
            var ptr = mmap((void *)0, _size, 0x01, 0x01, _fd, 0);
            texture.LoadRawImage(new IntPtr(ptr), GraphicsFormat.BGRA8, _width, _height);
            munmap(ptr, _size);
        }

        private void OnFailed(object? sender, ZwlrScreencopyFrameV1.FailedEventArgs e)
        {
            _status = CaptureStatus.FrameSkipped;
        }

        private void OnReady(object? sender, ZwlrScreencopyFrameV1.ReadyEventArgs e)
        {
            _status = CaptureStatus.FrameReady;
        }

        private void OnBuffer(object? sender, ZwlrScreencopyFrameV1.BufferEventArgs e)
        {
            _width = e.Width;
            _height = e.Height;
            _size = e.Stride * e.Height;

            _fd = shm_open("/x11overlay-screencopy", O_CREAT | O_RDWR, S_IRUSR | S_IWUSR);
            shm_unlink("/x11overlay-screencopy");
            ftruncate(_fd, _size);

            _pool = _shm.CreatePool(_fd, (int)_size);
            _buffer = _pool.CreateBuffer(0, (int)e.Width, (int)e.Height, (int)e.Stride, e.Format);
            _frame.Copy(_buffer!);
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _buffer?.Destroy();
            _pool?.Destroy();
            if (_fd != 0) close(_fd);
            _frame.Dispose();
            _disposed = true;
        }
        
        [DllImport("libc")]
        private static extern int shm_open([MarshalAs(UnmanagedType.LPStr)] string name, int oFlags, mode_t mode);

        [DllImport("libc")]
        private static extern int shm_unlink([MarshalAs(UnmanagedType.LPStr)] string name);
    }
}
