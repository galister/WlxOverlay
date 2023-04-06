using MutterDisplayConfig.DBus;
using Tmds.DBus.Protocol;
using WlxOverlay.Numerics;

namespace WlxOverlay.Desktop.Wayland;

public static class GnomeDisplayHandler
{
    public static async IAsyncEnumerable<WaylandOutput> GetOutputsAsync()
    {
        var dbus = new Connection(Address.Session!);
        await dbus.ConnectAsync();

        var service = new MutterDisplayConfigService(dbus, "org.gnome.Mutter.DisplayConfig");
        var state = await service
            .CreateDisplayConfig("org.gnome.Mutter.DisplayConfig")
            .GetCurrentStateAsync();

        foreach (var lm in state.LogicalMonitors)
        {
            yield return new WaylandOutput(0, null)
            {
                Name = lm.Item6.First().Item1, // connector name
                Position = new Vector2Int(lm.Item1, lm.Item2), // x, y position
            };
        }
    }
}