using WlxOverlay.Backend;
using WlxOverlay.Backend.OVR;
using WlxOverlay.Core;
using WlxOverlay.Core.Interactions;
using WlxOverlay.GFX;
using WlxOverlay.GUI;
using WlxOverlay.Input;
using WlxOverlay.Numerics;
using WlxOverlay.Types;

namespace WlxOverlay.Overlays;

public class KeyboardOverlay : BaseOverlay, IInteractable, IGrabbable
{
    private const uint PixelsPerKey = 80;
    private const uint ButtonPadding = 4;

    private static KeyboardOverlay? _instance;

    private readonly Canvas _canvas;

    public KeyboardOverlay() : base("Keyboard")
    {
        if (_instance != null)
            throw new InvalidOperationException("Can't have more than one KeyboardOverlay!");
        _instance = this;

        if ((Config.Instance.KeyboardVolume ?? 1) > float.Epsilon)
        {
            KeyButton.KeyPressSound = Config.Instance.KeyboardSound == null
                ? Path.Combine(Config.ResourcesFolder, "421581.wav")
                : Path.Combine(Config.UserConfigFolder, Config.Instance.KeyboardSound);
        }

        SpawnPosition = Vector3.Forward + Vector3.Down * 0.5f;
        ShowHideBinding = true;
        WantVisible = true;

        var layout = KeyboardLayout.Instance;
        var canvasWidth = PixelsPerKey * (uint)layout.RowSize;
        var canvasHeight = PixelsPerKey * (uint)layout.MainLayout.Length;

        WidthInMeters = layout.RowSize * 0.05f;

        _canvas = new Canvas(canvasWidth, canvasHeight);

        Canvas.CurrentBgColor = HexColor.FromRgb("#101010");
        _canvas.AddControl(new Panel(0, 0, _canvas.Width, _canvas.Height));


        Canvas.CurrentFont = FontCollection.Get(18, FontStyle.Bold);
        Canvas.CurrentBgColor = HexColor.FromRgb("#202020");

        var unitSize = _canvas.Width / (uint)layout.RowSize;

        var h = unitSize - 2 * ButtonPadding;
        for (var row = 0U; row < layout.KeySizes.Length; row++)
        {
            var y = (int)(_canvas.Height - unitSize * (row + 1) + ButtonPadding);
            var sumPrevSize = 0f;

            for (var col = 0U; col < layout.KeySizes[row].Length; col++)
            {
                var mySize = layout.KeySizes[row][col];
                var x = (int)(unitSize * sumPrevSize + ButtonPadding);
                var w = (uint)(unitSize * mySize) - 2 * ButtonPadding;

                var key = new KeyButton(row, col, x, y, w, h);
                _canvas.AddControl(key);

                sumPrevSize += mySize;
            }
        }
        _canvas.BuildInteractiveLayer();
    }

    protected override void Initialize()
    {
        var hmd = XrBackend.Current.Input.HmdTransform;
        var centerPoint = hmd.TranslatedLocal(SpawnPosition);
        Transform = hmd.LookingAt(centerPoint.origin, hmd.basis.y * hmd.basis.Inverse());
        Transform.origin = centerPoint.origin;

        Texture = _canvas.Initialize();

        base.Initialize();
    }

    protected internal override void Render()
    {
        _canvas.Render();

        base.Render();
    }

    public void OnPointerDown(PointerHit hitData)
    {
        var action = _canvas.OnPointerDown(hitData.uv, hitData.pointer.Hand);
        hitData.pointer.AddReleaseAction(action);
    }

    public void OnPointerUp(PointerHit hitData)
    {
    }

    public void OnScroll(PointerHit hitData, float value)
    {
    }

    public void OnPointerHover(PointerHit hitData)
    {
        if (hitData.isPrimary && KeyButton.Mode != (int)hitData.modifier)
        {
            KeyButton.Mode = (int)hitData.modifier;
            _canvas.MarkDirty();
        }

        _canvas.OnPointerHover(hitData.uv, hitData.pointer.Hand);
    }

    public void OnPointerLeft(LeftRight hand)
    {
        _canvas.OnPointerLeft(hand);
    }

    public void OnGrabbed(PointerHit hitData)
    {
    }

    public void OnDropped()
    {
    }

    public void OnClickWhileHeld()
    {
    }

    public void OnAltClickWhileHeld()
    {
    }
}