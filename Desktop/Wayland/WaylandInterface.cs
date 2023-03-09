using WaylandSharp;
using WlxOverlay.Numerics;
using WlxOverlay.Overlays.Simple;
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

    public IEnumerable<BaseOverlay> CreateScreens()
    {
        IEnumerable<BaseOverlay> UseWlrDmaBuf()
        {   
            Console.WriteLine($"Using desktop capture protocol: {WlInterface.ZwlrExportDmabufManagerV1.Name}");
            return Outputs.Values.Select(x => new WlrDmaBufScreen(x));
        }

        IEnumerable<BaseOverlay> UseWlrScreenCopy()
        {   
            Console.WriteLine($"Using desktop capture protocol: {WlInterface.ZwlrScreencopyManagerV1.Name}");
            return Outputs.Values.Select(x => new WlrScreenCopyScreen(x));
        }

        IEnumerable<BaseOverlay> UseKdeScreenCast()
        {   
            Console.WriteLine($"Using desktop capture protocol: {WlInterface.ZkdeScreencastUnstableV1.Name}");
            return Outputs.Values.Select(x => new WlrScreenCopyScreen(x));
        }
        
        IEnumerable<BaseOverlay> UsePipeWire()
        {
            Console.WriteLine("Using Fallback PipeWire capture. ");
            Console.WriteLine("--- Select one screen at a time using the popup dialogs, then press Cancel. ---");
            while (true)
            {
                var data = XdgScreenCastHandler.PromptUserAsync().GetAwaiter().GetResult();
                if (data == null)
                    yield break;
                yield return new PipeWireScreen(data);
            }
        }

        switch (Config.Instance.WaylandCapture)
        {
            case "dmabuf":
                return UseWlrDmaBuf();
            case "screencopy":
                return UseWlrScreenCopy();
            case "kde":
                return UseKdeScreenCast();
            case "pipewire":
                return UsePipeWire();
            default:
                if (_supportedScreenTypes.Contains(typeof(ZwlrExportDmabufManagerV1)))
                    return UseWlrDmaBuf();
                if (_supportedScreenTypes.Contains(typeof(ZwlrScreencopyManagerV1)))
                    return UseWlrScreenCopy();
                if (_supportedScreenTypes.Contains(typeof(ZkdeScreencastUnstableV1Pointer)))
                    return UseKdeScreenCast();
                return UsePipeWire();
        }
    }

    private WaylandInterface()
    {
        _display = WlDisplay.Connect();

        var reg = _display.GetRegistry();

        reg.Global += (_, e) =>
        {
            if (e.Interface == WlInterface.WlOutput.Name)
#pragma warning disable CS4014
#pragma warning disable VSTHRD110
                CreateOutputAsync(reg, e);
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

    private async Task CreateOutputAsync(WlRegistry reg, WlRegistry.GlobalEventArgs e)
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
