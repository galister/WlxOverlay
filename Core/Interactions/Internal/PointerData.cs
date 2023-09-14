using WlxOverlay.Backend;
using WlxOverlay.Numerics;
using WlxOverlay.Types;
using Action = System.Action;

namespace WlxOverlay.Core.Interactions.Internal;

internal class PointerData
{
    public readonly IPointer Pointer;

    public readonly Queue<Action> ReleaseActions = new();

    public InputState Now;
    public InputState Before;

    public PointerMode Mode;

    private InteractionData? _grabbedTarget;

    private InteractionData? _lastData;

    internal PointerData(IPointer pointer)
    {
        Pointer = pointer;
    }

    internal void UpdateState()
    {
        Before = Now;
        XrBackend.Current.Input.InputState(Pointer.Hand, ref Now);

#if DEBUG_INPUT
        if (Before.Click != Now.Click)
            Console.WriteLine($"[Dbg] {Pointer.Hand} Click {Before.Click} -> {Now.Click}");
        if (Before.Grab != Now.Grab)
            Console.WriteLine($"[Dbg] {Pointer.Hand} Grab {Before.Grab} -> {Now.Grab}");
        if (Before.AltClick != Now.AltClick)
            Console.WriteLine($"[Dbg] {Pointer.Hand} AltClick {Before.AltClick} -> {Now.AltClick}");
        if (Before.ShowHide != Now.ShowHide)
            Console.WriteLine($"[Dbg] {Pointer.Hand} ShowHide {Before.ShowHide} -> {Now.ShowHide}");
        if (Before.SpaceDrag != Now.SpaceDrag)
            Console.WriteLine($"[Dbg] {Pointer.Hand} SpaceDrag {Before.SpaceDrag} -> {Now.SpaceDrag}");
        if (Math.Abs(Before.Scroll - Now.Scroll) > float.Epsilon)
            Console.WriteLine($"[Dbg] {Pointer.Hand} Scroll {Before.Scroll:F} -> {Now.Scroll:F}");
        if (Before.ClickModifierMiddle != Now.ClickModifierMiddle)
            Console.WriteLine($"[Dbg] {Pointer.Hand} ClickModifierMiddle {Before.ClickModifierMiddle} -> {Now.ClickModifierMiddle}");
        if (Before.ClickModifierRight != Now.ClickModifierRight)
            Console.WriteLine($"[Dbg] {Pointer.Hand} ClickModifierRight {Before.ClickModifierRight} -> {Now.ClickModifierRight}");
#endif

        // Recalculate Mode
        if (Now.ClickModifierRight)
        {
            Mode = PointerMode.Right;
            return;
        }

        if (Now.ClickModifierMiddle)
        {
            Mode = PointerMode.Middle;
            return;
        }

        var hmdUp = XrBackend.Current.Input.HmdTransform.basis.y;
        var dot = hmdUp.Dot(Pointer.Transform.basis.x) * (1 - 2 * (int)Pointer.Hand);

        Mode = dot switch
        {
            < -0.85f => PointerMode.Right,
            > 0.7f => PointerMode.Middle,
            _ => PointerMode.Left
        };

        if (Mode == PointerMode.Middle && !Now.Grab && !Config.Instance.MiddleClickOrientation)
            Mode = PointerMode.Left;
        else if (Mode == PointerMode.Right && !Config.Instance.RightClickOrientation)
            Mode = PointerMode.Left;
    }

    internal void HandlePointerHit(PointerHit hitData, InteractionData data)
    {
        if (_grabbedTarget != null)
        {
            HandleGrabbedInteractions();
            return;
        }

        if (_lastData?.Overlay != data.Overlay)
        {
            _lastData?.OnPointerLeft(Pointer.Hand);
            _lastData = data;
        }

        if (Now.Grab && !Before.Grab && data.Overlay is IGrabbable)
        {
            data.OnGrabbed(this, hitData);
            _grabbedTarget = data;
            return;
        }

        data.OnPointerHover(this, hitData);

        if (Now.Click && !Before.Click)
            data.OnPointerDown(this, hitData);
        else if (!Now.Click && Before.Click)
            data.OnPointerUp(hitData);

        if (Mathf.Abs(Now.Scroll) > 0.1f)
            data.OnScroll(hitData, Now.Scroll);
    }

    internal void HandleNoHit()
    {
        if (_lastData == null)
            return;

        _lastData.OnPointerLeft(Pointer.Hand);
        _lastData = null;
    }

    internal void HandleRelease()
    {
        while (ReleaseActions.TryDequeue(out var action))
            action.Invoke();
    }

    /// <summary>
    /// Runs on the overlay that's being grabbed.
    /// </summary>
    private void HandleGrabbedInteractions()
    {
        if (!Now.Grab)
        {
            _grabbedTarget!.OnDropped();
            _grabbedTarget = null;
            return;
        }
        if (Mathf.Abs(Now.Scroll) > 0.1f)
        {
            if (Mode == PointerMode.Middle)
                _grabbedTarget!.OnScrollSize(Now.Scroll);
            else
                _grabbedTarget!.OnScrollDistance(Now.Scroll);
        }

        if (Now.Click && !Before.Click)
        {
            _grabbedTarget!.OnClickWhileHeld();
        }
        if (Now.AltClick && !Before.AltClick)
        {
            _grabbedTarget!.OnAltClickWhileHeld();
        }
        else
            _grabbedTarget!.OnGrabHeld();
    }

    internal void TryDrop(InteractionData data)
    {
        if (_grabbedTarget == data)
            _grabbedTarget = data;
    }
}
