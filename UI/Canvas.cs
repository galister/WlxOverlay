using X11Overlay.GFX;
using X11Overlay.Numerics;
using X11Overlay.Overlays;

namespace X11Overlay.UI;

/// <summary>
/// Renders Controls onto a texture
/// </summary>
public class Canvas : IDisposable
{
    // These will be inherited by new controls upon creation.
    public static Vector3 CurrentBgColor;
    public static Vector3 CurrentFgColor;
    public static Font? CurrentFont;
    
    public readonly uint Width;
    public readonly uint Height;
    
    private ITexture _texture = null!;
    private readonly List<Control> _controls = new();
    private readonly List<ButtonBase?> _buttons = new() { null };
    
    private const int ResDivider = 4;
    private byte[,] _uvToButtonMap;
    private readonly int[] _litButtons = new int[2];
    
    private bool _dirty = true;

    public Canvas(uint width, uint height)
    {
        Width = width;
        Height = height;
    }

    public ITexture Initialize()
    {
        _texture = GraphicsEngine.Instance.EmptyTexture(Width, Height, GraphicsFormat.RGB8, true);
        return _texture;
    }

    public void AddControl(Control c)
    {
        c.SetCanvas(this);
        _controls.Add(c);
        if (c is ButtonBase b)
            _buttons.Add(b);
    }

    public void RemoveControl(Control c)
    {
        _controls.Remove(c);
        if (c is ButtonBase b)
            _buttons.Remove(b);
    }

    public void MarkDirty()
    {
        _dirty = true;
    }

    public void BuildInteractiveLayer()
    {
        if (_buttons.Count > 1)
        {
            _uvToButtonMap = new byte[Width / ResDivider, Height / ResDivider];
            
            var maxIdxX = _uvToButtonMap.GetLength(0) - 1;
            var maxIdxY = _uvToButtonMap.GetLength(1) - 1;

            for (byte b = 1; b < _buttons.Count; b++)
            {
                var button = _buttons[b]!;
                
                var xMin = Mathf.Max(0, button.X / ResDivider);
                var yMin = Mathf.Max(0, button.Y / ResDivider);
                var xMax = Mathf.Min(maxIdxX, xMin + button.Width / ResDivider);
                var yMax = Mathf.Min(maxIdxY, yMin + button.Height / ResDivider);
                
                for (var i = xMin; i < xMax; i++)
                for (var j = yMin; j < yMax; j++)
                    try
                    {
                        _uvToButtonMap[i, j] = b;
                    }
                    catch (IndexOutOfRangeException)
                    {
                        Console.WriteLine($"Index ouf of range: ({i}, {j}). xywh: ({button.X}, {button.Y}, {button.Width}, {button.Height}) xyXY: ({xMin}, {yMin}, {xMax}, {yMax}), size: ({_uvToButtonMap.GetLength(0)}, {_uvToButtonMap.GetLength(1)})");
                        throw;
                    }
            }
        }
    }

    public void OnPointerHover(Vector2 uv, LeftRight hand)
    {
        if (!IdxFromUv(uv, out var idx)
            || !ButtonFromIdx(idx, out var button))
            return;

        var hint = (int) hand;
            
        var litIdx = _litButtons[hint];
        if (litIdx == idx)
            return;
            
        if (ButtonFromIdx(litIdx, out var otherBtn))
            otherBtn.OnPointerExit();

        _litButtons[hint] = idx;
        button.OnPointerEnter();
    }

    public void OnPointerDown(Vector2 uv, LeftRight hand)
    {
        if (!IdxFromUv(uv, out var idx)
            || !ButtonFromIdx(idx, out var button))
            return;
                
        var hint = (int) hand;
        _litButtons[hint] = idx;

        button.OnPointerDown();
    }

    public void OnPointerUp(LeftRight hand)
    {
        if (!IdxFromHand(hand, out var idx) 
            || !ButtonFromIdx(idx, out var button)) return;   
            
        button.OnPointerUp();
    }
    
    public void Render()
    {
        foreach (var control in _controls) 
            control.Update();
        
        if (!_dirty)
            return;
        
        GraphicsEngine.UiRenderer.Begin(_texture);
        GraphicsEngine.UiRenderer.Clear();

        foreach (var control in _controls) 
            control.Render();

        GraphicsEngine.UiRenderer.End();
        _dirty = false;
    }

    public void Dispose()
    {
        _texture.Dispose();
    }
    
    private bool IdxFromHand(LeftRight hand, out int idx)
    {
        idx = _litButtons[(int) hand];
        return idx != 0;
    }

    private bool ButtonFromIdx(int idx, out ButtonBase button)
    {
        button = _buttons[idx]!;
        return button != null!;
    }
        
    private bool IdxFromUv(Vector2 uv, out int idx)
    {
        var i = (int)((_uvToButtonMap.GetLength(0) - 1) * uv.x);
        var j = (int)((_uvToButtonMap.GetLength(1) - 1) * uv.y);

        try
        {
            idx = _uvToButtonMap[i, j];
            return true;
        }
        catch (IndexOutOfRangeException)
        {
            Console.WriteLine($"Index out of range on uvMap: ({i},{j}), uv: {uv}, size: ({_uvToButtonMap.GetLength(0)}, {_uvToButtonMap.GetLength(1)})");
        }

        idx = 0;
        return false;
    }

    public void OnPointerLeft(LeftRight hand)
    {
        if (IdxFromHand(hand, out var btnIdx) 
            && ButtonFromIdx(btnIdx, out var button))
            button.OnPointerExit();
    }
}