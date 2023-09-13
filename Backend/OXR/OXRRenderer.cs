using Silk.NET.OpenXR;
using WlxOverlay.GFX;
using WlxOverlay.GFX.OpenGL;
using WlxOverlay.Numerics;

namespace WlxOverlay.Backend.OXR;

public class OXRRenderer
{
    private readonly List<OXROverlay> _overlays = new();
    private readonly OXRState _oxr;

    private GlStereoRenderer _renderer;

    public OXRRenderer(OXRState oxr)
    {
        _oxr = oxr;
    }

    public void Initialize()
    {
        _renderer = ((GlGraphicsEngine)GraphicsEngine.Instance).CreateStereoRenderer();
    }

    public void Clear()
    {
        _overlays.Clear();
    }

    public void AddOverlay(OXROverlay overlay)
    {
        _overlays.Add(overlay);
    }

    private Transform3D CreateProjectionFov(Fovf fov, float nearZ, float farZ)
    {
        var tanAngleLeft = (float)Math.Tan(fov.AngleLeft);
        var tanAngleRight = (float)Math.Tan(fov.AngleRight);

        var tanAngleDown = (float)Math.Tan(fov.AngleDown);
        var tanAngleUp = (float)Math.Tan(fov.AngleUp);

        var tanAngleWidth = tanAngleRight - tanAngleLeft;
        var tanAngleHeight = (tanAngleUp - tanAngleDown);

        const float offsetZ = 0;

        var result = Transform3D.Identity;
        result[0, 0] = 2 / tanAngleWidth;
        result[1, 0] = 0;
        result[2, 0] = (tanAngleRight + tanAngleLeft) / tanAngleWidth;
        result[3, 0] = 0;

        result[0, 1] = 0;
        result[1, 1] = 2 / tanAngleHeight;
        result[2, 1] = (tanAngleUp + tanAngleDown) / tanAngleHeight;
        result[3, 1] = 0;

        result[0, 2] = 0;
        result[1, 2] = 0;
        result[2, 2] = -(farZ + offsetZ) / (farZ - nearZ);
        result[3, 2] = -(farZ * (nearZ + offsetZ)) / (farZ - nearZ);
        return result;
    }

    private readonly uint[] _textures = new uint[2];
    private readonly Rect2Di[] _rects = new Rect2Di[2];
    private readonly Transform3D[] _pvMatrices = new Transform3D[2];

    public void Render(uint swapchainIndex)
    {
        for (var i = 0; i < 2; i++)
        {
            ref var data = ref _oxr.ProjectionViews[i];
            var mProj = CreateProjectionFov(data.Fov, 0.001f, 100f);
            var mView = data.Pose.ToWlx();

            _pvMatrices[i] = mProj * mView;
            _textures[i] = _oxr.SwapchainImages[swapchainIndex].Image;
            _rects[i] = data.SubImage.ImageRect;
        }

        _renderer.Begin(_textures, _rects, _pvMatrices);
        _renderer.Clear();
        _renderer.UseShader(GlGraphicsEngine.QuadShader);

        //foreach (var overlay in _overlays)
        //    _renderer.DrawQuad(overlay.Texture, overlay.Color, overlay.Alpha, overlay.Transform);

        _renderer.DrawColor(new Vector3(0, 1, 0));

        _renderer.End();
    }
}