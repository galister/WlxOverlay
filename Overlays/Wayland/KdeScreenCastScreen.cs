using WaylandSharp;
using WlxOverlay.Desktop.Pipewire;
using WlxOverlay.Desktop.Wayland;
using WlxOverlay.Overlays.Wayland.Abstract;

namespace WlxOverlay.Overlays.Wayland;

public class KdeScreenCastScreen : BaseWaylandScreen
{
    private ZkdeScreencastUnstableV1? _screencastManager;
    private ZkdeScreencastStreamUnstableV1? _stream;

    private PipeWireCapture? _pipewire;

    public KdeScreenCastScreen(WaylandOutput output) : base(output)
    {
    }

    protected override void Initialize()
    {
        Screen.RecalculateTransform();
        base.Initialize();
        if (_screencastManager == null)
        {
            Console.WriteLine("FATAL Your Wayland compositor does not support " + WlInterface.ZkdeScreencastUnstableV1.Name);
            Console.WriteLine("FATAL Check your `config.yaml`!");
            throw new ApplicationException();
        }

        _stream = _screencastManager.StreamOutput(Screen.Handle!, 2); // 2: rendered
        _stream.Created += (_, e) =>
            _pipewire = new PipeWireCapture(e.Node, Screen.Name, (uint)Screen.Size.X, (uint)Screen.Size.Y);
        _stream.Failed += (_, e) =>
        {
            Console.WriteLine($"Stream failure @ {Screen.Name}: {e.Error}");
            _stream.Close();
        };
        _stream.Closed += (_, _) =>
        {
            _stream = null;
            Dispose();
        };
    }

    protected override void OnGlobal(WlRegistry reg, WlRegistry.GlobalEventArgs e)
    {
        if (e.Interface == WlInterface.ZkdeScreencastUnstableV1.Name)
            _screencastManager = reg.Bind<ZkdeScreencastUnstableV1>(e.Name, e.Interface, e.Version);
    }

    protected internal override void Render()
    {
        _pipewire?.ApplyToTexture(Texture!);

        base.Render();
    }

    public override void Dispose()
    {
        _pipewire?.Dispose();
        _stream?.Close();
        _screencastManager?.Dispose();
        base.Dispose();
    }
}