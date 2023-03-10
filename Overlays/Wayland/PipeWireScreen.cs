using WlxOverlay.Desktop.Pipewire;
using WlxOverlay.Desktop.Wayland;
using WlxOverlay.Numerics;
using WlxOverlay.Overlays.Simple;

namespace WlxOverlay.Overlays.Wayland;

public class PipeWireScreen : BaseScreen<PipewireOutput>
{
    private readonly PipeWireCapture? _pipewire;

    protected override Rect2 OutputRect => WaylandInterface.Instance!.OutputRect;

    public PipeWireScreen(PipewireOutput screen) : base(screen)
    {
        _pipewire = new PipeWireCapture(Screen.NodeId, Screen.Name, (uint)Screen.Size.X, (uint)Screen.Size.Y);

        foreach (var wlOutput in WaylandInterface.Instance!.Outputs.Values)
        {
            if (wlOutput.Position == Screen.Position && wlOutput.Size == Screen.Size)
            {
                screen.Name = wlOutput.Name;
                screen.IdName = wlOutput.IdName;
                screen.Handle = wlOutput.Handle;
            }
        }
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