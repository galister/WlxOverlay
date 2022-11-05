using X11Overlay.GFX;

namespace X11Overlay;

public interface ITexture : IDisposable
{
    public void LoadRawPixels(IntPtr ptr, GraphicsFormat graphicsFormat);
    public IntPtr GetNativeTexturePtr();

    public uint GetWidth();
    public uint GetHeight();

    public bool IsDynamic();
}