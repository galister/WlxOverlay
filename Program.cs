using System.Runtime.InteropServices;
using X11Overlay.Core;
using X11Overlay.GFX.OpenGL;
using X11Overlay.Overlays;
using X11Overlay.Overlays.Simple;
using X11Overlay.Screen.X11;
using X11Overlay.Types;

if (!Config.Load())
    return;

var manager = OverlayManager.Initialize();

void SignalHandler(PosixSignalContext context)
{
    context.Cancel = true;
    manager.Stop();
}

PosixSignalRegistration.Create(PosixSignal.SIGINT, SignalHandler);
PosixSignalRegistration.Create(PosixSignal.SIGHUP, SignalHandler);
PosixSignalRegistration.Create(PosixSignal.SIGTERM, SignalHandler);

ManifestInstaller.EnsureInstalled("galister.x11overlay");

if (!Config.Instance.FallbackCursors)
    manager.RegisterChild(new DesktopCursor());

var leftPointer = Config.Instance.LeftUsePtt
    ? new LaserPointerWithPushToTalk(LeftRight.Left)
    : new LaserPointer(LeftRight.Left);

var rightPointer = Config.Instance.RightUsePtt
    ? new LaserPointerWithPushToTalk(LeftRight.Right)
    : new LaserPointer(LeftRight.Right);

if (Config.Instance.PrimaryHand == LeftRight.Left)
{
    manager.RegisterChild(leftPointer);
    manager.RegisterChild(rightPointer);
}
else
{
    manager.RegisterChild(rightPointer);
    manager.RegisterChild(leftPointer);
}

IEnumerable<BaseOverlay> GetScreens()
{
    var numScreens = XScreenCapture.NumScreens();
    for (var s = 0; s < numScreens; s++)
    {
        var screen = new XorgScreen(s) { WantVisible = s == Config.Instance.DefaultScreen };
        manager.RegisterChild(screen);
        yield return screen;
    }
}

if (!KeyboardLayout.Load())
{
    Console.WriteLine("[Fatal] Keyboard layout is invalid.");
    Environment.Exit(1);
}

var keyboard = new KeyboardOverlay();
manager.RegisterChild(keyboard);

manager.RegisterChild(new Watch(keyboard, GetScreens().ToList()));

var engine = new GlGraphicsEngine();
engine.StartEventLoop();