using X11Overlay.Core;
using X11Overlay.Numerics;
using X11Overlay.Overlays.Simple;
using X11Overlay.Screen.Interop;

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

    protected override void Initialize()
    {
        var hmd = InputManager.HmdTransform;
        var centerPoint = hmd.TranslatedLocal(SpawnPosition);

        LocalScale = new Vector3(2, -2, 2);
        CurveWhenUpright = true;

        Transform = hmd.LookingAt(centerPoint.origin, hmd.basis.y * hmd.basis.Inverse()).ScaledLocal(LocalScale);
        Transform.origin = centerPoint.origin;

        _capture = new XScreenCapture(_screen);
        Texture = _capture.Texture;

        UpdateInteractionTransform();
        UploadCurvature();
        
        base.Initialize();
    }

    public override void Show()
    {
        base.Show();
        _capture?.Resume();
    }

    public override void Hide()
    {
        base.Hide();
        _capture?.Suspend();
    }


    protected internal override void Render()
    {
        if (_capture == null || !_capture.Running())
            return;
        
        _capture.Tick();
            
        var mouse = _capture.GetMousePosition();

        var w = Texture!.GetWidth();
        var h = Texture!.GetHeight();
        
        if (mouse.X >= 0 && mouse.X < w
                         && mouse.Y >= 0 && mouse.Y < h)
        {
            var uv = new Vector2(mouse.X / (float)w, mouse.Y / (float)h);

            var moveToTransform = CurvedSurfaceTransformFromUv(uv);
            DesktopCursor.Instance.MoveTo(moveToTransform);
        }

        base.Render();
    }

    
    protected internal override void OnPointerHover(PointerHit hitData)
    {
        base.OnPointerHover(hitData);
        if (PrimaryPointer == hitData.pointer && _freezeCursor < DateTime.UtcNow)
        {
            var adjustedUv = hitData.uv;
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
            PointerMode.Shift => XcbMouseButton.Right,
            PointerMode.Alt => XcbMouseButton.Middle,
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


        if (hitData.modifier == PointerMode.Alt)
        {
            // super fast scroll, 1 click per frame
        }
        else
        {
            var millis = hitData.modifier == PointerMode.Shift ? 50 : 100;
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
        SnapUpright = !SnapUpright;
        
        base.OnClickWhileHeld();
    }

    protected internal override void OnAltClickWhileHeld()
    { 
        // TODO high quality overlays
        
        base.OnAltClickWhileHeld();
    }
}