using WaylandSharp;
using WlxOverlay.Numerics;

namespace WlxOverlay.Desktop.Wayland;

public class WaylandInterface : IDisposable
{
    public static WaylandInterface? Instance;

    public readonly Dictionary<uint, WaylandOutput> Outputs = new();
    public Rect2 OutputRect;

    private readonly WlDisplay _display;
    private ZxdgOutputManagerV1? _outputManager;
    private ZwlrVirtualPointerManagerV1? _pointerManager;
    private ZwlrVirtualPointerV1? _pointer;
    private ZwpVirtualKeyboardManagerV1? _keyboardManager;
    private ZwpVirtualKeyboardV1? _keyboard;
    private WlSeat? _seat;

    private static readonly DateTime _epoch = DateTime.UtcNow.Date;
    public static uint Time() => (uint)(DateTime.UtcNow - _epoch).TotalMilliseconds;

    public static void Initialize()
    {
        if (Instance != null)
            throw new ApplicationException($"Can't have more than one {nameof(WaylandInterface)}!");

        Instance = new WaylandInterface();
        Instance._display.Roundtrip();
    }

    public ZwpVirtualKeyboardV1? GetVirtualKeyboard()
    {
        if (_keyboard == null)
        {
            if (_keyboardManager == null)
            {
                Console.WriteLine("ERROR Your Wayland compositor does not support zwp_virtual_keyboard_v1");
                Console.WriteLine("ERROR The keyboard will not function.");
            }
            else if (_seat == null)
                Console.WriteLine("ERROR wl_seat could not be loaded.");
            else
            {
                _keyboard = _keyboardManager.CreateVirtualKeyboard(_seat);
                using var wlKeyboard = _seat.GetKeyboard();
                wlKeyboard.Keymap += (_, e) => _keyboard.Keymap((uint)e.Format, e.Fd, e.Size);
                _display.Roundtrip();
            }
        }
        return _keyboard;
    }

    public ZwlrVirtualPointerV1? GetVirtualPointer()
    {
        if (_pointer == null)
        {
            if (_pointerManager == null)
            {
                Console.WriteLine("ERROR Your Wayland compositor does not support zwlr_virtual_pointer_v1");
                Console.WriteLine("ERROR The mouse pointer will not function.");
            }
            else if (_seat == null)
                Console.WriteLine("ERROR wl_seat could not be loaded.");
            else
                _pointer = _pointerManager.CreateVirtualPointer(_seat);
        }
        return _pointer;
    }

    public void RoundTrip()
    {
        _display.Roundtrip();
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
            else if (e.Interface == WlInterface.ZwpVirtualKeyboardManagerV1.Name)
                _keyboardManager = reg.Bind<ZwpVirtualKeyboardManagerV1>(e.Name, e.Interface, e.Version);
            else if (e.Interface == WlInterface.ZwlrVirtualPointerManagerV1.Name)
                _pointerManager = reg.Bind<ZwlrVirtualPointerManagerV1>(e.Name, e.Interface, e.Version);
            else if (e.Interface == WlInterface.ZxdgOutputManagerV1.Name)
                _outputManager = reg.Bind<ZxdgOutputManagerV1>(e.Name, e.Interface, e.Version);
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

        _keyboard?.Dispose();
        _keyboardManager?.Dispose();
        _pointer?.Dispose();
        _pointerManager?.Dispose();
        _seat?.Dispose();
        _outputManager?.Dispose();
        _display.Dispose();
    }
}
