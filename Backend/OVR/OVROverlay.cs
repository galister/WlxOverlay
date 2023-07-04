using OVRSharp;
using Valve.VR;
using WlxOverlay.Core.Interactions;
using WlxOverlay.GFX;
using WlxOverlay.Numerics;
using WlxOverlay.Overlays;

namespace WlxOverlay.Backend.OVR;

public sealed class OVROverlay : IOverlay
{
    private string _key;

    private BaseOverlay _parent;
    
    private Overlay? _overlay;
    private Overlay? _childOverlay;
    
    private bool _created;
    private bool _textureUploaded;
    
    /// <summary>
    /// Transforms texture UV (rect) to overlay UV (square)
    /// </summary>
    private Transform2D InteractionTransform;

    /// <summary>
    /// Transforms overlay UV (square) to texture UV (rect) 
    /// </summary>
    private Transform2D InvInteractionTransform;
    
    private static HmdMatrix34_t HmdMatrix;
    private static VROverlayIntersectionParams_t IntersectionParams = new() { eOrigin = ETrackingUniverseOrigin.TrackingUniverseStanding };
    private static VROverlayIntersectionResults_t IntersectionResults;

    public OVROverlay(BaseOverlay parent)
    {
        _key = parent.Key;
        _parent = parent;
    }

    public ITexture CreateTexture(uint width, uint height)
    {
        return GraphicsEngine.Instance.EmptyTexture(width, height);
    }

    public void SetWidth(float width)
    {
        if (_overlay == null)
            return;
        _overlay.WidthInMeters = width;
        UpdateInteractionTransform();
    }

    public void SetAlpha(float alpha)
    {
        if (_overlay == null)
            return;
        _overlay.Alpha = alpha;
    }

    public void SetColor(Vector3 c)
    {
        if (_overlay == null)
            return;
        var err = OpenVR.Overlay.SetOverlayColor(_overlay.Handle, c.x, c.y, c.z);
        if (err != EVROverlayError.None)
            Console.WriteLine($"[Err] SetOverlayColor {c}: " + OpenVR.Overlay.GetOverlayErrorNameFromEnum(err));
    }

    public void SetTransform(Transform3D transform)
    {
        if (_overlay == null)
            return;
        transform.CopyTo(ref HmdMatrix);
        _overlay.Transform = HmdMatrix;
        if (_childOverlay != null)
            _childOverlay.Transform = HmdMatrix;
    }

    public void SetZOrder(uint zOrder)
    {        
        if (_overlay == null)
            return;
        var err = OpenVR.Overlay.SetOverlaySortOrder(_overlay.Handle, zOrder);
        if (err != EVROverlayError.None)
            Console.WriteLine($"[Err] SetOverlaySortOrder {zOrder}: " + OpenVR.Overlay.GetOverlayErrorNameFromEnum(err));
    }

    public void SetCurvature(float curvature)
    {
        if (_overlay == null)
            return;
        _overlay!.Curvature = curvature;
        if (_childOverlay != null)
            _childOverlay.Curvature = curvature;
    }

    public bool TestInteraction(IPointer pointer, out PointerHit hitData)
    {
        if (_overlay == null)
        {
            hitData = null!;
            return false;
        }

        pointer.Transform.origin.CopyTo(ref IntersectionParams.vSource);
        (-pointer.Transform.basis.z).CopyTo(ref IntersectionParams.vDirection);
        
        var wasHit = OpenVR.Overlay.ComputeOverlayIntersection(_childOverlay?.Handle ?? _overlay!.Handle, ref IntersectionParams, ref IntersectionResults);
        if (!wasHit || !TryTransformToLocal(IntersectionResults.vUVs.ToWlx(), out var localUv))
        {
            hitData = null!;
            return false;
        }

        hitData = new PointerHit(pointer)
        {
            uv = localUv,
            distance = IntersectionResults.fDistance,
            normal = IntersectionResults.vNormal.ToVector3(),
            point = IntersectionResults.vPoint.ToVector3()
        };

        return wasHit;
    }

