using WlxOverlay.Desktop.Wayland;
using WlxOverlay.Desktop.Wayland.Frame;

namespace WlxOverlay.Overlays.Wayland.Abstract;

/// <summary>
/// Base of all Wayland screens that use the wlr frame-request workflow.
/// </summary>
public abstract class BaseWlrScreen : BaseWaylandScreen
{
    protected IWaylandFrame? Frame;
    protected TimeSpan RoundTripSleepTime = TimeSpan.FromMilliseconds(1);

    private readonly CancellationTokenSource _cancel = new();
    private Task? _worker;

    protected BaseWlrScreen(WaylandOutput output) : base(output) { }

    protected abstract void RequestNewFrame();
    protected abstract void Suspend();
    
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
}
