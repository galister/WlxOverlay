namespace WlxOverlay.GFX;

public interface ITexture : IDisposable
{
    public void LoadRawImage(IntPtr ptr, GraphicsFormat graphicsFormat, uint newWidth = 0, uint newHeight = 0);

    public void LoadRawSubImage(IntPtr ptr, GraphicsFormat graphicsFormat, int xOffset, int yOffset, int width, int height);

    public void CopyTo(ITexture target, uint width = 0, uint height = 0, int srcX = 0, int srcY = 0, int dstX = 0, int dstY = 0);

    public IntPtr GetNativeTexturePtr();

    public uint GetWidth();
    public uint GetHeight();

    public bool IsDynamic();
}