    public Transform3D UvToWorld(Vector2 uv)
    {
        var ovrUv = InteractionTransform * uv - new Vector2(0.5f, 0.5f);

        var tCursor = _parent.Transform.TranslatedLocal(new Vector3(_parent.WidthInMeters * ovrUv.x, _parent.WidthInMeters * ovrUv.y, 0));

        if (Mathf.Abs(_parent.Curvature) < float.Epsilon)
            return tCursor;

        var theta = Mathf.Pi * 4f * _parent.Curvature;
        var halfTheta = theta / 2f;
        var r = _parent.WidthInMeters * 2 / theta;

        var tOrigin = _parent.Transform.TranslatedLocal(Vector3.Back * r);
        tOrigin.origin.y = tCursor.origin.y;

        var offsetAngle = ovrUv.x * halfTheta;
        tCursor = tOrigin.RotatedLocal(Vector3.Up, -offsetAngle)
            .TranslatedLocal(Vector3.Forward * r);

        return tCursor;
    }

    private bool TryTransformToLocal(Vector2 uvIn, out Vector2 uvOut)
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

    public void Render()
    {
        if (_overlay == null)
            return;
        
        if (_parent.Texture!.IsDynamic() || !_textureUploaded)
        {
            UploadTexture(_overlay, _parent.Texture);
            _textureUploaded = true;
        }
    }

    public void Show()
    {
        if (!_created)
        {
            _overlay = new Overlay(_key, _key);
            _created = true;
        }
        _overlay!.Show();
        _childOverlay?.Show();
    }

    public void Hide()
    {
        if (!_created)
            return;
        
        if (_parent.Texture!.IsDynamic())
        {
            _overlay!.Destroy();
            _overlay = null;
            _created = false;
            _textureUploaded = false;
        }
        else
            _overlay!.Hide();
        _childOverlay?.Hide();
    }
    
    private void UpdateInteractionTransform()
    {
        if (_parent.Texture == null)
            return;

        var w = _parent.Texture.GetWidth();
        var h = _parent.Texture.GetHeight();

        InteractionTransform = Transform2D.Identity;
        if (w > h)
        {
            _childOverlay?.Destroy();
            _childOverlay = null;
            InteractionTransform.y *= h / (float)w;
            InteractionTransform.origin = Vector2.Down * ((w - h) * 0.5f / w);
        }
        else if (h > w)
        {
            if (_childOverlay == null)
            {
                var key = $"{_key}-Interact";
                _childOverlay = new Overlay(key, key)
                {
                    Alpha = 0,
                    WidthInMeters = _parent.WidthInMeters * (h / (float)w),
                    Transform = _overlay!.Transform
                };
                UploadTexture(_childOverlay, GraphicsEngine.Instance.EmptyTexture(1, 1));
            }
            
            InteractionTransform.x *= w / (float)h;
            InteractionTransform.origin = Vector2.Right * ((h - w) * 0.5f / h);
        }

        InvInteractionTransform = InteractionTransform.AffineInverse();
    }

    private Transform3D CurvedSurfaceTransformFromUv(Vector2 localUv)
    {
        var ovrUv = InteractionTransform * localUv - new Vector2(0.5f, 0.5f);

        var tCursor = _parent.Transform.TranslatedLocal(new Vector3(_parent.WidthInMeters * ovrUv.x, _parent.WidthInMeters * ovrUv.y, 0));

        if (Mathf.Abs(_parent.Curvature) < float.Epsilon)
            return tCursor;

        var theta = Mathf.Pi * 4f * _parent.Curvature;
        var halfTheta = theta / 2f;
        var r = _parent.WidthInMeters * 2 / theta;

        var tOrigin = _parent.Transform.TranslatedLocal(Vector3.Back * r);
        tOrigin.origin.y = tCursor.origin.y;

        var offsetAngle = ovrUv.x * halfTheta;
        tCursor = tOrigin.RotatedLocal(Vector3.Up, -offsetAngle)
            .TranslatedLocal(Vector3.Forward * r);

        return tCursor;
    }

    private static void UploadTexture(Overlay overlay, ITexture texture)
    {
        var tex = new Texture_t
        {
            handle = texture!.GetNativeTexturePtr(),
            eType = GraphicsEngine.Instance.GetTextureType(),
            eColorSpace = EColorSpace.Auto
        };

        if (tex.handle == IntPtr.Zero)
        {
            Console.WriteLine("Cannot upload texture: Handle is null.");
            return;
        }

        overlay.SetTexture(tex);
    }

    public void Dispose()
    {
        _overlay?.Destroy();
        _childOverlay?.Destroy();
    }
}