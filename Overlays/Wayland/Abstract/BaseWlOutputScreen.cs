using WaylandSharp;
using WlxOverlay.Desktop;
using WlxOverlay.Desktop.Wayland;
using WlxOverlay.GFX;

namespace WlxOverlay.Overlays.Wayland.Abstract;

/// <summary>
/// Base of all Wayland screens that rely on the WlOutput interface.
/// </summary>
public abstract class BaseWlOutputScreen : BaseWaylandScreen<WaylandOutput>
{
    protected readonly WlDisplay Display;
    protected WlOutput? Output;

    protected BaseWlOutputScreen(WaylandOutput output) : base(output)
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
}
