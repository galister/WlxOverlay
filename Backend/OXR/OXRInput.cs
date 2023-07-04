using WlxOverlay.Core.Interactions;
using WlxOverlay.Core.Interactions.Internal;
using WlxOverlay.Numerics;
using WlxOverlay.Types;

namespace WlxOverlay.Backend.OXR;

public class OXRInput : IXrInput
{
    private readonly OXRState _oxr;
    private readonly OXRPointer[] _pointers;
    
    public event EventHandler? BatteryStatesUpdated;
    public Transform3D HmdTransform { get; private set; }

    public OXRInput(OXRState oxr)
    {
        _oxr = oxr;
        
        var left = new OXRPointer(_oxr, LeftRight.Left);
        var right = new OXRPointer(_oxr, LeftRight.Right);
        
        _pointers = Config.Instance.PrimaryHand == LeftRight.Left 
            ? new [] { left, right }
            : new [] { right, left };
        
        InteractionsHandler.RegisterPointers(_pointers[0], _pointers[1]);
    }
    
    public Transform3D HandTransform(LeftRight hand)
    {
        return _pointers[(int)hand].Transform3D;
    }

    public void InputState(LeftRight hand, ref InputState state)
    {
        state = _pointers[(int)hand].State;
    }

    public void HapticVibration(LeftRight hand, float durationSec, float amplitude, float frequency = 5)
    {
        _pointers[(int)hand].ApplyHapticFeedback(durationSec, amplitude, frequency);
    }

    public void Update()
    {
        HmdTransform = new Transform3D(
            _oxr.Views[0].Pose.Orientation.ToWlx(), 
            (_oxr.Views[0].Pose.Position.ToWlx() + _oxr.Views[1].Pose.Position.ToWlx()) * 0.5f
            );
        
        foreach (var pointer in _pointers)
            pointer.Update();
    }

    public void Initialize()
    {
        foreach (var pointer in _pointers)
            pointer.Initialize();
    }
}