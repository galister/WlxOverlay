using WlxOverlay.Core;
using WlxOverlay.Desktop;
using WlxOverlay.Numerics;
using WlxOverlay.Types;

namespace WlxOverlay.Overlays.Simple;

/// <summary>
/// An overlay that displays a screen, moves the mouse and sends mouse events.
/// </summary>
public abstract class BaseScreen<T> : GrabbableOverlay
{
    protected readonly T Screen;
    protected readonly UInput? UInp;

    private DateTime _freezeCursor = DateTime.MinValue;
    // ReSharper disable once StaticMemberInGenericType
    private static bool _mouseMoved;

    protected BaseScreen(T screen) : base($"Screen_{screen}")
    {
        WidthInMeters = 1;
        Screen = screen;

        if (KeyboardProvider.Instance is UInput uInput)
            UInp = uInput;
    }

    protected override void Initialize()
    {
        var hmd = InputManager.HmdTransform;
        var centerPoint = hmd.TranslatedLocal(SpawnPosition);

        LocalScale = new Vector3(2, -2, 2);
        CurveWhenUpright = true;

        Transform = hmd.LookingAt(centerPoint.origin, hmd.basis.y * hmd.basis.Inverse()).ScaledLocal(LocalScale);
        Transform.origin = centerPoint.origin;
        OnOrientationChanged();

        base.Initialize();
    }

    protected internal override void Render()
    {
        _mouseMoved = false;
        base.Render();
    }

    protected internal override void OnClickWhileHeld()
    {
        SnapUpright = !SnapUpright;

        base.OnClickWhileHeld();
    }


    protected internal override void OnPointerHover(PointerHit hitData)
    {
        base.OnPointerHover(hitData);
        if (PrimaryPointer == hitData.pointer && !_mouseMoved && _freezeCursor < DateTime.UtcNow)
            _mouseMoved = _mouseMoved || MoveMouse(hitData);
    }

    protected internal override void OnPointerDown(PointerHit hitData)
    {
        if (PrimaryPointer != hitData.pointer)
            _mouseMoved = _mouseMoved || MoveMouse(hitData);

        base.OnPointerDown(hitData);
        _freezeCursor = DateTime.UtcNow + TimeSpan.FromSeconds(Config.Instance.ClickFreezeTime);
        SendMouse(hitData, true);
    }

    protected internal override void OnPointerUp(PointerHit hitData)
    {
        SendMouse(hitData, false);
        base.OnPointerUp(hitData);
    }

    private void SendMouse(PointerHit hitData, bool pressed)
    {
        if (UInp != null)
        {
            var evBtn = hitData.modifier switch
            {
                PointerMode.Right => EvBtn.Right,
                PointerMode.Middle => EvBtn.Middle,
                _ => EvBtn.Left
            };

            UInp.SendButton(evBtn, pressed);
        }
    }

    protected abstract bool MoveMouse(PointerHit hitData);


    private DateTime _nextScroll = DateTime.MinValue;
    protected internal override void OnScroll(PointerHit hitData, float value)
    {
        base.OnScroll(hitData, value);

        if (UInp == null)
            return;

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
            UInp.Wheel(-1);
        else
            UInp.Wheel(1);
    }

    protected internal override void OnAltClickWhileHeld()
    {
        // TODO high quality overlays

        base.OnAltClickWhileHeld();
    }
}