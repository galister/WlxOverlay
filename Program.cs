using System.Runtime.InteropServices;
using WlxOverlay.Core;
using WlxOverlay.Desktop;
using WlxOverlay.Desktop.Wayland;
using WlxOverlay.Desktop.X11;
using WlxOverlay.GFX.OpenGL;
using WlxOverlay.Overlays;
using WlxOverlay.Overlays.Simple;
using WlxOverlay.Types;

var version = "unknown-version";
try
{
    version = File.ReadAllText("Resources/version.txt");
}
catch { /* */ }

Console.WriteLine($"WlxOverlay {version}");

if (!Config.Load())
    return;

var manager = OverlayManager.Initialize();
KeyboardProvider.Instance = new UInput();

void SignalHandler(PosixSignalContext context)
{
    context.Cancel = true;
    manager.Stop();
}

PosixSignalRegistration.Create(PosixSignal.SIGINT, SignalHandler);
PosixSignalRegistration.Create(PosixSignal.SIGHUP, SignalHandler);
PosixSignalRegistration.Create(PosixSignal.SIGTERM, SignalHandler);

ManifestInstaller.EnsureInstalled("galister.wlxoverlay");

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

var screens = new List<BaseOverlay>();
if (Environment.GetEnvironmentVariable("XDG_SESSION_TYPE") == "wayland")
{
    Console.WriteLine("Wayland detected.");
    EGL.Initialize();
    WaylandInterface.Initialize();
    foreach (var output in WaylandInterface.Instance!.Outputs.Values)
    {
        BaseWaylandScreen screen;
        if (Config.Instance.WaylandCapture == "dmabuf")
            screen = new WlDmaBufScreen(output);
        else
            screen = new WlScreenCopyScreen(output);

        screen.WantVisible = output.Name == Config.Instance.DefaultScreen;
        manager.RegisterChild(screen);
        screens.Add(screen);
    }

}
else
{
    Console.WriteLine("X11 desktop detected.");
    var numScreens = XScreenCapture.NumScreens();
    for (var s = 0; s < numScreens; s++)
    {
        var screen = new XorgScreen(s) { WantVisible = s.ToString() == Config.Instance.DefaultScreen };
        manager.RegisterChild(screen);
        screens.Add(screen);
    }
}

if (!KeyboardLayout.Load())
{
    Console.WriteLine("[Fatal] Keyboard layout is invalid.");
    Environment.Exit(1);
}

var keyboard = new KeyboardOverlay();
manager.RegisterChild(keyboard);

foreach (var screen in screens)
    manager.RegisterChild(screen);

manager.RegisterChild(new Watch(keyboard, screens));

var engine = new GlGraphicsEngine();
engine.StartEventLoop();
