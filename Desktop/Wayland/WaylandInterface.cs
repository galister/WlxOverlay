using WaylandSharp;
using WlxOverlay.Core;
using WlxOverlay.Desktop.Pipewire;
using WlxOverlay.Overlays.Simple;
using WlxOverlay.Overlays.Wayland;
using WlxOverlay.Types;

namespace WlxOverlay.Desktop.Wayland;

public class WaylandInterface : IDisposable
{
    public static WaylandInterface? Instance;
    public static string? DisplayName;

    private readonly Dictionary<uint, WaylandOutput> _outputs = new();

    private readonly List<Type> _supportedScreenTypes = new();

    private readonly WlDisplay _display;
    private ZxdgOutputManagerV1? _outputManager;
    private WlSeat? _seat;

    public static bool TryInitialize()
    {
        if (Instance != null)
            throw new ApplicationException($"Can't have more than one {nameof(WaylandInterface)}!");

        var env = Environment.GetEnvironmentVariable("WAYLAND_DISPLAY");
        if (env != null)
            DisplayName = env;
        else
        {
            env = Environment.GetEnvironmentVariable("XDG_RUNTIME_DIR");
            if (env != null)
                foreach (var fPath in Directory.GetFiles(env))
                {
                    var fName = Path.GetFileName(fPath);
                    if (fName.StartsWith("wayland-"))
                    {
                        DisplayName = fName;
                        Environment.SetEnvironmentVariable("WAYLAND_DISPLAY", DisplayName);
                        break;
                    }
                }
        }

        if (DisplayName == null)
            return false;

        Console.WriteLine("Wayland detected.");

        if (Config.Instance.WaylandCapture != "pw-fallback")
            EGL.Initialize();

        Instance = new WaylandInterface();

        var maxTries = 100;
        while (Instance._outputs.Count == 0 && maxTries-- > 0)
        {
            Instance.RoundTrip();
            Thread.Sleep(50);
        }

        return true;
    }

    public void RoundTrip()
    {
        _display.Roundtrip();
    }

