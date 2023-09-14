using WlxOverlay.Backend;
using WlxOverlay.Core;
using WlxOverlay.Core.Interactions;
using WlxOverlay.Core.Subsystem;
using WlxOverlay.Extras;
using WlxOverlay.GFX.OpenGL;
using WlxOverlay.Input;
using WlxOverlay.Overlays;
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

if (Config.Instance.OverrideEnv != null)
    foreach (var pair in Config.Instance.OverrideEnv)
        Environment.SetEnvironmentVariable(pair.Key, pair.Value);

Session.Initialize();

if (args.Contains("--xr"))
    XrBackend.UseOpenXR();
else
    XrBackend.UseOpenVR();
InputProvider.UseUInput();

void SignalHandler(PosixSignalContext context)
{
    context.Cancel = true;
    Console.WriteLine($"Received signal {context.Signal}. Exiting...");
    MainLoop.Shutdown();
}

PosixSignalRegistration.Create(PosixSignal.SIGINT, SignalHandler);
PosixSignalRegistration.Create(PosixSignal.SIGHUP, SignalHandler);
PosixSignalRegistration.Create(PosixSignal.SIGTERM, SignalHandler);

if (Config.Instance.LeftUsePtt)
    PttHandler.Add(LeftRight.Left);

if (Config.Instance.RightUsePtt)
    PttHandler.Add(LeftRight.Right);

if (!KeyboardLayout.Load())
{
    Console.WriteLine("[Fatal] Keyboard layout is invalid.");
    Environment.Exit(1);
}

var keyboard = new KeyboardOverlay();
OverlayRegistry.Register(keyboard);

if (WaylandSubsystem.TryInitialize(out var wayland))
{
    MainLoop.AddSubsystem(wayland);
    await wayland.CreateScreensAsync();
}
else if (XshmSubsystem.TryInitialize(out var xshm))
{
    MainLoop.AddSubsystem(xshm);
    xshm.CreateScreens();
}

AudioManager.Initialize();

if (!string.IsNullOrWhiteSpace(Config.Instance.NotificationsEndpoint))
    NotificationsManager.Initialize();

var watch = new Watch(keyboard);
OverlayRegistry.Register(watch);

var engine = new GlGraphicsEngine();
engine.StartEventLoop();
