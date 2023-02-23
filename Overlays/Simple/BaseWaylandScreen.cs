using WaylandSharp;
using X11Overlay.Core;
using X11Overlay.Desktop;
using X11Overlay.Desktop.Wayland;
using X11Overlay.Desktop.Wayland.Frame;
using X11Overlay.GFX;
using X11Overlay.Types;

namespace X11Overlay.Overlays.Simple;

public abstract class BaseWaylandScreen : BaseScreen<WaylandOutput>
{
    protected readonly WlDisplay Display;
    protected WlOutput? Output;
    protected IWaylandFrame? Frame;

    private ZwlrVirtualPointerV1? _pointer;

    private DateTime _freezeCursor = DateTime.MinValue;
    private static bool _pointerSendFrame;
    private static bool _mouseMoved;

    private readonly CancellationTokenSource _cancel = new();
    private Task? _worker;

    protected BaseWaylandScreen(WaylandOutput output) : base(output)
    {
        Display = WlDisplay.Connect();

        var reg = Display.GetRegistry();

        reg.Global += (_, e) =>
        {
            if (e.Interface == WlInterface.WlOutput.Name)
            {
                if (e.Name == Screen.IdName)
                    Output = reg.Bind<WlOutput>(e.Name, e.Interface, e.Version);
            }
            else OnGlobal(reg, e);
        };

        reg.GlobalRemove += (_, e) =>
        {
            if (e.Name == Screen.IdName)
                Dispose();
        };

        Display.Roundtrip();

        //_pointer = WaylandInterface.Instance!.GetVirtualPointer();
    }

    protected abstract void OnGlobal(WlRegistry reg, WlRegistry.GlobalEventArgs e);
    protected abstract void RequestNewFrame();
    protected abstract void Suspend();

    protected override void Initialize()
    {
        base.Initialize();

        Texture = GraphicsEngine.Instance.EmptyTexture((uint)Screen.Size.X, (uint)Screen.Size.Y, internalFormat: GraphicsFormat.RGB8, dynamic: true);

        UpdateInteractionTransform();
        UploadCurvature();
    }

    protected internal override void AfterInput(bool batteryStateUpdated)
    {
        base.AfterInput(batteryStateUpdated);

        if (!Visible && _worker is { Status: TaskStatus.RanToCompletion })
        {
            _worker.Dispose();
            _worker = null;
            Suspend();
        }

    }

    protected internal override void Render()
    {
        if (_pointerSendFrame)
        {
            _pointer!.Frame();
            WaylandInterface.Instance!.RoundTrip();
        }

        var wantNewFrame = true;

        if (_worker is { Status: TaskStatus.RanToCompletion })
        {
            _worker.Dispose();
            switch (Frame!.GetStatus())
            {
                case CaptureStatus.FrameReady:
                    Frame.ApplyToTexture(Texture!);
                    break;
                case CaptureStatus.FrameSkipped:
                    Console.WriteLine($"{Screen.Name}: Frame was skipped.");
                    break;
                case CaptureStatus.Fatal:
                    OverlayManager.Instance.UnregisterChild(this);
                    Dispose();
                    return;
            }
        }
        else if (_worker != null)
            wantNewFrame = false;

        if (wantNewFrame)
            _worker = Task.Run(RequestNewFrame, _cancel.Token);

        base.Render();

        _pointerSendFrame = _mouseMoved = false;
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
        if (KeyboardProvider.Instance is UInput uInput)
        {
            var evBtn = hitData.modifier switch
            {
                PointerMode.Right => EvBtn.Right,
                PointerMode.Middle => EvBtn.Middle,
                _ => EvBtn.Left
            };
            
            uInput.SendButton(evBtn, pressed);
            return;
        }

        if (_pointer == null)
            return;

        var mic = hitData.modifier switch
        {
            PointerMode.Right => MouseInputCode.Right,
            PointerMode.Middle => MouseInputCode.Middle,
            _ => MouseInputCode.Left
        };
        
        

        _pointer.Button(WaylandInterface.Time(), (uint)mic, pressed ? WlPointerButtonState.Pressed : WlPointerButtonState.Released);
        _pointerSendFrame = true;
    }

    private void MoveMouse(PointerHit hitData)
    {
        if (KeyboardProvider.Instance is UInput uInput)
        {
            var uv = hitData.uv;
            var posX = uv.x * Screen.Size.X + Screen.Position.X;
            var posY = uv.y * Screen.Size.Y + Screen.Position.Y;
            var rectSize = WaylandInterface.Instance!.OutputRect.Size;

            var mulX = UInput.Extent / rectSize.x;
            var mulY = UInput.Extent / rectSize.y;
            
            uInput.MouseMove((int)(posX * mulX), (int)(posY * mulY));
            return;
        }

        if (_pointer != null)
        {
            var uv = hitData.uv;
            var posX = uv.x * Screen.Size.X + Screen.Position.X;
            var posY = uv.y * Screen.Size.Y + Screen.Position.Y;
            var extent = WaylandInterface.Instance!.OutputRect.Size;

            _pointer.MotionAbsolute(WaylandInterface.Time(), (uint)posX, (uint)posY, (uint)extent.x, (uint)extent.y);
            _pointerSendFrame = _mouseMoved = true;
        }
    }

    protected internal override void OnScroll(PointerHit hitData, float value)
    {
        base.OnScroll(hitData, value);
        if (_pointer == null)
            return;

        var multiplier = hitData.modifier switch
        {
            PointerMode.Middle => -30f,
            PointerMode.Right => -20f,
            _ => -10f
        };

        var time = WaylandInterface.Time();
        _pointer.AxisSource(WlPointerAxisSource.Wheel);
        _pointer.Axis(time, WlPointerAxis.VerticalScroll, value * multiplier);
        _pointerSendFrame = true;
    }

    public override string ToString()
    {
        return Screen.Name;
    }

    public override void Dispose()
    {
        _cancel.Cancel();
        _pointer?.Dispose();
        Texture?.Dispose();
        base.Dispose();
    }
}

public enum MouseInputCode
{
    Left = 0x110,
    Right = 0x111,
    Middle = 0x112,
}
