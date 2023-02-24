using WaylandSharp;
using WlxOverlay.Core;
using WlxOverlay.Desktop;
using WlxOverlay.Desktop.Wayland;
using WlxOverlay.Desktop.Wayland.Frame;
using WlxOverlay.GFX;

namespace WlxOverlay.Overlays.Simple;

public abstract class BaseWaylandScreen : BaseScreen<WaylandOutput>
{
    protected readonly WlDisplay Display;
    protected WlOutput? Output;
    protected IWaylandFrame? Frame;

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
