using WlxOverlay.Types;

namespace WlxOverlay.Input;

public interface IKeyboardProvider
{
    void SetModifiers(KeyModifier newModifiers);
    void SendKey(VirtualKey key, bool depressed);
}