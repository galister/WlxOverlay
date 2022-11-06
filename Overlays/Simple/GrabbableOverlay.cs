using X11Overlay.Core;
using X11Overlay.Numerics;

namespace X11Overlay.Overlays.Simple;

/// <summary>
/// An interactable overlay that exists in world space and can be moved by grabbing.
/// </summary>
public class GrabbableOverlay : InteractableOverlay
{
    /// <summary>
    /// Max distance to manually place the overlay at.
    /// </summary>
    private const float FarDistance = 15f;
    /// <summary>
    /// Min distance to manually place the overlay at.
    /// </summary>
    private const float NearDistance = 0.35f;
    /// <summary>
    /// Far distance beyond which the overlay will be placed back at SpawnPosition on Show()
    /// </summary>
    private const float FarResetDistance = 20f;
    /// <summary>
    /// Near distance beyond which the overlay will be placed back at SpawnPosition on Show()
    /// </summary>
    private const float NearResetDistance = 0.2f;

    protected bool SnapUpright;
    protected bool CurveWhenUpright;
    
    /// <summary>
    /// Default spawn point, relative to HMD
    /// </summary>
    public Vector3 SpawnPosition = Vector3.Forward;
    
    private Vector3 _savedSpawnPosition;
    
    public GrabbableOverlay(string key) : base(key)
    {
    }
    
    public override void Show()
    {
        var len = _savedSpawnPosition.Length();
        if (len is > FarResetDistance or < NearResetDistance)
            _savedSpawnPosition = SpawnPosition;

        var globalRef = InputManager.HmdTransform.TranslatedLocal(_savedSpawnPosition);
        
        Transform.origin = globalRef.origin;
        OnOrientationChanged();
        
        base.Show();
    }

    public override void ResetPosition()
    {
        _savedSpawnPosition = SpawnPosition;
        var globalRef = InputManager.HmdTransform.TranslatedLocal(SpawnPosition);
        Transform.origin = globalRef.origin;
        OnOrientationChanged();
    }

    private Vector3 _grabOffset;
    protected internal void OnGrabbed(PointerHit hitData)
    {
        if (PrimaryPointer != null && hitData.pointer != PrimaryPointer)
            PrimaryPointer.OnPrimaryLost(this);
        
        PrimaryPointer = hitData.pointer;
        
        _grabOffset = PrimaryPointer.HandTransform.AffineInverse() * Transform.origin;
    }

    protected internal void OnGrabHeld()
    {
        Transform.origin = PrimaryPointer!.HandTransform.TranslatedLocal(_grabOffset).origin;
        OnOrientationChanged();
    }

    protected internal void OnDropped()
    {
        _savedSpawnPosition = InputManager.HmdTransform.AffineInverse() * Transform.origin;
    }
    
    protected internal virtual void OnClickWhileHeld()
    {
        OnGrabHeld();
    }
    
    protected internal virtual void OnAltClickWhileHeld()
    {
        OnGrabHeld();
    }

    protected internal void OnScrollSize(float value)
    {
        if (LocalScale.Length() < NearDistance && value > 0
            || LocalScale.Length() > FarDistance && value < 0)
            return;
        
        LocalScale *= Vector3.One - Vector3.One * Mathf.Pow(value, 3) * 2;
    }

    protected internal void OnScrollDistance(float value)
    {
        var newGrabOffset = _grabOffset + _grabOffset.Normalized() * Mathf.Pow(value, 3);

        var distance = newGrabOffset.Length();
        
        if (distance < 0.3f && value < 0
            || distance > 10f && value > 0)
            return;

        _grabOffset = newGrabOffset;
    }
    
    private void OnOrientationChanged()
    {
        var tHmd = InputManager.HmdTransform;
        var vRela = Transform.origin - tHmd.origin;
        var lookPoint = Transform.Translated(vRela).origin;

        if (SnapUpright)
        {
            lookPoint.y = Transform.origin.y;
            Transform = Transform.LookingAt(lookPoint, Vector3.Up).ScaledLocal(LocalScale);

            if (CurveWhenUpright)
            {
                Curvature = 0.2f;
                UploadCurvature();
                UploadTransform();
                return;
            }
        }
        else
        {
            Vector3 upDirection;
            if (Mathf.Abs(tHmd.basis.x.Dot(Vector3.Up)) > 0.2f)
                upDirection = tHmd.basis.y;
            else
            {
                var dot = vRela.Normalized().Dot(tHmd.basis.z);
                var zDist = lookPoint.Length();

                var yDist = Mathf.Abs(Transform.origin.y - InputManager.HmdTransform.origin.y);
                var xAngle = Mathf.Asin(yDist / lookPoint.Length());

                if (dot < -float.Epsilon) // facing downwards
                {
                    var upPoint = InputManager.HmdTransform.origin + zDist / Mathf.Cos(xAngle) * Vector3.Up;
                    upDirection = (upPoint - Transform.origin).Normalized();
                }
                else if (dot > float.Epsilon) // facing upwards
                {
                    var downPoint = InputManager.HmdTransform.origin + zDist / Mathf.Cos(xAngle) * Vector3.Down;
                    upDirection = (Transform.origin - downPoint).Normalized();
                }
                else // perfectly upright
                    upDirection = Vector3.Up;
            }

            Transform = Transform.LookingAt(lookPoint, upDirection).ScaledLocal(LocalScale);
        }

        if (Curvature > float.Epsilon)
        {
            Curvature = 0;
            UploadCurvature();
        }
        UploadTransform();
    }
}