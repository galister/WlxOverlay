namespace X11Overlay.GFX;

public interface ITexture : IDisposable
{
    public void LoadRawPixels(IntPtr ptr, GraphicsFormat graphicsFormat);
    public IntPtr GetNativeTexturePtr();

    public uint GetWidth();
    public uint GetHeight();

    public bool IsDynamic();
}