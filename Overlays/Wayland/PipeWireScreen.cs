using WlxOverlay.Desktop.Pipewire;
using WlxOverlay.Desktop.Wayland;
using WlxOverlay.Overlays.Wayland.Abstract;

namespace WlxOverlay.Overlays.Wayland;

public class PipeWireScreen : BaseWaylandScreen<PipewireOutput>
{
    private PipeWireCapture? _pipewire;
    
    public PipeWireScreen(PipewireOutput screen) : base(screen)
    { }
    
    protected override void Initialize()
    {
        base.Initialize();
        _pipewire = new PipeWireCapture(Screen.NodeId, Screen.Name, (uint) Screen.Size.X, (uint) Screen.Size.Y);
    }

    protected internal override void Render()
    {
        _pipewire?.ApplyToTexture(Texture!);
        base.Render();
    }
    
    public override void Dispose()
    {
        _pipewire?.Dispose();
        base.Dispose();
    }
}