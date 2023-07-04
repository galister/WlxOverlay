namespace WlxOverlay.Capture.PipeWire;

public class SpaHook : IDisposable
{
    public IntPtr Ptr { get; }

    public SpaHook()
    {
        var size = Marshal.SizeOf<spa_hook>();
        Ptr = Marshal.AllocHGlobal(size);
        Marshal.StructureToPtr(new spa_hook(), Ptr, false);
    }

    public void Dispose()
    {
        Marshal.FreeHGlobal(Ptr);
    }
}