using System.Runtime.InteropServices;
using X11Overlay.GFX;
using X11Overlay.Types;

namespace X11Overlay.Screen.Interop
{
    public class XScreenCapture : IDisposable
    {
        private readonly IntPtr xShmHandle;
        private readonly uint maxBytes;
        private uint width, height;
        public ITexture? texture { get; }

        public XScreenCapture(int screen)
        {
            (width, height) = GetScreenSize(screen);
            maxBytes = 4U * width * height;
            if (maxBytes == 0)
                return;

            xShmHandle = xshm_cap_start(screen);
            
            texture = GraphicsEngine.Instance.EmptyTexture(width, height, GraphicsFormat.RGB8, true);
        }

        public void Tick()
        {
            if (xShmHandle == IntPtr.Zero) return;
            
            var bytes = (int) xshm_grab_bgra32(xShmHandle);
            if (bytes != maxBytes)
            {
                Console.WriteLine($"Unexpected buffer size: {bytes}");
                return;
            }

            var pixBuf = xshm_pixel_buffer(xShmHandle);
                
            if (pixBuf == IntPtr.Zero)
            {
                Console.WriteLine($"Could not get pixel buffer!");
                return;
            }
            
            texture!.LoadRawPixels(pixBuf, GraphicsFormat.BGRA8);
        }

        public void Dispose()
        {
            if (xShmHandle != IntPtr.Zero)
                xshm_cap_end(xShmHandle);
            
            texture?.Dispose();
        }

        public void MoveMouse(Vector2 uv)
        {
            if (xShmHandle == IntPtr.Zero)
                return;

            var to = MouseCoordinatesFromUv(uv);
            xshm_mouse_move(xShmHandle, to.x, to.y);
        }

        public void SendMouse(Vector2 uv, XcbMouseButton button, bool pressed)
        {
            if (xShmHandle == IntPtr.Zero)
                return;
            
            var to = MouseCoordinatesFromUv(uv);
            //Debug.Log($"Mouse: {button} {(pressed ? "down" : "up")} at {to}");
            xshm_mouse_event(xShmHandle, to.x, to.y, (byte) button, pressed ? 1 : 0);
        }

        public static void SendKey(int keyCode, bool pressed)
        {
            xshm_keybd_event(IntPtr.Zero, (byte) keyCode, pressed ? 1 : 0);
        }
        
        public Vector2Int GetMousePosition()
        {
            var vec = new Vector2Int();
            xshm_mouse_position(xShmHandle, ref vec);
            return vec;
        }

        public static Int32 NumScreens()
        {
            return xshm_num_screens();
        }

        private (short x, short y) MouseCoordinatesFromUv(Vector2 uv)
        {
            var x = (short)(uv.x * width);
            var y = (short)((1 - uv.y) * height);
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

        [DllImport("libxshm_cap.so")]
        private static extern void xshm_keybd_event(IntPtr xhsm_instance, Byte keycode, Int32 pressed);
    }
}
