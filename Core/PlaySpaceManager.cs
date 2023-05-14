using Valve.VR;
using WlxOverlay.Numerics;

namespace WlxOverlay.Core;

public class PlaySpaceManager
{
    public static PlaySpaceManager Instance { get; } = new ();

    private static bool _canDrag;
    private Vector3 _offset;
    private HmdMatrix34_t matrix34_T;

    public bool CanDrag => _canDrag;

    public void ApplyOffsetRelative(Vector3 relativeMovement)
    {
        _offset += relativeMovement;
        Apply();
        _canDrag = false;
    }

    public void ResetOffset()
    {
        _offset = Vector3.Zero;
        Apply();
    }

    public void EndFrame()
    {
        _canDrag = true;
    }

    private void Apply()
    {
        if (!OpenVR.ChaperoneSetup.GetWorkingStandingZeroPoseToRawTrackingPose(ref matrix34_T))
        {
            Console.WriteLine("ERR: Failed to get Zero-Pose");
            return;
        }
        
        var universe = matrix34_T.ToTransform3D();
        universe.origin = _offset;

        universe.CopyTo(ref matrix34_T);
        OpenVR.ChaperoneSetup.SetWorkingStandingZeroPoseToRawTrackingPose(ref matrix34_T);
        OpenVR.ChaperoneSetup.CommitWorkingCopy(EChaperoneConfigFile.Live);
    }
} 
