using WlxOverlay.Extras;
using WlxOverlay.Overlays;

namespace WlxOverlay.Core.Interactions.Internal;

internal static class InteractionsHandler
{
    private static readonly List<PointerData> _pointers = new(2);
    private static readonly List<PointerData> _handPointers = new(2) { null!, null! };

    private static readonly List<InteractionData> _interactables = new();
    private static readonly List<InteractionData> _workInteractables = new();
    private static readonly object _interactableLock = new();

    private static readonly Dictionary<string, Func<InteractionArgs, InteractionResult>> _customInteractions = new();
    private static readonly List<Func<InteractionArgs, InteractionResult>> _workInteractions = new();
    private static readonly object _interactionLock = new();

    private static bool _showHideState;

    /// <summary>
    /// Register pointers to be used for interactions. Must be called before MainLoop start.
    /// </summary>
    internal static void RegisterPointers(IPointer primary, IPointer secondary)
    {
        _pointers.Add(new PointerData(primary));
        _pointers.Add(new PointerData(secondary));
        foreach (var pointer in _pointers)
            _handPointers[(int)pointer.Pointer.Hand] = pointer;
    }

    internal static void RegisterCustomInteraction(string name, Func<InteractionArgs, InteractionResult> interaction)
    {
        lock (_interactionLock)
            _customInteractions.Add(name, interaction);
    }

    internal static void UnregisterCustomInteraction(string name)
    {
        lock (_interactionLock)
            _customInteractions.Remove(name);
    }

    internal static void TryRegister(BaseOverlay overlay)
    {
        if (overlay is not (IInteractable or IGrabbable)) return;
        lock (_interactableLock)
            _interactables.Add(new InteractionData(overlay));
    }

    internal static void TryUnregister(BaseOverlay overlay)
    {
        lock (_interactableLock)
            _interactables.RemoveAll(x => x.Overlay == overlay);
    }

    internal static void Update()
    {
        _workInteractables.Clear();
        lock (_interactableLock)
            _workInteractables.AddRange(_interactables);

        foreach (var pointer in _pointers)
        {
            pointer.UpdateState();
            if (pointer.Before.Click && !pointer.Now.Click)
                pointer.HandleRelease();
            if (!pointer.Before.ShowHide && pointer.Now.ShowHide)
                ShowHide();
            if (pointer.Now.SpaceDrag)
                PlaySpaceMover.OnSpaceDrag(pointer.Pointer.Transform.origin, pointer.Before.SpaceDrag);
        }

        foreach (var interactable in _workInteractables)
            interactable.Begin();

        foreach (var pointer in _pointers)
        {
            var minDistance = float.MaxValue;
            var minData = (InteractionData?)null;
            var minHit = (PointerHit?)null;

            foreach (var data in _workInteractables)
            {
                if (!data.TestInteraction(pointer, out var hitData)) continue;
                if (hitData.distance > minDistance) continue;
                minDistance = hitData.distance;
                minData = data;
                minHit = hitData;
            }

            if (minHit != null)
            {
                pointer.Pointer.SetLength(minDistance);
                pointer.Pointer.SetColor(IPointer.ModeColors[(int)pointer.Mode]);
                pointer.HandlePointerHit(minHit, minData!);
            }
            else
            {
                pointer.Pointer.SetLength(0);
                pointer.HandleNoHit();
                HandleCustomInteractions(pointer);
            }
        }
    }

    private static void HandleCustomInteractions(PointerData data)
    {
        _workInteractions.Clear();
        lock (_interactionLock)
            _workInteractions.AddRange(_customInteractions.Values);

        foreach (var func in _workInteractions)
        {
            var args = new InteractionArgs
            {
                Hand = data.Pointer.Hand,
                Mode = data.Mode,
                HandTransform = data.Pointer.Transform,
                Now = data.Now,
                Before = data.Before
            };

            var result = func.Invoke(args);
            if (!result.Handled)
                continue;

            if (result.Length > float.Epsilon)
                data.Pointer.SetLength(result.Length);
            if (result.Color.Length() > float.Epsilon)
                data.Pointer.SetColor(result.Color);
            break;
        }
    }

    /// <summary>
    /// Do not call from inside another ReleaseAction
    /// </summary>
    public static void AddPointerReleaseAction(LeftRight hand, Action action)
    {
        var data = _handPointers[(int)hand];
        data.ReleaseActions.Enqueue(action);
    }

    private static void ShowHide()
    {
        _showHideState = !_showHideState;
        OverlayRegistry.Execute(overlay =>
        {
            if (overlay is Watch w)
                w.Hidden = false;
            else if (overlay.ShowHideBinding)
            {
                if (!_showHideState && overlay.Visible)
                    overlay.Hide();
                else if (_showHideState && overlay is { Visible: false, WantVisible: true })
                    overlay.Show();
            }
        });
    }
}