using WlxOverlay.Core.Interactions;
using WlxOverlay.GFX;
using WlxOverlay.Numerics;
using WlxOverlay.Overlays;

namespace WlxOverlay.Backend;

public interface IOverlay : IDisposable
{
    public ITexture CreateTexture(uint width, uint height);

    public void SetWidth(float width);

    public void SetAlpha(float alpha);

    public void SetColor(Vector3 c);

    public void SetTransform(Transform3D transform);

    public void SetZOrder(uint zOrder);

    public void SetCurvature(float curvature);

    public bool TestInteraction(IPointer pointer, out PointerHit hit);

    public Transform3D UvToWorld(Vector2 uv);

    public void Render();

    public void Show();

    public void Hide();
}