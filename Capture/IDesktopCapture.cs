using WlxOverlay.GFX;

namespace WlxOverlay.Capture;

public interface IDesktopCapture : IDisposable
{
    void Initialize();
    bool TryApplyToTexture(ITexture texture);

    void Pause();

    void Resume();
}