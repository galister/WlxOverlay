using WlxOverlay.Numerics;
using WlxOverlay.Overlays;

namespace WlxOverlay.Backend;

public interface IXrBackend
{
    IXrInput Input { get; }
    float DisplayFrequency { get; }

    IList<TrackedDevice> GetBatteryStates();

    void Initialize();

    LoopShould BeginFrame();

    void EndFrame(LoopShould should);

    void SetZeroPose(Vector3 offset);

    void AdjustGain(int ch, float gain);
    
    IOverlay CreateOverlay(BaseOverlay overlay);

    void Destroy();
}

public enum LoopShould
{
    Idle,
    NotRender,
    Render,
    Quit
}