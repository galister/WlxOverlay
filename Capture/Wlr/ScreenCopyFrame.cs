using Tmds.Linux;
using WaylandSharp;
using WlxOverlay.GFX;
using WlxOverlay.Types;
using static Tmds.Linux.LibC;

namespace WlxOverlay.Capture.Wlr
{
    internal class ScreenCopyFrame : IWlrFrame

    {
        private readonly WlShm _shm;
        private readonly ZwlrScreencopyFrameV1 _frame;
        private readonly string _shmPath;

        private uint _width;
        private uint _height;
        private uint _size;
        private int _fd;
        private WlShmPool? _pool;
        private WlBuffer? _buffer;

        private bool _disposed;

        private CaptureStatus _status;

        public ScreenCopyFrame(WlrCaptureData data)
        {
            _shmPath = $"/wlxoverlay-screencopy-{data.Output!.GetId()}";
            _frame = data.ScreencopyManager!.CaptureOutput(1, data.Output!);
            _frame.Buffer += OnBuffer;
            _frame.Ready += OnReady;
            _frame.Failed += OnFailed;
            _shm = data.Shm!;
        }

        public CaptureStatus GetStatus() => _status;

        public unsafe void ApplyToTexture(ITexture texture)
        {
            var ptr = mmap((void*)0, _size, 0x01, 0x01, _fd, 0);
            var fmt = Config.Instance.WaylandColorSwap
                ? GraphicsFormat.RGBA8
                : GraphicsFormat.BGRA8;

            texture.LoadRawImage(new IntPtr(ptr), fmt, _width, _height);
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

            _fd = shm_open(_shmPath, O_CREAT | O_RDWR, S_IRUSR | S_IWUSR);
            shm_unlink(_shmPath);
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
