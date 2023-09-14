using Valve.VR;
using WlxOverlay.Backend;
using WlxOverlay.Core.Interactions.Internal;
using WlxOverlay.Core.Subsystem;
using WlxOverlay.Extras;
using WlxOverlay.GFX;

namespace WlxOverlay.Core;

public static class MainLoop
{
    private static readonly List<ISubsystem> _subsystems = new();
    private static bool _running = true;

    public static void AddSubsystem(ISubsystem subsystem)
    {
        _subsystems.Add(subsystem);
    }

    public static void Initialize()
    {
        XrBackend.Current.Initialize();
        foreach (var subsystem in _subsystems)
            subsystem.Initialize();
    }

    public static void Update()
    {
        var should = _running
            ? XrBackend.Current.BeginFrame()
            : LoopShould.Quit;

        if (should == LoopShould.Quit)
        {
            Destroy();
            return;
        }

        if (should == LoopShould.Idle)
        {
            Thread.Sleep(5);
            return;
        }

        while (TaskScheduler.TryDequeue(out var action))
            action();

        if (should == LoopShould.Render)
        {
            foreach (var overlay in OverlayRegistry.MainLoopEnumerate())
                overlay.AfterInput();

            InteractionsHandler.Update();

            // Show overlays that want to be shown
            foreach (var overlay in OverlayRegistry.MainLoopEnumerate())
                if (overlay is { Visible: false, WantVisible: true, ShowHideBinding: false })
                    overlay.Show();

            ChaperoneManager.Instance.Render(); //TODO

            // Render all visible overlays
            foreach (var overlay in OverlayRegistry.MainLoopEnumerate())
                if (overlay.Visible)
                    overlay.Render();

            FontCollection.CloseHandles();
            PlaySpaceMover.EndFrame();
        }

        foreach (var subsystem in _subsystems)
            subsystem.Update();

        XrBackend.Current.EndFrame(should);
    }

    public static void Shutdown()
    {
        _running = false;
    }

    private static void Destroy()
    {
        Console.WriteLine("Shutting down.");

        foreach (var baseOverlay in OverlayRegistry.MainLoopEnumerate())
            baseOverlay.Dispose();

        foreach (var subsystem in _subsystems)
            subsystem.Dispose();

        OpenVR.Shutdown();
        GraphicsEngine.Instance.Shutdown();
    }

}