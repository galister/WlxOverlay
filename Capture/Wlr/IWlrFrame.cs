using WlxOverlay.GFX;

namespace WlxOverlay.Capture.Wlr;

public enum CaptureStatus
{
    Pending,
    FrameReady,
    FrameSkipped,
    Fatal
}

public interface IWlrFrame : IDisposable
{
    CaptureStatus GetStatus();
    void ApplyToTexture(ITexture texture);
}