using Valve.VR;
using X11Overlay.Numerics;

namespace X11Overlay.Overlays.Simple;

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

    public bool TryTransformToLocal(Vector2 uvIn, out Vector2 uvOut)
    {
        var uv = InvInteractionTransform * uvIn;
        if (uv.x is < 0f or > 1f
            || uv.y is < 0f or > 1f)
        {
            uvOut = default;
            return false;
        }

        uvOut = uv;
        return true;
    }

    protected Transform3D CurvedSurfaceTransformFromUv(Vector2 localUv)
    {
        var ovrUv = InteractionTransform * localUv - new Vector2(0.5f, 0.5f);
        
        var tCursor =  Transform.TranslatedLocal(new Vector3(WidthInMeters * ovrUv.x, WidthInMeters * ovrUv.y, 0));

        if (Mathf.Abs(Curvature) < float.Epsilon)
            return tCursor;
        
        var theta = Mathf.Pi * 4f * Curvature;
        var halfTheta = theta / 2f;
        var r = WidthInMeters * 2 / theta;
        
        var tOrigin = Transform.TranslatedLocal(Vector3.Back * r);
        tOrigin.origin.y = tCursor.origin.y;

        var offsetAngle = ovrUv.x * halfTheta;
        tCursor = tOrigin.RotatedLocal(Vector3.Up, -offsetAngle)
            .TranslatedLocal(Vector3.Forward * r);
        
        return tCursor;
    }
}