using System.Runtime.InteropServices;
using WlxOverlay.GFX;
using WlxOverlay.Numerics;

namespace WlxOverlay.Desktop.X11
{
    public class XScreenCapture : IDisposable
    {
        private readonly int _screen;
        private readonly uint _maxBytes, _width, _height;

        private IntPtr _xShmHandle;
        public readonly ITexture? Texture;

        public XScreenCapture(int screen)
        {
            _screen = screen;
            (_width, _height) = GetScreenSize(screen);
            _maxBytes = 4U * _width * _height;
            if (_maxBytes == 0)
                return;
            Texture = GraphicsEngine.Instance.EmptyTexture(_width, _height, GraphicsFormat.RGB8, true);
        }

        public void Suspend()
        {
            if (_xShmHandle != IntPtr.Zero)
                xshm_cap_end(_xShmHandle);
            _xShmHandle = IntPtr.Zero;
        }

        public void Resume()
        {
            _xShmHandle = xshm_cap_start(_screen);
        }

        public bool Running()
        {
            return _xShmHandle != IntPtr.Zero;
        }

        public void Tick()
        {
            if (_xShmHandle == IntPtr.Zero) return;

            var bytes = (int)xshm_grab_bgra32(_xShmHandle);
            if (bytes != _maxBytes)
            {
                Console.WriteLine($"Unexpected buffer size: {bytes}");
                return;
            }

            var pixBuf = xshm_pixel_buffer(_xShmHandle);

            if (pixBuf == IntPtr.Zero)
            {
                Console.WriteLine($"Could not get pixel buffer!");
                return;
            }

            Texture!.LoadRawImage(pixBuf, GraphicsFormat.BGRA8);
        }

        public void Dispose()
        {
            if (_xShmHandle != IntPtr.Zero)
                xshm_cap_end(_xShmHandle);

            Texture?.Dispose();
        }

        public void MoveMouse(Vector2 uv)
        {
            if (_xShmHandle == IntPtr.Zero)
                return;

            var to = MouseCoordinatesFromUv(uv);
            xshm_mouse_move(_xShmHandle, to.x, to.y);
        }

        public void SendMouse(Vector2 uv, XcbMouseButton button, bool pressed)
        {
            if (_xShmHandle == IntPtr.Zero)
                return;

            var to = MouseCoordinatesFromUv(uv);
            //Debug.Log($"Mouse: {button} {(pressed ? "down" : "up")} at {to}");
            xshm_mouse_event(_xShmHandle, to.x, to.y, (byte)button, pressed ? 1 : 0);
        }

        public Vector2Int GetMousePosition()
        {
            var vec = new Vector2Int();
            xshm_mouse_position(_xShmHandle, ref vec);
            return vec;
        }

        public static Int32 NumScreens()
        {
            return xshm_num_screens();
        }

        private (short x, short y) MouseCoordinatesFromUv(Vector2 uv)
        {
            var x = (short)(uv.x * _width);
            var y = (short)((1 - uv.y) * _height);
            return (x, y);
        }

        public static (uint w, uint h) GetScreenSize(int screen)
        {
            var vec = new Vector2Int();
            xshm_screen_size(screen, ref vec);
            Console.WriteLine($"Screen {screen} is {vec.X}x{vec.Y}.");
            return ((uint)vec.X, (uint)vec.Y);
        }

        // ReSharper disable IdentifierTypo
        // ReSharper disable StringLiteralTypo
        // ReSharper disable InconsistentNaming
        // ReSharper disable BuiltInTypeReferenceStyle
        [DllImport("libxshm_cap.so")]
        private static extern IntPtr xshm_cap_start(Int32 screen_id);
        [DllImport("libxshm_cap.so")]
        private static extern void xshm_cap_end(IntPtr xhsm_instance);

        [DllImport("libxshm_cap.so")]
        private static extern Int32 xshm_num_screens();

        [DllImport("libxshm_cap.so")]
        private static extern void xshm_screen_size(Int32 screen, ref Vector2Int vec);

        [DllImport("libxshm_cap.so")]
        private static extern void xshm_mouse_position(IntPtr xhsm_instance, ref Vector2Int vec);

        [DllImport("libxshm_cap.so")]
        private static extern IntPtr xshm_pixel_buffer(IntPtr xhsm_instance);

        [DllImport("libxshm_cap.so")]
        private static extern UInt64 xshm_grab_bgra32(IntPtr xhsm_instance);

        [DllImport("libxshm_cap.so")]
        private static extern void xshm_mouse_move(IntPtr xhsm_instance, Int16 x, Int16 y);

        [DllImport("libxshm_cap.so")]
        private static extern void xshm_mouse_event(IntPtr xhsm_instance, Int16 x, Int16 y, Byte button, Int32 pressed);
    }
}
