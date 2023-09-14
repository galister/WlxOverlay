using WlxOverlay.Input.Impl;

namespace WlxOverlay.Input;

public static class InputProvider
{
    public static IMouseProvider Mouse { get; private set; } = null!;
    public static IKeyboardProvider Keyboard { get; private set; } = null!;

    public static void UseUInput()
    {
        try
        {
            var uInput = new UInput();
            Mouse = uInput;
            Keyboard = uInput;
        }
        catch (ApplicationException)
        {
            Console.WriteLine("FATAL Could not register uinput device.");
            Console.WriteLine("FATAL Check that you are in the `input` group or otherwise have access.");
            Console.WriteLine("FATAL Try: sudo usermod -a -G input $USER");
        }
    }

    public static void UseDummy()
    {
        Mouse = new DummyMouse();
        Keyboard = new DummyKeyboard();
    }
}