    public IAsyncEnumerable<BaseOverlay> CreateScreensAsync()
    {
        IAsyncEnumerable<BaseOverlay> UseWlrDmaBuf()
        {
            Console.WriteLine($"Using desktop capture protocol: {WlInterface.ZwlrExportDmabufManagerV1.Name}");
            return _outputs.Values.Select(x => new WlrDmaBufScreen(x)).AsAsync();
        }

        IAsyncEnumerable<BaseOverlay> UseWlrScreenCopy()
        {
            Console.WriteLine($"Using desktop capture protocol: {WlInterface.ZwlrScreencopyManagerV1.Name}");
            return _outputs.Values.Select(x => new WlrScreenCopyScreen(x)).AsAsync();
        }

        IAsyncEnumerable<BaseOverlay> UseKdeScreenCast()
        {
            Console.WriteLine($"Using desktop capture protocol: {WlInterface.ZkdeScreencastUnstableV1.Name}");
            return _outputs.Values.Select(x => new KdeScreenCastScreen(x)).AsAsync();
        }

        async IAsyncEnumerable<BaseOverlay> UsePipeWire(bool dmaBuf)
        {
            Console.WriteLine("Using PipeWire capture.");
            PipeWireCapture.Load(dmaBuf);

            if (_outputs.Values.Count > 0)
            {
                Console.WriteLine(" You will be prompted one screen at a time.\n" +
                                  " Please select the corresponding screen on the prompt.\n" +
                                  " Cancel the prompt if you do not wish to capture the given screen. \n" +
                                  " If your compositor supports org.freedesktop.portal.ScreenCast v4, you will only be prompted once.");

                foreach (var output in _outputs.Values)
                {
                    if (output == null)
                        continue;
                    var data = await XdgScreenCastHandler.PromptUserAsync(output);
                    if (data != null)
                    {
                        var screen = new PipeWireScreen(data);
                        Console.WriteLine($"{data.Name} -> {data.NodeId}");
                        yield return screen;
                    }
                    else
                        Console.WriteLine($"{output.Name} will not be used.");
                }
            }
            else
            {
                Console.WriteLine(" ERROR Could not poll Wayland outputs.\n" +
                                  " You may still use WlxOverlay in single-screen mode.\n" +
                                  " Select your screen which is positioned at 0,0.");

                var output = new WaylandOutput(0, null) { Name = "Default" };
                var data = await XdgScreenCastHandler.PromptUserAsync(output);
                if (data != null)
                {
                    var screen = new PipeWireScreenSingle(data);
                    Console.WriteLine($"{data.Name} -> {data.NodeId}");
                    yield return screen;
                }
                else
                    Console.WriteLine($"{output.Name} will not be used.");
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
                return UsePipeWire(dmaBuf: true);
            case "pw-fallback":
                return UsePipeWire(dmaBuf: false);
            default:
                if (_supportedScreenTypes.Contains(typeof(WlrDmaBufScreen)))
                    return UseWlrDmaBuf();
                if (_supportedScreenTypes.Contains(typeof(WlrScreenCopyScreen)))
                    return UseWlrScreenCopy();
                if (_supportedScreenTypes.Contains(typeof(KdeScreenCastScreen)))
                    return UseKdeScreenCast();

                return UsePipeWire(dmaBuf: true);
        }
    }

    private WaylandInterface()
    {
        _display = WlDisplay.Connect(DisplayName!);

        var reg = _display.GetRegistry();

        reg.Global += (_, e) =>
        {
            if (e.Interface == WlInterface.WlOutput.Name)
                _ = CreateOutputAsync(reg, e);
            else if (e.Interface == WlInterface.WlSeat.Name)
                _seat = reg.Bind<WlSeat>(e.Name, e.Interface, e.Version);
            else if (e.Interface == WlInterface.ZxdgOutputManagerV1.Name)
                _outputManager = reg.Bind<ZxdgOutputManagerV1>(e.Name, e.Interface, e.Version);
            else if (e.Interface == WlInterface.ZwlrExportDmabufManagerV1.Name)
                _supportedScreenTypes.Add(typeof(WlrDmaBufScreen));
            else if (e.Interface == WlInterface.ZwlrScreencopyManagerV1.Name)
                _supportedScreenTypes.Add(typeof(WlrScreenCopyScreen));
            else if (e.Interface == WlInterface.ZkdeScreencastUnstableV1.Name)
                _supportedScreenTypes.Add(typeof(KdeScreenCastScreen));
        };

        reg.GlobalRemove += (_, e) =>
        {
            if (!_outputs.TryGetValue(e.Name, out var output))
                return;

            _outputs.Remove(e.Name);
            output.Dispose();
        };
    }

    private async Task CreateOutputAsync(WlRegistry reg, WlRegistry.GlobalEventArgs e)
    {
        var wlOutput = reg.Bind<WlOutput>(e.Name, e.Interface, e.Version);

        while (_outputManager == null)
            await Task.Delay(10);

        var obj = new WaylandOutput(e.Name, wlOutput);

        wlOutput.Geometry += obj.SetGeometry;
        wlOutput.Mode += obj.SetMode;

        using var xdgOutput = _outputManager.GetXdgOutput(wlOutput);
        xdgOutput.Name += obj.SetName;
        xdgOutput.LogicalSize += obj.SetSize;
        xdgOutput.LogicalPosition += obj.SetPosition;
        _display.Roundtrip();

        _outputs.Add(e.Name, obj);
        obj.RecalculateTransform();
    }

    public void Dispose()
    {
        Thread.Sleep(5);

        foreach (var output in _outputs.Values)
            output.Dispose();

        _seat?.Dispose();
        _outputManager?.Dispose();
        _display.Dispose();
    }
}
