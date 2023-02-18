using Valve.VR;

namespace X11Overlay.GFX;

public static class GraphicsEngine
{
    public static IGraphicsEngine Instance = null!;
    public static IUiRenderer UiRenderer = null!;
}

public interface IGraphicsEngine
{
    public ITexture TextureFromFile(string path, GraphicsFormat internalFormat = GraphicsFormat.RGBA8);

    public ITexture EmptyTexture(uint width, uint height,
        GraphicsFormat internalFormat = GraphicsFormat.RGBA8, bool dynamic = false);

    public ITexture TextureFromRaw(uint width, uint height, GraphicsFormat inputFormat, IntPtr data,
        GraphicsFormat internalFormat = GraphicsFormat.RGBA8);

    public ITexture TextureFromRaw(uint width, uint height, GraphicsFormat inputFormat, Span<byte> data,
        GraphicsFormat internalFormat = GraphicsFormat.RGBA8);

    public ETextureType GetTextureType();

    public void Shutdown();

}