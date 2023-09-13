namespace WlxOverlay.Core.Interactions;

public interface IGrabbable
{
    public void OnGrabbed(PointerHit hitData);

    public void OnDropped();
    public void OnClickWhileHeld();

    public void OnAltClickWhileHeld();
}