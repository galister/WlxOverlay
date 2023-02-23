using WaylandSharp;
using X11Overlay.Types;

namespace X11Overlay.Desktop.Wayland;

public class WaylandKeyboard : IKeyboardProvider
{
    private readonly ZwpVirtualKeyboardV1? _keyboard;

    public WaylandKeyboard()
    {
        _keyboard = WaylandInterface.Instance!.GetVirtualKeyboard();
    }

    private const KeyModifier LockMods = KeyModifier.CapsLock | KeyModifier.NumLock;
    public void SetModifiers(KeyModifier newModifiers)
    {
        _keyboard?.Modifiers((uint)(newModifiers & ~LockMods), 0, (uint)(newModifiers & LockMods), 0);
        WaylandInterface.Instance!.RoundTrip();
    }

    public void SendKey(VirtualKey keyCode, bool depressed)
    {
        _keyboard?.Key(WaylandInterface.Time(), (uint)keyCode - 8U, depressed ? 1U : 0U);
        WaylandInterface.Instance!.RoundTrip();
    }
}
