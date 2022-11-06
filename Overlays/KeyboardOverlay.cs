using X11Overlay.Core;
using X11Overlay.GFX;
using X11Overlay.Numerics;
using X11Overlay.Overlays.Simple;
using X11Overlay.Types;
using X11Overlay.UI;
using Action = System.Action;

namespace X11Overlay.Overlays;

public class KeyboardOverlay : GrabbableOverlay
{
    private const uint ButtonPadding = 4;
    
    private static KeyboardOverlay? _instance;

    private Canvas _canvas;
    
    public KeyboardOverlay() : base("Keyboard")
    {
        if (_instance != null)
            throw new InvalidOperationException("Can't have more than one KeyboardOverlay!");
        _instance = this;

        SpawnPosition = Vector3.Forward + Vector3.Down * 0.5f;
        WidthInMeters = 0.8f;
        ShowHideBinding = true;
        WantVisible = true;

        _canvas = new Canvas(1200, 400);

        Canvas.CurrentBgColor = HexColor.FromRgb("#101010");
        _canvas.AddControl(new Panel(0, 0, _canvas.Width, _canvas.Height));

        
        Canvas.CurrentFont = new Font("LiberationSans-Bold.ttf", 18);
        Canvas.CurrentBgColor = HexColor.FromRgb("#202020");
        
        var layout = KeyboardLayout.Instance;
        
        var unitSize = _canvas.Width / (uint)layout.RowSize;

        var h = unitSize - 2 * ButtonPadding;
        for (var row = 0U; row < layout.KeySizes.Length; row++)
        {
            var y = (int)(_canvas.Height - unitSize * (row + 1) + ButtonPadding);
            var sumPrevSize = 0f;
                
            for (var col = 0U; col < layout.KeySizes[row].Length; col++)
            {
                var mySize = layout.KeySizes[row][col];
                var x = (int)(unitSize * sumPrevSize + ButtonPadding);
                var w = (uint)(unitSize * mySize) - 2 * ButtonPadding;

                var key = new KeyButton(row, col, x, y, w, h);
                _canvas.AddControl(key);
                    
                sumPrevSize += mySize;
            }
        }
        _canvas.BuildInteractiveLayer();
    }

    protected override void Initialize()
    {
        var hmd = InputManager.HmdTransform;
        var centerPoint = hmd.TranslatedLocal(SpawnPosition);
        Transform = hmd.LookingAt(centerPoint.origin, hmd.basis.y * hmd.basis.Inverse());
        Transform.origin = centerPoint.origin;
        
        Texture = _canvas.Initialize();
        
        UpdateInteractionTransform();
        base.Initialize();
    }
    
    protected internal override void Render()
    {
        _canvas.Render();
        
        base.Render();
    }

    protected internal override void OnPointerDown(PointerHit hitData)
    {
        base.OnPointerDown(hitData);
        var action = _canvas.OnPointerDown(hitData.uv, hitData.hand);
        hitData.pointer.ReleaseAction = action;
    }

    protected internal override void OnPointerHover(PointerHit hitData)
    {
        if (hitData.pointer == PrimaryPointer && KeyButton.Mode != (int)hitData.modifier)
        {
            KeyButton.Mode = (int)hitData.modifier;
            _canvas.MarkDirty();
        }
        
        base.OnPointerHover(hitData);
        _canvas.OnPointerHover(hitData.uv, hitData.hand);
    }

    protected internal override void OnPointerLeft(LeftRight hand)
    {
        base.OnPointerLeft(hand);
        _canvas.OnPointerLeft(hand);
    }
}