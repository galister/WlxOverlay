using X11Overlay.Core;
using X11Overlay.Screen.Interop;
using X11Overlay.Types;

namespace X11Overlay.Overlays;

/// <summary>
/// An overlay that displays an X11 screen, moves the mouse and sends mouse events.
/// </summary>
public class ScreenOverlay : GrabbableOverlay
{
    private readonly int _screen;
    private XScreenCapture? _capture;

    private DateTime _freezeCursor = DateTime.MinValue;
    
    public ScreenOverlay(int screen) : base($"Screen{screen}")
    {
        WidthInMeters = 1;
        _screen = screen;
    }

    public override void Initialize()
    {
        var hmd = InputManager.HmdTransform;
        var centerPoint = hmd.TranslatedLocal(Vector3.Forward);

        LocalScale = new Vector3(2, -2, 2);

        Transform = hmd.LookingAt(centerPoint.origin, hmd.basis.y * hmd.basis.Inverse()).ScaledLocal(LocalScale);
        Transform.origin = centerPoint.origin;

        _capture = new XScreenCapture(_screen);
        Texture = _capture.texture;

        //Curvature = 0.25f;

        //myFont = new Font("FreeSans", 96);

        UpdateInteractionTransform();
        UploadCurvature();
        
        base.Initialize();
    }

    protected internal override void Render()
    {
        if (_capture == null)
            return;
        
        _capture.Tick();
            
        var mouse = _capture.GetMousePosition();

        var w = Texture!.GetWidth();
        var h = Texture!.GetHeight();
        
        if (mouse.X >= 0 && mouse.X < w
                         && mouse.Y >= 0 && mouse.Y < h)
        {
            var uv = new Vector2(mouse.X / (float)w, mouse.Y / (float)h);

            var (origin, center) = CurvedSurfacePositionFromUv(uv);
            DesktopCursor.Instance.MoveTo(origin, center);
        }

        base.Render();
    }

    
    protected internal override void OnPointerHover(PointerHit hitData)
    {
        if (PrimaryPointer == hitData.pointer && _freezeCursor < DateTime.UtcNow)
        {
            var adjustedUv = InvInteractionTransform * hitData.uv;
            adjustedUv.y = 1 - adjustedUv.y;
            _capture?.MoveMouse(adjustedUv);
            
        }
    }

    protected internal override void OnPointerDown(PointerHit hitData)
    {
        base.OnPointerDown(hitData);
        _freezeCursor = DateTime.UtcNow + TimeSpan.FromMilliseconds(200);
        SendMouse(hitData, true);
    }

    protected internal override void OnPointerUp(PointerHit hitData)
    {
        SendMouse(hitData, false);
        base.OnPointerUp(hitData);
    }

    private void SendMouse(PointerHit hitData, bool pressed)
    {
        var click = hitData.modifier switch
        {
            PointerMode.Alt => XcbMouseButton.Right,
            PointerMode.Alt2 => XcbMouseButton.Middle,
            _ => XcbMouseButton.Left
        };

        _capture?.SendMouse(hitData.uv, click, pressed);
    }

    private DateTime _nextScroll = DateTime.MinValue;
    protected internal override void OnScroll(PointerHit hitData, float value)
    {
        base.OnScroll(hitData, value);

        if (_nextScroll > DateTime.UtcNow)
            return;


        if (hitData.modifier == PointerMode.Alt2)
        {
            // super fast scroll, 1 click per frame
        }
        else
        {
            var millis = hitData.modifier == PointerMode.Alt ? 50 : 100;
            _nextScroll = DateTime.UtcNow.AddMilliseconds((1 - Mathf.Abs(value)) * millis);
        }

        if (value < 0)
        {
            _capture?.SendMouse(hitData.uv, XcbMouseButton.WheelDown, true);
            _capture?.SendMouse(hitData.uv, XcbMouseButton.WheelDown, false);
        }
        else
        {
            _capture?.SendMouse(hitData.uv, XcbMouseButton.WheelUp, true);
            _capture?.SendMouse(hitData.uv, XcbMouseButton.WheelUp, false);
        }
    }

    protected internal override void OnClickWhileHeld()
    {
        // TODO high quality overlays
        
        base.OnClickWhileHeld();
    }

    protected internal override void OnAltClickWhileHeld()
    {
        // TODO toggle curvature
        
        base.OnAltClickWhileHeld();
    }
}