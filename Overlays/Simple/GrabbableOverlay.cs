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
        Transform = Transform.LookingAt(InputManager.HmdTransform.origin, Transform.basis.y);
        
        base.Show();
    }
    
    private Vector3 _grabOffset;
    protected internal void OnGrabbed(PointerHit hitData)
    {
        if (PrimaryPointer != null && hitData.pointer != PrimaryPointer)
            PrimaryPointer.OnPrimaryLost(this);
        
        PrimaryPointer = hitData.pointer;
        
        _grabOffset = Transform.origin - hitData.pointer.Transform.origin;
    }

    protected internal void OnGrabHeld()
    {
        Transform.origin = PrimaryPointer!.Transform.TranslatedLocal(_grabOffset).origin;
        LookAtHmd();
        UploadTransform();
    }

    protected internal void OnDropped()
    {
        var globalRef = InputManager.HmdTransform.TranslatedLocal(SpawnPosition);
        
        _savedSpawnPosition = (Transform.origin - globalRef.origin) * globalRef;
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
        OnGrabHeld();
    }

    protected internal void OnScrollDistance(float value)
    {
        var t = Transform.TranslatedLocal(Vector3.Forward * (Mathf.Pow(value, 3)));
        
        var newGrabOffset = t.origin - PrimaryPointer!.Transform.origin;
        var distance = (t.origin - InputManager.HmdTransform.origin).Length();
        
        if (distance < 0.3f && value < 0
            || distance > 10f && value > 0)
            return;

        _grabOffset = newGrabOffset;
        OnGrabHeld();
    }
}