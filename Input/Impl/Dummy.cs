namespace WlxOverlay.Input.Impl;

public class DummyMouse : IMouseProvider
{
    public void MouseMove(int x, int y) { }
    public void SendButton(EvBtn button, bool pressed) { }
    public void Wheel(int delta) { }
}

public class DummyKeyboard : IKeyboardProvider
{
    public void SetModifiers(KeyModifier newModifiers) { }
    public void SendKey(VirtualKey key, bool depressed) { }
}