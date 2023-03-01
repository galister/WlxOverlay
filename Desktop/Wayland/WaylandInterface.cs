using WaylandSharp;
using WlxOverlay.Numerics;
using WlxOverlay.Overlays;
using WlxOverlay.Overlays.Wayland;
using WlxOverlay.Types;

namespace WlxOverlay.Desktop.Wayland;

public class WaylandInterface : IDisposable
{
    public static WaylandInterface? Instance;

    public readonly Dictionary<uint, WaylandOutput> Outputs = new();
    public Rect2 OutputRect;

    private List<Type> _supportedScreenTypes = new();

    private readonly WlDisplay _display;
    private ZxdgOutputManagerV1? _outputManager;
    private WlSeat? _seat;

    public static void Initialize()
    {
        if (Instance != null)
            throw new ApplicationException($"Can't have more than one {nameof(WaylandInterface)}!");

        Instance = new WaylandInterface();
        Instance._display.Roundtrip();
    }

    public void RoundTrip()
    {
        _display.Roundtrip();
    }

    public Type GetScreenTypeToUse()
    {
        if (Config.Instance.WaylandCapture == "dmabuf")
        {
            Console.WriteLine("Using DMA-BUF capture.");
            return typeof(WlrDmaBufScreen);
        }
        if (Config.Instance.WaylandCapture == "screencopy")
        {
            Console.WriteLine("Using ScreenCopy capture.");
            return typeof(WlrScreenCopyScreen);
        }

        if (_supportedScreenTypes.Contains(typeof(ZwlrExportDmabufManagerV1)))
            return typeof(ZwlrExportDmabufManagerV1);
        
        if (_supportedScreenTypes.Contains(typeof(ZwlrScreencopyManagerV1)))
            return typeof(ZwlrScreencopyManagerV1);
        
        Console.WriteLine("FATAL WlxOverlay requires either wlr_export_dmabuf_v1 or wlr_screencopy_v1 protocols.");
        Console.WriteLine("FATAL Your Wayland compositor does not seem to support either.");
        throw new ApplicationException();
    }

    private WaylandInterface()
    {
        _display = WlDisplay.Connect();

        var reg = _display.GetRegistry();

        reg.Global += (_, e) =>
        {
            if (e.Interface == WlInterface.WlOutput.Name)
#pragma warning disable CS4014
                CreateOutput(reg, e);
            else if (e.Interface == WlInterface.WlSeat.Name)
                _seat = reg.Bind<WlSeat>(e.Name, e.Interface, e.Version);
            else if (e.Interface == WlInterface.ZxdgOutputManagerV1.Name)
                _outputManager = reg.Bind<ZxdgOutputManagerV1>(e.Name, e.Interface, e.Version);
            else if (e.Interface == WlInterface.ZwlrExportDmabufManagerV1.Name)
                _supportedScreenTypes.Add(typeof(WlrDmaBufScreen));
            else if (e.Interface == WlInterface.ZwlrScreencopyManagerV1.Name)
                _supportedScreenTypes.Add(typeof(WlrScreenCopyScreen));
        };

        reg.GlobalRemove += (_, e) =>
        {
            if (!Outputs.TryGetValue(e.Name, out var output))
                return;

            output.Dispose();
            Outputs.Remove(e.Name);
        };
    }

    private async Task CreateOutput(WlRegistry reg, WlRegistry.GlobalEventArgs e)
    {
        var wlOutput = reg.Bind<WlOutput>(e.Name, e.Interface, e.Version);

        while (_outputManager == null)
            await Task.Delay(10);

        var obj = new WaylandOutput(e.Name, wlOutput);

        using var xdgOutput = _outputManager.GetXdgOutput(wlOutput);
        xdgOutput.Name += obj.SetName;
        xdgOutput.LogicalSize += obj.SetSize;
        xdgOutput.LogicalPosition += obj.SetPosition;
        _display.Roundtrip();

        Outputs.Add(e.Name, obj);
        RecalculateOutputRect();
    }

    private void RecalculateOutputRect()
    {
        OutputRect = new Rect2();
        foreach (var output in Outputs.Values)
            OutputRect = OutputRect.Merge(new Rect2(output.Position.X, output.Position.Y, output.Size.X, output.Size.Y));
    }

    public void Dispose()
    {
        Thread.Sleep(5);

        foreach (var output in Outputs.Values)
            output.Dispose();

        _seat?.Dispose();
        _outputManager?.Dispose();
        _display.Dispose();
    }
}
