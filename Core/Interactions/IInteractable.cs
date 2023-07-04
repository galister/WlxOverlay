namespace WlxOverlay.Core.Interactions;

public interface IInteractable
{
    public void OnPointerHover(PointerHit hitData);

    public void OnPointerLeft(LeftRight hand);

    public void OnPointerDown(PointerHit hitData);

    public void OnPointerUp(PointerHit hitData);

    public void OnScroll(PointerHit hitData, float value);
}