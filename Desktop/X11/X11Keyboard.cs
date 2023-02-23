using System.Runtime.InteropServices;
using X11Overlay.Types;

namespace X11Overlay.Desktop.X11;

public class X11Keyboard : IKeyboardProvider
{
    private KeyModifier _curModifiers;
    public void SetModifiers(KeyModifier newModifiers)
    {
        var changed = _curModifiers ^ newModifiers;
        foreach (var mod in Enum.GetValues<KeyModifier>())
            if ((changed & mod) != 0)
                SendKey(KeyboardLayout.ModifiersToKeys[mod][0], (newModifiers & mod) != 0);

        _curModifiers = newModifiers;
    }

    public void SendKey(VirtualKey key, bool depressed)
    {
        xshm_keybd_event(IntPtr.Zero, (byte)key, depressed ? 1 : 0);
    }

    [DllImport("libxshm_cap.so")]
    private static extern void xshm_keybd_event(IntPtr xhsm_instance, Byte keycode, Int32 pressed);
}