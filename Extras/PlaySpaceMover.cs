using WlxOverlay.Backend;
using WlxOverlay.Core;
using WlxOverlay.Core.Interactions;
using WlxOverlay.Numerics;
using WlxOverlay.Types;

namespace WlxOverlay.Extras;

public static class PlaySpaceMover
{
    private static bool _canDrag;
    private static Vector3 _offset;

    public static bool CanDrag => _canDrag;

    private static Vector3 _startPosition;
    public static void OnSpaceDrag(Vector3 handPos, bool spaceDragBefore)
    {
        if (!CanDrag)
            return;

        if (spaceDragBefore)
            ApplyOffsetRelative(handPos - _startPosition);
        else
            _startPosition = handPos;
    }

    private static void ApplyToOverlays(Vector3 offset)
    {
        OverlayRegistry.Execute(o =>
        {
            if (o is IGrabbable)
                return;
            o.Transform.origin += offset;
            o.UploadTransform();
        });
    }

    private static void ApplyOffsetRelative(Vector3 relativeMovement)
    {
        ApplyToOverlays(relativeMovement * -1f);

        _offset += relativeMovement;
        Apply();
        _canDrag = false;
    }

    public static void ResetOffset()
    {
        var moveAmount = Session.Instance.PlaySpaceOffset - _offset;
        ApplyToOverlays(moveAmount * -1f);

        _offset = Session.Instance.PlaySpaceOffset;
        Apply();
    }

    public static void SetAsDefault()
    {
        Session.Instance.PlaySpaceOffset = _offset;
        Session.Instance.Persist();
    }

    public static void FixFloor()
    {
        var left = XrBackend.Current.Input.HandTransform(LeftRight.Left).origin;
        var right = XrBackend.Current.Input.HandTransform(LeftRight.Right).origin;

        var floorY = Mathf.Min(left.y, right.y);
        ApplyToOverlays(floorY * -1f * Vector3.Up);

        _offset.y += floorY;
        Apply();
    }

    public static void EndFrame()
    {
        _canDrag = true;
    }

    private static void Apply()
    {
        XrBackend.Current.SetZeroPose(_offset);
    }
}
