using WlxOverlay.Core.Interactions;
using WlxOverlay.GFX;
using WlxOverlay.Numerics;
using WlxOverlay.Overlays;

namespace WlxOverlay.Backend.OXR;

public class OXROverlay : IOverlay
{
    private Vector2Int _size;

    public float Width { get; private set; }
    public Transform3D Transform { get; private set; }
    public Vector3 Color { get; private set; } = Vector3.One;
    public float Alpha { get; private set; } = 1f;
    public ITexture? Texture { get; private set; }

    private readonly OXRRenderer _renderer;
    private readonly BaseOverlay _parent;

    public OXROverlay(BaseOverlay parent, OXRRenderer renderer)
    {
        _parent = parent;
        _renderer = renderer;
    }

    public ITexture CreateTexture(uint width, uint height)
    {
        _size = new Vector2Int((int)width, (int)height);
        return null!;
    }

    public void SetWidth(float width)
    {
        Width = width;
    }
    
    public void SetAlpha(float alpha) 
        => Alpha = alpha;

    public void SetColor(Vector3 c) 
        => Color = new Vector3(c.x, c.y, c.z);

    public void SetTransform(Transform3D transform)
        => Transform = transform;

    public void SetZOrder(uint _) {}
    
    public void SetCurvature(float _) {}
    public bool TestInteraction(IPointer pointer, out PointerHit hit)
    {
        hit = null!;
        return false;
    }

    public Transform3D UvToWorld(Vector2 uv)
    {
        throw new NotImplementedException();
    }

    public void Render()
    {
        _renderer.AddOverlay(this);
    }

    public void Show()
    {
    }

    public void Hide() { }

    public void Dispose()
    {
        Texture?.Dispose();
    }
}