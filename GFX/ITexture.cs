using X11Overlay.GFX;
using X11Overlay.Numerics;

namespace X11Overlay;

public interface ITexture : IDisposable
{
    public void LoadRawPixels(IntPtr ptr, GraphicsFormat graphicsFormat);
    public IntPtr GetNativeTexturePtr();

    public uint GetWidth();
    public uint GetHeight();
    
    public void Clear(Vector3 color);
    public void Clear(Vector3 color, int xOffset, int yOffset, uint width, uint height);

    public void Draw(ITexture overlay, int xOffset, int yOffset);
    public void Draw(ITexture overlay, int xOffset, int yOffset, uint width, uint height);

    public bool IsDynamic();
}