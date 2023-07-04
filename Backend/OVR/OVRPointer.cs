using WlxOverlay.Core.Interactions;
using WlxOverlay.GFX;
using WlxOverlay.Numerics;
using WlxOverlay.Overlays;

namespace WlxOverlay.Backend.OVR;

/// <summary>
/// A long and thin overlay originating from the controller's tip.
/// </summary>
public class OVRPointer : BaseOverlay, IPointer
{
    public LeftRight Hand { get; }

    private Transform3D _handTransform;
    private float _length;

    private static readonly float RotationOffset = Mathf.DegToRad(-90);

    private static ITexture? _sharedTexture;

    public OVRPointer(LeftRight hand) : base($"Pointer{hand}")
    {
        Hand = hand;
        WantVisible = true;
        ZOrder = 69;
        WidthInMeters = 0.002f;
        ShowHideBinding = false;
    }

    protected override void Initialize()
    {
        _length = 2f;
        if (_sharedTexture == null)
        {
            var pixels = Enumerable.Repeat((byte)255, 4).ToArray();
            _sharedTexture = GraphicsEngine.Instance.TextureFromRaw(1, 1, GraphicsFormat.RGBA8, pixels);
        }

        Texture = _sharedTexture;

        base.Initialize();
    }

    protected internal override void AfterInput()
    {
        _handTransform = XrBackend.Current.Input.HandTransform(Hand);

        if (_length > float.Epsilon)
        {
            if (!Visible)
                Show();
        }
        else if (Visible)
            Hide();
    }
    
    private void RecalculateTransform()
    {
        var hmd = XrBackend.Current.Input.HmdTransform;

        Transform = _handTransform
            .TranslatedLocal(Vector3.Forward * (_length * 0.5f))
            .RotatedLocal(Vector3.Right, RotationOffset);

        // scale to make it a laser
        Transform = Transform.ScaledLocal(new Vector3(1, _length / WidthInMeters, 1));

        // billboard towards hmd
        /*var viewDirection = _handTransform.origin - hmd.origin;

        const float step = Mathf.Pi / 3f;

        var best = 1f;
        var bestAt = 0;

        for (var i = 0; i < 6; i++)
        {
            var x0 = viewDirection.Dot(Transform.RotatedLocal(Vector3.Up, step * i).basis.z);
            if (x0 < best)
            {
                best = x0;
                bestAt = i;
            }
        }

        Transform = Transform.RotatedLocal(Vector3.Up, step * bestAt);*/

        UploadTransform();
    }

    protected internal override void Render()
    {
        RecalculateTransform();
        UploadColor();
        base.Render();
    }

    public override void SetBrightness(float brightness)
    {
        Brightness = brightness;
        // don't upload, since we'll do that later
    }
    public void SetLength(float length)
    {
        _length = length;
    }

    public void SetColor(Vector3 color)
    {
        Color = color;
    }
}

