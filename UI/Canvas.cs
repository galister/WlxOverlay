using X11Overlay.GFX;
using X11Overlay.Types;

namespace X11Overlay.UI;

/// <summary>
/// Renders Controls onto a texture
/// </summary>
public class Canvas : IDisposable
{
    // These will be inherited by new controls upon creation.
    public static Vector3 CurrentBgColor;
    public static Vector3 CurrentBgColorHover;
    public static Vector3 CurrentBgColorClick;
    public static Vector3 CurrentBgColorInactive;
    public static Vector3 CurrentFgColor;
    public static Vector3 CurrentFgColorInactive;
    public static Font? CurrentFont;
    
    private ITexture _texture;
    public readonly List<Control> Controls = new();

    public Canvas(uint width, uint height)
    {
        _texture = GraphicsEngine.Instance.EmptyTexture(width, height, GraphicsFormat.RGB8, true);
    }

    public void OnHover()
    {
        
    }

    public void OnClick()
    {
        
    }
    
    public void Render()
    {
        
    }

    public ITexture GetTexture() => _texture;

    public void Dispose()
    {
        _texture.Dispose();
    }
}