using WlxOverlay.Core;
using WlxOverlay.Desktop;
using WlxOverlay.Desktop.Wayland;
using WlxOverlay.GFX.OpenGL;
using WlxOverlay.Overlays;
using WlxOverlay.Overlays.Simple;
using WlxOverlay.Types;

var version = "unknown-version";
try
{
    version = File.ReadAllText(Path.Combine(Config.AppDir, "Resources", "version.txt")).Trim();
}
catch { /* */ }

Console.WriteLine($"WlxOverlay {version}");

if (!Config.Load())
    return;

var manager = OverlayManager.Initialize();
try
{
    KeyboardProvider.Instance = new UInput();
}
catch (ApplicationException)
{
    Console.WriteLine("FATAL Could not register uinput device.");
    Console.WriteLine("FATAL Check that you are in the `input` group or otherwise have access.");
    Console.WriteLine("FATAL Try: sudo usermod -a -G input $USER");
    return;
}

void SignalHandler(PosixSignalContext context)
{
    context.Cancel = true;
    manager.Stop();
}

PosixSignalRegistration.Create(PosixSignal.SIGINT, SignalHandler);
PosixSignalRegistration.Create(PosixSignal.SIGHUP, SignalHandler);
PosixSignalRegistration.Create(PosixSignal.SIGTERM, SignalHandler);

ManifestInstaller.EnsureInstalled();

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

    await foreach (var screen in WaylandInterface.Instance!.CreateScreensAsync())
    {
        manager.RegisterChild(screen);
        screens.Add(screen);
    }
}
else
{
    Console.WriteLine("X11 desktop detected.");
    var numScreens = XorgScreen.NumScreens();
    for (var s = 0; s < numScreens; s++)
    {
        var screen = new XorgScreen(s);
        manager.RegisterChild(screen);
        screens.Add(screen);
    }
}

if (!string.IsNullOrWhiteSpace(Config.Instance.NotificationsEndpoint))
    NotificationsManager.Initialize();

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
