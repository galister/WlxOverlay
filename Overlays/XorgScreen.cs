using X11Overlay.Desktop.X11;
using X11Overlay.Numerics;
using X11Overlay.Overlays.Simple;
using X11Overlay.Types;

namespace X11Overlay.Overlays;

public class XorgScreen : BaseScreen<int>
{
    private XScreenCapture? _capture;
    private DateTime _freezeCursor = DateTime.MinValue;

    private static bool _mouseMoved;

    public XorgScreen(int screen) : base(screen)
    {
    }

    protected override void Initialize()
    {
        base.Initialize();

        _capture = new XScreenCapture(Screen);
        Texture = _capture.Texture;

        UpdateInteractionTransform();
        UploadCurvature();
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
            if (Config.Instance.FallbackCursors)
            {
                DrawFallbackCross(mouse.X, mouse.Y, Vector3.One, 8);
                DrawFallbackCross(mouse.X + 1, mouse.Y + 1, Vector3.Zero, 8);
            }
            else
            {
                var uv = new Vector2(mouse.X / (float)w, mouse.Y / (float)h);
                var moveToTransform = CurvedSurfaceTransformFromUv(uv);
                DesktopCursor.Instance.MoveTo(moveToTransform);
            }
        }

        base.Render();

        _mouseMoved = false;
    }

    protected internal override void OnPointerHover(PointerHit hitData)
    {
        base.OnPointerHover(hitData);
        if (PrimaryPointer == hitData.pointer && !_mouseMoved && _freezeCursor < DateTime.UtcNow)
            MoveMouse(hitData);
    }

    protected internal override void OnPointerDown(PointerHit hitData)
    {
        if (PrimaryPointer != hitData.pointer)
            MoveMouse(hitData);

        base.OnPointerDown(hitData);
        _freezeCursor = DateTime.UtcNow + TimeSpan.FromSeconds(Config.Instance.ClickFreezeTime);
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
            PointerMode.Right => XcbMouseButton.Right,
            PointerMode.Middle => XcbMouseButton.Middle,
            _ => XcbMouseButton.Left
        };

        _capture?.SendMouse(hitData.uv, click, pressed);
    }

    protected override bool MoveMouse(PointerHit hitData)
    {
        var adjustedUv = hitData.uv;
        adjustedUv.y = 1 - adjustedUv.y;
        _capture?.MoveMouse(adjustedUv);
        return true;
    }

    private DateTime _nextScroll = DateTime.MinValue;
    protected internal override void OnScroll(PointerHit hitData, float value)
    {
        base.OnScroll(hitData, value);

        if (_nextScroll > DateTime.UtcNow)
            return;


        if (hitData.modifier == PointerMode.Middle)
        {
            // super fast scroll, 1 click per frame
        }
        else
        {
            var millis = hitData.modifier == PointerMode.Right ? 50 : 100;
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

    public override string ToString()
    {
        return $"Scr {Screen}";
    }

    public override void Dispose()
    {
        _capture?.Dispose();
        base.Dispose();
    }
}