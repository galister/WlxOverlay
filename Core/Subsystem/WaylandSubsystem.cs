using WaylandSharp;
using WlxOverlay.Capture;
using WlxOverlay.Capture.Wlr;
using WlxOverlay.Desktop.Wayland;
using WlxOverlay.GFX;
using WlxOverlay.Overlays;
using WlxOverlay.Types;

namespace WlxOverlay.Core.Subsystem;

public class WaylandSubsystem : ISubsystem
{
    public static string? DisplayName;

    private DateTime _nextRoundTrip = DateTime.UtcNow;

    private readonly Dictionary<uint, WaylandOutput> _outputs = new();

    private readonly List<CaptureMethod> _supportedCaptureMethods = new();

    private readonly WlDisplay _display;
    private ZxdgOutputManagerV1? _outputManager;
    private WlSeat? _seat;
    private CaptureMethod _captureMethod;

    public static bool TryInitialize(out WaylandSubsystem instance)
    {
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
        {
            instance = null!;
            return false;
        }

        Console.WriteLine("Wayland detected.");

        switch (Config.Instance.WaylandCapture)
        {
            case "screencopy":
                Console.WriteLine("Not loading EGL due to screencopy.");
                break;
            case "pw-fallback":
                Console.WriteLine("Not loading EGL due to pw-fallback.");
                break;
            default:
                EGL.Initialize();
                break;
        }

        instance = new WaylandSubsystem();

        var maxTries = 100;
        while (instance._outputs.Count == 0 && maxTries-- > 0)
        {
            instance._display.Roundtrip();
            Thread.Sleep(50);
        }
        Thread.Sleep(100);

        switch (Config.Instance.WaylandCapture)
        {
            case "dmabuf":
                instance._captureMethod = CaptureMethod.WlrDmaBuf;
                break;
            case "screencopy":
                instance._captureMethod = CaptureMethod.WlrScreenCopy;
                break;
            case "pipewire":
            case "pw-fallback":
                instance._captureMethod = CaptureMethod.PipeWire;
                break;
            default:
                if (instance._supportedCaptureMethods.Contains(CaptureMethod.WlrDmaBuf))
                    instance._captureMethod = CaptureMethod.WlrDmaBuf;
                else if (instance._supportedCaptureMethods.Contains(CaptureMethod.WlrScreenCopy))
                    instance._captureMethod = CaptureMethod.WlrScreenCopy;
                else
                    instance._captureMethod = CaptureMethod.PipeWire;
                break;
        }

        return true;
    }

    public void Initialize()
    {
        // No Subsystem Initialization
    }

    public void Update()
    {
        if (_nextRoundTrip < DateTime.UtcNow)
        {
            _display.Roundtrip();
            _nextRoundTrip = DateTime.UtcNow.AddSeconds(1);
        }
    }

    public async Task CreateScreensAsync()
    {
        switch (_captureMethod)
        {
            case CaptureMethod.WlrDmaBuf:
                Console.WriteLine($"Using desktop capture protocol: {WlInterface.ZwlrExportDmabufManagerV1.Name}");
                foreach (var output in _outputs.Values)
                {
                    var screen = new DesktopOverlay(output,
                        new WlrCapture<DmaBufFrame>(output));
                    OverlayRegistry.Register(screen);
                }
                break;
            case CaptureMethod.WlrScreenCopy:
                Console.WriteLine($"Using desktop capture protocol: {WlInterface.ZwlrScreencopyManagerV1.Name}");
                foreach (var output in _outputs.Values)
                {
                    var screen = new DesktopOverlay(output,
                        new WlrCapture<ScreenCopyFrame>(output));
                    OverlayRegistry.Register(screen);
                }
                break;
            case CaptureMethod.PipeWire:
                Console.WriteLine("Using PipeWire capture.");
                await CreatePipeWireScreensAsync();
                break;
        }
    }

    private async Task CreatePipeWireScreensAsync()
    {
        PipeWireCapture.Load();

        if (_outputs.Values.Count > 0)
        {
            Console.WriteLine(" You will be prompted one screen at a time.\n" +
                              " Please select the corresponding screen on the prompt.\n" +
                              " Cancel the prompt if you do not wish to capture the given screen. \n" +
                              " If your compositor supports org.freedesktop.portal.ScreenCast v4, you will only be prompted once.");

            foreach (var output in _outputs.Values)
            {
                if (output == null!) // idk why, but it happens
                    continue;

                var data = await XdgScreenCastHandler.PromptUserAsync(output);
                if (data != null)
                {
                    var screen = new DesktopOverlay(output, new PipeWireCapture(output, data.Value));
                    OverlayRegistry.Register(screen);
                    Console.WriteLine($"{output.Name} -> {data.Value}");
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
                output.RecalculateTransform();
                var screen = new DesktopOverlay(output, new PipeWireCapture(output, data.Value));
                OverlayRegistry.Register(screen);
                Console.WriteLine($"{output.Name} -> {data.Value}");

            }
            else
                Console.WriteLine($"{output.Name} will not be used.");
        }
    }

    private WaylandSubsystem()
    {
        _display = WlDisplay.Connect(DisplayName!);

        var reg = _display.GetRegistry();

        reg.Global += (__, e) =>
        {
            if (e.Interface == WlInterface.WlOutput.Name)
                _ = CreateOutputAsync(reg, e);
            else if (e.Interface == WlInterface.WlSeat.Name)
                _seat = reg.Bind<WlSeat>(e.Name, e.Interface, e.Version);
            else if (e.Interface == WlInterface.ZxdgOutputManagerV1.Name)
                _outputManager = reg.Bind<ZxdgOutputManagerV1>(e.Name, e.Interface, e.Version);
            else if (e.Interface == WlInterface.ZwlrExportDmabufManagerV1.Name)
                _supportedCaptureMethods.Add(CaptureMethod.WlrDmaBuf);
            else if (e.Interface == WlInterface.ZwlrScreencopyManagerV1.Name)
                _supportedCaptureMethods.Add(CaptureMethod.WlrScreenCopy);
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

    private enum CaptureMethod
    {
        PipeWire,
        WlrDmaBuf,
        WlrScreenCopy
    }
}
