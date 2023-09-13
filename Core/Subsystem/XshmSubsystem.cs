using WlxOverlay.Capture;
using WlxOverlay.Desktop;
using WlxOverlay.Overlays;

namespace WlxOverlay.Core.Subsystem;

public class XshmSubsystem : ISubsystem
{
    public static bool TryInitialize(out XshmSubsystem instance)
    {
        if (XshmCapture.NumScreens() > 0)
        {
            instance = new XshmSubsystem();
            return true;
        }

        instance = null!;
        return false;
    }

    public void CreateScreens()
    {
        Console.WriteLine("X11 desktop detected.");
        for (var s = 0; s < XshmCapture.NumScreens(); s++)
        {
            var output = new BaseOutput(s);
            var screen = new DesktopOverlay(output, new XshmCapture(output));
            OverlayRegistry.Register(screen);
        }
    }

    public void Initialize()
    {
    }

    public void Update()
    {
        XshmCapture.ResetMouse();
    }

    public void Dispose()
    {
    }
}