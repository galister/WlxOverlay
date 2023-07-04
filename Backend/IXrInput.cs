using WlxOverlay.Core.Interactions;
using WlxOverlay.Numerics;

namespace WlxOverlay.Backend;

public interface IXrInput
{
    public event EventHandler BatteryStatesUpdated;
    
    public Transform3D HmdTransform { get; }

    public Transform3D HandTransform(LeftRight hand);
    
    public void InputState(LeftRight hand, ref InputState state);
    
    public void HapticVibration(LeftRight hand, float durationSec, float amplitude, float frequency = 5f);
}