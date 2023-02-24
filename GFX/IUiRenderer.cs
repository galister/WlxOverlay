using WlxOverlay.Numerics;

namespace WlxOverlay.GFX;

public interface IUiRenderer
{
    /// <summary>
    /// Begin rendering to this texture.
    /// </summary>
    public void Begin(ITexture texture);

    /// <summary>
    /// Call after rendering of the texture is done. 
    /// </summary>
    public void End();

    /// <summary>
    /// Clear the color buffer
    /// </summary>
    public void Clear();

    /// <summary>
    /// Draw a sprite at the specified bounds
    /// </summary>
    public void DrawSprite(ITexture sprite, int x, int y, uint w, uint h);

    /// <summary>
    /// Draw a font bitmap (r8 format) tinted by <i>color</i> at the specified bounds.
    /// </summary>
    public void DrawFont(Glyph glyph, Vector3 color, int x, int y, uint w, uint h);

    /// <summary>
    /// Fill the specified bounds with a solid color
    /// </summary>
    public void DrawColor(Vector3 color, int x, int y, uint w, uint h);
}