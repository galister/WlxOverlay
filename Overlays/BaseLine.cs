using WlxOverlay.GFX;
using WlxOverlay.Numerics;

namespace WlxOverlay.Overlays;

public abstract class BaseLine : BaseOverlay
{
    public Vector3 Start { get; private set; }
    public Vector3 End { get; private set; }

    protected BaseLine(string key) : base(key)
    {
        ShowHideBinding = false;
        WidthInMeters = 0.002f;
    }

    private static ITexture? _sharedTexture;

    protected override void Initialize()
    {
        if (_sharedTexture == null)
        {
            var pixels = Enumerable.Repeat((byte)255, 3).ToArray();
            _sharedTexture = GraphicsEngine.Instance.TextureFromRaw(1, 1, GraphicsFormat.RGB8, pixels);
        }

        Texture = _sharedTexture;
        Alpha = 0.5f;

        base.Initialize();
    }
    private static readonly float RotationOffset = Mathf.DegToRad(-90);
    public virtual void SetPoints(Vector3 start, Vector3 end, bool upload = true)
    {
        Start = start;
        End = end;

        var length = (End - Start).Length();

        Transform = Transform3D.Identity.Translated(Start)
            .LookingAt(End, Vector3.Up)
            .TranslatedLocal(Vector3.Forward * length * 0.5f)
            .RotatedLocal(Vector3.Right, RotationOffset)
            .ScaledLocal(new Vector3(1, length / WidthInMeters, 1));

        if (upload)
            UploadTransform();
    }
}
