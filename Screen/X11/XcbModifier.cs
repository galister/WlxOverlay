namespace X11Overlay.Screen.X11
{
    [Flags]
    public enum XcbModifier : ushort
    {
        Shift = 1,
        Caps = 2,
        Ctrl = 4,
        Alt = 8,
        NumLock = 16,
        Super = 64
    }
}