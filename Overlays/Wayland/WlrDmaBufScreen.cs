using WaylandSharp;
using WlxOverlay.Desktop.Wayland;
using WlxOverlay.Desktop.Wayland.Frame;
using WlxOverlay.Overlays.Simple;

namespace WlxOverlay.Overlays.Wayland;

public class WlrDmaBufScreen : BaseWlrScreen
{
    private ZwlrExportDmabufManagerV1? _dmabufManager;
    private IWaylandFrame? _lastFrame;

    public WlrDmaBufScreen(WaylandOutput output) : base(output) { }

    protected override void Initialize()
    {
        base.Initialize();
        if (_dmabufManager == null)
        {
            Console.WriteLine("FATAL Your Wayland compositor does not support wlr_export_dmabuf_v1");
            Console.WriteLine("FATAL Edit your `config.yaml` and set `wayland_capture: screencopy`");
            throw new ApplicationException();
        }
    }

    protected override void OnGlobal(WlRegistry reg, WlRegistry.GlobalEventArgs e)
    {
        if (e.Interface == WlInterface.ZwlrExportDmabufManagerV1.Name)
            _dmabufManager = reg.Bind<ZwlrExportDmabufManagerV1>(e.Name, e.Interface, e.Version);
    }

    protected override void RequestNewFrame()
    {
        _lastFrame?.Dispose();
        _lastFrame = Frame;
        Frame = new DmaBufFrame(Output!, _dmabufManager!);
        Display.Roundtrip();
        while (Frame.GetStatus() == CaptureStatus.Pending)
        {
            Thread.Sleep(RoundTripSleepTime);
            Display.Roundtrip();
        }
    }

    protected override void Suspend()
    {
        _lastFrame?.Dispose();
        Frame?.Dispose();
        _lastFrame = null;
        Frame = null;
    }

    public override void Dispose()
    {
        _dmabufManager?.Dispose();
        base.Dispose();
    }
}