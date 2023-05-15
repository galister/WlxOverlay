using Valve.VR;
using WlxOverlay.Numerics;
using WlxOverlay.Types;

namespace WlxOverlay.Core;

public class PlaySpaceManager
{
    public static PlaySpaceManager Instance { get; } = new ();

    private static bool _canDrag;
    private Vector3 _offset;
    private HmdMatrix34_t matrix34_T;

    public bool CanDrag => _canDrag;

    private PlaySpaceManager()
    {
    }

    public void ApplyOffsetRelative(Vector3 relativeMovement)
    {
        _offset += relativeMovement;
        Apply();
        _canDrag = false;
    }

    public void ResetOffset()
    {
        _offset = Session.Instance.PlaySpaceOffset;
        Apply();
    }

    public void SetAsDefault()
    {
        Session.Instance.PlaySpaceOffset = _offset;
        Session.Instance.Persist();
    }

    public void FixFloor()
    {
        const float PADDING = 0.01f;

        var left = InputManager.PoseState["LeftHand"].origin;
        var right = InputManager.PoseState["RightHand"].origin;

        var floorY = Mathf.Min(left.y, right.y);

        _offset.y += floorY;
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
