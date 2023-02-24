using WlxOverlay.GFX;

namespace WlxOverlay.Desktop.Wayland.Frame;

public interface IWaylandFrame : IDisposable
{
    CaptureStatus GetStatus();
    void ApplyToTexture(ITexture texture);
}

public enum CaptureStatus
{
    Pending,
    FrameReady,
    FrameSkipped,
    Fatal
}