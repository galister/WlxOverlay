using WlxOverlay.Types;

namespace WlxOverlay.Desktop;

public interface IKeyboardProvider
{
    void SetModifiers(KeyModifier newModifiers);
    void SendKey(VirtualKey key, bool depressed);
}

public class KeyboardProvider : IKeyboardProvider
{
    public static IKeyboardProvider Instance = new KeyboardProvider();
    public void SetModifiers(KeyModifier newModifiers) { }
    public void SendKey(VirtualKey key, bool depressed) { }
}