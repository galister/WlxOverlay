using WlxOverlay.Core.Interactions.Internal;
using WlxOverlay.Overlays;

namespace WlxOverlay.Core;

public static class OverlayRegistry
{
    private const int MaxOverlays = 32;

    private static readonly List<BaseOverlay> _overlays = new(MaxOverlays);
    private static readonly List<BaseOverlay> _workOverlays = new(MaxOverlays);
    private static readonly object _overlayLock = new();

    public static void Register(BaseOverlay baseOverlay)
    {
        lock (_overlayLock)
            _overlays.Add(baseOverlay);
        InteractionsHandler.TryRegister(baseOverlay);
    }
    public static void Unregister(BaseOverlay baseOverlay)
    {
        lock (_overlayLock)
            _overlays.Remove(baseOverlay);
        InteractionsHandler.TryUnregister(baseOverlay);
    }

    public static IEnumerable<BaseOverlay> MainLoopEnumerate()
    {
        _workOverlays.Clear();
        lock (_overlayLock)
            _workOverlays.AddRange(_overlays);
        foreach (var overlay in _workOverlays)
            yield return overlay;
    }

    public static IReadOnlyList<BaseOverlay> ListOverlays()
    {
        var list = new List<BaseOverlay>();
        lock (_overlayLock)
            list.AddRange(_overlays);
        return list;
    }

    public static void Execute(Action<BaseOverlay> action)
    {
        lock (_overlayLock)
            foreach (var baseOverlay in _overlays)
                action(baseOverlay);
    }
}