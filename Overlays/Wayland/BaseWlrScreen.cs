using WlxOverlay.Desktop;
using WlxOverlay.Desktop.Wayland;
using WlxOverlay.Desktop.Wayland.Frame;
using WlxOverlay.GFX;
using WlxOverlay.Overlays.Wayland;

namespace WlxOverlay.Overlays.Simple;

public abstract class BaseWlrScreen : BaseWaylandScreen
{
    protected IWaylandFrame? Frame;
    protected TimeSpan RoundTripSleepTime = TimeSpan.FromMilliseconds(1);
    
    private readonly CancellationTokenSource _cancel = new();
    private Task? _worker;

    protected BaseWlrScreen(WaylandOutput output) : base(output) {  }
    
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
                    Dispose();
                    return;
            }
        }
        else if (_worker != null)
            wantNewFrame = false;

        if (wantNewFrame)
            _worker = Task.Run(RequestNewFrame, _cancel.Token);

        base.Render();
    }

    protected override bool MoveMouse(PointerHit hitData)
    {
        if (UInp == null)
            return false;

        var uv = hitData.uv;
        var posX = uv.x * Screen.Size.X + Screen.Position.X;
        var posY = uv.y * Screen.Size.Y + Screen.Position.Y;
        var rectSize = WaylandInterface.Instance!.OutputRect.Size;

        var mulX = UInput.Extent / rectSize.x;
        var mulY = UInput.Extent / rectSize.y;

        UInp.MouseMove((int)(posX * mulX), (int)(posY * mulY));
        return true;
    }

    public override string ToString()
    {
        return Screen.Name;
    }

    public override void Dispose()
    {
        _cancel.Cancel();
        Texture?.Dispose();
        base.Dispose();
    }
}
