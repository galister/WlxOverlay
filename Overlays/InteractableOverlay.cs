using System.Runtime.InteropServices;
using Valve.VR;
using X11Overlay.Types;

namespace X11Overlay.Overlays;

/// <summary>
/// Base class for all overlays supporting pointer events.
/// </summary>
public abstract class InteractableOverlay : BaseOverlay
{
    internal LaserPointer? PrimaryPointer;

    /// <summary>
    /// Transforms texture UV (rect) to overlay UV (square)
    /// </summary>
    protected Transform2D InteractionTransform;
    
    /// <summary>
    /// Transforms overlay UV (square) to texture UV (rect) 
    /// </summary>
    protected Transform2D InvInteractionTransform;

    protected InteractableOverlay(string key) : base(key) { }
    
    protected void UpdateInteractionTransform()
    {
        if (Texture == null)
            return;

        var w = Texture.GetWidth();
        var h = Texture.GetHeight();

        InteractionTransform = Transform2D.Identity;
        if (w > h)
        {
            InteractionTransform.y *= h / (float)w;
            InteractionTransform.origin = Vector2.Down * ((w - h) * 0.5f / w);
        }
        else if (h > w)
        {
            InteractionTransform.x *= w / (float)h;
            InteractionTransform.origin = Vector2.Right * ((h - w) * 0.5f / h);
        }

        InvInteractionTransform = InteractionTransform.AffineInverse();
    }

    protected void EnsurePrimary(LaserPointer pointer)
    {
        if (PrimaryPointer != null)
        {
            if (PrimaryPointer == pointer)
                return;
            
            PrimaryPointer.OnPrimaryLost(this);
        }

        PrimaryPointer = pointer;
    }

    protected internal virtual void OnPointerHover(PointerHit hitData)
    {
        PrimaryPointer ??= hitData.pointer;
    }

    protected internal virtual void OnPointerLeft(LeftRight hand)
    {
        if (PrimaryPointer?.Hand == hand)
            PrimaryPointer = null;

    }

    protected internal virtual void OnPointerDown(PointerHit hitData)
    {
        EnsurePrimary(hitData.pointer);
        
    }

    protected internal virtual void OnPointerUp(PointerHit hitData)
    {
        
    }

    protected internal virtual void OnScroll(PointerHit hitData, float value)
    {
        
    }

    private HmdVector2_t _vector2;
    protected (Vector3 origin, Vector3 center) CurvedSurfacePositionFromUv(Vector2 uv)
    {
        var xFormedUv = InteractionTransform * uv;
        xFormedUv.CopyTo(ref _vector2);
        
        var err = OpenVR.Overlay.GetTransformForOverlayCoordinates(Handle, ETrackingUniverseOrigin.TrackingUniverseStanding, _vector2, ref HmdMatrix);
        if (err != EVROverlayError.None)
            Console.WriteLine($"[Err] GetTransformForOverlayCoordinates: " + OpenVR.Overlay.GetOverlayErrorNameFromEnum(err));

        var transform = HmdMatrix.ToTransform3D();
        if (Mathf.Abs(Curvature) < float.Epsilon)
            return (transform.origin, Transform.basis.z);

        var uvFromCenter = xFormedUv - new Vector2(0.5f, 0.5f);

        var curveFactor = Mathf.Sin(uvFromCenter.Length() * (WidthInMeters * Curvature / Transform.basis.x.Length()) * Mathf.Pi);
        transform = transform.TranslatedLocal(Vector3.Back * curveFactor);
        
        Console.WriteLine($"CurveFactor: {curveFactor:F}");
        
        var sphereOrigin = Transform.TranslatedLocal(Vector3.Back * WidthInMeters * Curvature / Transform.basis.x.Length()).origin;

        return (transform.origin, sphereOrigin);
    }
}