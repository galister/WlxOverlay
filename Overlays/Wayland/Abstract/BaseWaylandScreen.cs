using WaylandSharp;
using WlxOverlay.Desktop.Wayland;
using WlxOverlay.Numerics;
using WlxOverlay.Overlays.Simple;

namespace WlxOverlay.Overlays.Wayland.Abstract;

/// <summary>
/// Base of all Wayland screens that rely on the WlOutput interface.
/// </summary>
public abstract class BaseWaylandScreen : BaseScreen<WaylandOutput>
{
    protected readonly WlDisplay Display;
    protected WlOutput? Output;

    protected override Rect2 OutputRect => WaylandInterface.Instance!.OutputRect;

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

    protected override void UploadTransform()
    {
        var oldTransform = Transform;
        Transform = Transform.RotatedLocal(Vector3.Back, Screen.Transform.Rotation);
        base.UploadTransform();
        Transform = oldTransform;
    }

    protected abstract void OnGlobal(WlRegistry reg, WlRegistry.GlobalEventArgs e);
}
