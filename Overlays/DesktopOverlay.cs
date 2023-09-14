using WlxOverlay.Backend;
using WlxOverlay.Capture;
using WlxOverlay.Core.Interactions;
using WlxOverlay.Desktop;
using WlxOverlay.GFX;
using WlxOverlay.Input;
using WlxOverlay.Input.Impl;
using WlxOverlay.Numerics;
using WlxOverlay.Types;

namespace WlxOverlay.Overlays;

/// <summary>
/// An overlay that displays a desktop screen, moves the mouse and sends mouse events.
/// </summary>
public class DesktopOverlay : BaseOverlay, IInteractable, IGrabbable
{
    // ReSharper disable StaticMemberInGenericType
    private static int _numScreens;
    private static bool _mouseMoved;

    public readonly BaseOutput Screen;

    private DateTime _freezeCursor = DateTime.MinValue;
    private readonly IDesktopCapture _capture;

    public DesktopOverlay(BaseOutput screen, IDesktopCapture capture) : base($"Screen_{screen}")
    {
        WidthInMeters = 1;
        Screen = screen;
        _capture = capture;

        if (int.TryParse(Config.Instance.DefaultScreen, out var defaultIdx))
            WantVisible = _numScreens == defaultIdx;
        else
            WantVisible = Screen.ToString() == Config.Instance.DefaultScreen;

        _numScreens++;
    }

    protected override void Initialize()
    {
        _capture.Initialize();

        var hmd = XrBackend.Current.Input.HmdTransform;
        var centerPoint = hmd.TranslatedLocal(SpawnPosition);

        LocalScale = new Vector3(2, -2, 2);
        CurveWhenUpright = true;

        Transform = hmd.LookingAt(centerPoint.origin, hmd.basis.y * hmd.basis.Inverse()).ScaledLocal(LocalScale);
        Transform.origin = centerPoint.origin;
        OnOrientationChanged();

        Texture = GraphicsEngine.Instance.EmptyTexture((uint)Screen.Size.X, (uint)Screen.Size.Y, internalFormat: GraphicsFormat.RGB8, dynamic: true);
        base.Initialize();
    }

    protected internal override void Render()
    {
        _capture.TryApplyToTexture(Texture!);
        _mouseMoved = false;
        base.Render();
    }

    public override void Show()
    {
        _capture.Resume();
        base.Show();
    }

    public override void Hide()
    {
        _capture.Pause();
        base.Hide();
    }

    public void OnGrabbed(PointerHit hitData)
    {
    }

    public void OnDropped()
    {
    }

    public void OnClickWhileHeld()
    {
        SnapUpright = !SnapUpright;
    }

    public void OnAltClickWhileHeld()
    {
    }

    public void OnPointerHover(PointerHit hitData)
    {
        if (hitData.isPrimary && !_mouseMoved && _freezeCursor < DateTime.UtcNow)
            _mouseMoved = _mouseMoved || MoveMouse(hitData);
    }

    public void OnPointerLeft(LeftRight hand)
    {
    }

    public void OnPointerDown(PointerHit hitData)
    {
        if (hitData.isPrimary)
            _mouseMoved = _mouseMoved || MoveMouse(hitData);

        _freezeCursor = DateTime.UtcNow + TimeSpan.FromSeconds(Config.Instance.ClickFreezeTime);
        SendMouse(hitData, true);
    }

    public void OnPointerUp(PointerHit hitData)
    {
        SendMouse(hitData, false);
    }

    private void SendMouse(PointerHit hitData, bool pressed)
    {
        var evBtn = hitData.modifier switch
        {
            PointerMode.Right => EvBtn.Right,
            PointerMode.Middle => EvBtn.Middle,
            _ => EvBtn.Left
        };
        InputProvider.Mouse.SendButton(evBtn, pressed);
    }

    private bool MoveMouse(PointerHit hitData)
    {
        var pos = Screen.Transform * hitData.uv;

        var rectSize = BaseOutput.OutputRect.Size;
        var mulX = UInput.Extent / rectSize.x;
        var mulY = UInput.Extent / rectSize.y;

        InputProvider.Mouse.MouseMove((int)(pos.x * mulX), (int)(pos.y * mulY));
        return true;
    }

    private DateTime _nextScroll = DateTime.MinValue;

    public void OnScroll(PointerHit hitData, float value)
    {
        if (_nextScroll > DateTime.UtcNow)
            return;


        if (hitData.modifier == PointerMode.Middle)
        {
            // super fast scroll, 1 click per frame
        }
        else
        {
            var millis = hitData.modifier == PointerMode.Right ? 50 : 100;
            _nextScroll = DateTime.UtcNow.AddMilliseconds((1 - Mathf.Abs(value)) * millis);
        }

        if (value < 0)
            InputProvider.Mouse.Wheel(-1);
        else
            InputProvider.Mouse.Wheel(1);
    }

    public override void UploadTransform()
    {
        var oldTransform = Transform;
        Transform = Transform.RotatedLocal(Vector3.Back, Screen.Transform.Rotation);
        base.UploadTransform();
        Transform = oldTransform;
    }

    public override string ToString()
    {
        return Screen.Name;
    }

    public override void Dispose()
    {
        _capture.Dispose();
        Texture?.Dispose();
        base.Dispose();
    }
}
