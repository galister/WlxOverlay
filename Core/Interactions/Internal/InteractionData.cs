using WlxOverlay.Backend;
using WlxOverlay.Numerics;
using WlxOverlay.Overlays;

namespace WlxOverlay.Core.Interactions.Internal;

internal class InteractionData
{
    public readonly BaseOverlay Overlay;
    public readonly List<PointerHit> HitsThisFrame = new(2);

    public PointerData? PrimaryPointer;

    public InteractionData(BaseOverlay overlay)
    {
        Overlay = overlay;
    }

    public void Begin()
    {
        HitsThisFrame.Clear();
    }

    public bool TestInteraction(PointerData pointer, out PointerHit hitData)
    {
        if (Overlay._overlay != null)
        {
            var ok = Overlay._overlay.TestInteraction(pointer.Pointer, out hitData);
            if (ok)
                hitData.modifier = pointer.Mode;
            return ok;
        }

        hitData = null!;
        return false;
    }

    #region Pointer Interaction
    private void EnsurePrimary(PointerData data)
    {
        if (PrimaryPointer != null)
        {
            if (PrimaryPointer.Pointer == data.Pointer)
                return;

            PrimaryPointer.TryDrop(this);
        }

        PrimaryPointer = data;
    }

    internal void OnPointerHover(PointerData pointer, PointerHit hitData)
    {
        PrimaryPointer ??= pointer;
        HitsThisFrame.Add(hitData);

        hitData.isPrimary = PrimaryPointer.Pointer == pointer.Pointer;

        if (Overlay is IInteractable interactable)
            interactable.OnPointerHover(hitData);
    }

    internal void OnPointerLeft(LeftRight hand)
    {
        if (PrimaryPointer?.Pointer.Hand == hand)
            PrimaryPointer = null;

        if (Overlay is IInteractable interactable)
            interactable.OnPointerLeft(hand);
    }

    internal void OnPointerDown(PointerData pointer, PointerHit hitData)
    {
        EnsurePrimary(pointer);
        hitData.isPrimary = true;

        if (Overlay is IInteractable interactable)
            interactable.OnPointerDown(hitData);
    }

    internal void OnPointerUp(PointerHit hitData)
    {
        if (Overlay is IInteractable interactable)
            interactable.OnPointerUp(hitData);
    }

    internal void OnScroll(PointerHit hitData, float value)
    {
        if (Overlay is IInteractable interactable)
            interactable.OnScroll(hitData, value);
    }
    #endregion

    #region Grab Interaction

    /// <summary>
    /// Default spawn point, relative to HMD
    /// </summary>
    private Vector3 _grabOffset;

    internal void OnGrabbed(PointerData pointer, PointerHit hitData)
    {
        if (PrimaryPointer != null && pointer != PrimaryPointer)
            PrimaryPointer.TryDrop(this);

        PrimaryPointer = pointer;

        _grabOffset = PrimaryPointer.Pointer.Transform.AffineInverse() * Overlay.Transform.origin;

        if (Overlay is IGrabbable grabbable)
            grabbable.OnGrabbed(hitData);
    }

    internal void OnGrabHeld()
    {
        if (PrimaryPointer == null)
            return;
        Overlay.Transform.origin = PrimaryPointer!.Pointer.Transform.TranslatedLocal(_grabOffset).origin;
        Overlay.OnOrientationChanged();
    }

    internal void OnDropped()
    {
        Overlay.SavedSpawnPosition = XrBackend.Current.Input.HmdTransform.AffineInverse() * Overlay.Transform.origin;

        if (Overlay is IGrabbable grabbable)
            grabbable.OnDropped();
    }

    internal void OnClickWhileHeld()
    {
        OnGrabHeld();

        if (Overlay is IGrabbable grabbable)
            grabbable.OnClickWhileHeld();
    }

    internal void OnAltClickWhileHeld()
    {
        OnGrabHeld();

        if (Overlay is IGrabbable grabbable)
            grabbable.OnAltClickWhileHeld();
    }

    internal void OnScrollSize(float value)
    {
        var oldScale = Overlay.LocalScale;
        Overlay.LocalScale *= Vector3.One - Vector3.One * Mathf.Pow(value, 3) * 2;
        if (Overlay.LocalScale.x < 0.35f)
            Overlay.LocalScale = oldScale;
        if (Overlay.LocalScale.x > 10f)
            Overlay.LocalScale = oldScale;
    }

    internal void OnScrollDistance(float value)
    {
        var newGrabOffset = _grabOffset + _grabOffset.Normalized() * Mathf.Pow(value, 3);

        var distance = newGrabOffset.Length();

        if (distance < 0.3f && value < 0
            || distance > 10f && value > 0)
            return;

        _grabOffset = newGrabOffset;
    }

    #endregion
}