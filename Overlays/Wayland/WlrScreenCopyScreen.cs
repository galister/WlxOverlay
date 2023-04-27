using WaylandSharp;
using WlxOverlay.Desktop.Wayland;
using WlxOverlay.Desktop.Wayland.Frame;
using WlxOverlay.Overlays.Simple;
using WlxOverlay.Overlays.Wayland.Abstract;

namespace WlxOverlay.Overlays.Wayland;

public class WlrScreenCopyScreen : BaseWlrScreen
{
    private WlShm? _shm;
    private ZwlrScreencopyManagerV1? _screencopyManager;

    public WlrScreenCopyScreen(WaylandOutput output) : base(output) { }

    protected override void Initialize()
    {
        base.Initialize();
        if (_screencopyManager == null)
        {
            Console.WriteLine("FATAL Your Wayland compositor does not support " + WlInterface.ZwlrScreencopyManagerV1.Name);
            Console.WriteLine("FATAL Check your `config.yaml`!");
            throw new ApplicationException();
        }
    }
    protected override void OnGlobal(WlRegistry reg, WlRegistry.GlobalEventArgs e)
    {
        if (e.Interface == WlInterface.WlShm.Name)
            _shm = reg.Bind<WlShm>(e.Name, e.Interface, e.Version);
        else if (e.Interface == WlInterface.ZwlrScreencopyManagerV1.Name)
            _screencopyManager = reg.Bind<ZwlrScreencopyManagerV1>(e.Name, e.Interface, e.Version);
    }

    protected override void RequestNewFrame()
    {
        Frame?.Dispose();
        Frame = new ScreenCopyFrame(Output!, _screencopyManager!, _shm!);
        Display.Roundtrip();
        while (Frame.GetStatus() == CaptureStatus.Pending)
        {
            Thread.Sleep(RoundTripSleepTime);
            Display.Roundtrip();
        }
    }

    protected override void Suspend()
    {
        Frame?.Dispose();
        Frame = null;
    }

    public override void Dispose()
    {
        _screencopyManager?.Dispose();
        _shm?.Dispose();
        base.Dispose();
    }
}
