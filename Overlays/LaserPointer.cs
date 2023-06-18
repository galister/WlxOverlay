using Valve.VR;
using WlxOverlay.Core;
using WlxOverlay.GFX;
using WlxOverlay.Numerics;
using WlxOverlay.Overlays.Simple;
using WlxOverlay.Types;
using Action = System.Action;

namespace WlxOverlay.Overlays;

/// <summary>
/// A long and thin overlay originating from the controller's tip.
/// Also handles interactions.
/// </summary>
public class LaserPointer : BaseOverlay
{
    public readonly LeftRight Hand;
    public PointerMode Mode { get; private set; }

    public Transform3D HandTransform;

    public Action? ReleaseAction;

    private readonly string _myPose;

    private GrabbableOverlay? _grabbedTarget;
    private Vector2 _grabbedUv;

    private float _length;
    private float? _interactLength;

    private static readonly float RotationOffset = Mathf.DegToRad(-90);

    private static readonly Vector3[] ModeColors = {
        HexColor.FromRgb(Config.Instance.PrimaryColor ?? Config.DefaultPrimaryColor),
        HexColor.FromRgb(Config.Instance.ShiftColor ?? Config.DefaultShiftColor),
        HexColor.FromRgb(Config.Instance.AltColor ?? Config.DefaultAltColor),
        HexColor.FromRgb("#A0A0A0"),
    };

    private static ITexture? _sharedTexture;

    public LaserPointer(LeftRight hand) : base($"Pointer{hand}")
    {
        Hand = hand;
        _myPose = $"{hand}Hand";

        ZOrder = 69;
        WidthInMeters = 0.002f;
        ShowHideBinding = false;
    }

    protected override void Initialize()
    {
        _length = 2f;
        if (_sharedTexture == null)
        {
            var pixels = new byte[] { 255, 255, 255 };
            _sharedTexture = GraphicsEngine.Instance.TextureFromRaw(1, 1, GraphicsFormat.RGB8, pixels);
        }

        Texture = _sharedTexture;

        base.Initialize();
    }

    protected internal override void AfterInput(bool batteryStateUpdated)
    {
        HandTransform = InputManager.PoseState[_myPose];
        EvaluateInput();

        if (!ClickNow && ClickBefore)
        {
            ReleaseAction?.Invoke();
            ReleaseAction = null;
        }

        if (_spaceDragNow)
            SpaceDrag();

        if (_showHideNow && !_showHideBefore)
            OverlayManager.Instance.ShowHide();
    }

    protected bool ClickNow;
    protected bool ClickBefore;

    protected bool GrabNow;
    protected bool GrabBefore;

    protected bool AltClickNow;
    protected bool AltClickBefore;

    protected bool ClickModifierRight;
    protected bool ClickModifierMiddle;

    private bool _showHideNow;
    private bool _showHideBefore;

    private bool _spaceDragNow;
    private bool _spaceDragBefore;

    protected float Scroll;

    private bool GetBoolState(string name)
    {
        if (InputManager.BooleanState.TryGetValue(name, out var hands))
            return hands[(int)Hand];
        return false;
    }

    private Vector3 GetVec3State(string name)
    {
        if (InputManager.Vector3State.TryGetValue(name, out var hands))
            return hands[(int)Hand];
        return Vector3.Zero;
    }

    private void EvaluateInput()
    {
        ClickBefore = ClickNow;
        ClickNow = GetBoolState("Click");

        GrabBefore = GrabNow;
        GrabNow = GetBoolState("Grab");

        AltClickBefore = AltClickNow;
        AltClickNow = GetBoolState("AltClick");

        _showHideBefore = _showHideNow;
        _showHideNow = GetBoolState("ShowHide");

        _spaceDragBefore = _spaceDragNow;
        _spaceDragNow = GetBoolState("SpaceDrag");

        ClickModifierRight = GetBoolState("ClickModifierRight");

        ClickModifierMiddle = GetBoolState("ClickModifierMiddle");

        Scroll = GetVec3State("Scroll").y;

        RecalculateModifier();
    }

    private Vector3 _lastPosition;
    private void SpaceDrag()
    {
        if (!PlaySpaceManager.Instance.CanDrag)
          return;
        
        if (_spaceDragBefore)
            PlaySpaceManager.Instance.ApplyOffsetRelative(HandTransform.origin - _lastPosition);
        _lastPosition = HandTransform.origin;
    }

    private void RecalculateModifier()
    {
        if (ClickModifierRight)
        {
            Mode = PointerMode.Right;
            return;
        }

        if (ClickModifierMiddle)
        {
            Mode = PointerMode.Middle;
            return;
        }

        var hmdUp = InputManager.HmdTransform.basis.y;
        var dot = hmdUp.Dot(HandTransform.basis.x) * (1 - 2 * (int)Hand);

        Mode = dot switch
        {
            < -0.85f => PointerMode.Right,
            > 0.7f => PointerMode.Middle,
            _ => PointerMode.Left
        };

        if (Mode == PointerMode.Middle && !GrabNow && !Config.Instance.MiddleClickOrientation)
            Mode = PointerMode.Left;
        else if (Mode == PointerMode.Right && !Config.Instance.RightClickOrientation)
            Mode = PointerMode.Left;
    }

    private void RecalculateTransform()
    {
        _length = _lastHit?.distance ?? _interactLength ?? 25f;
        var hmd = InputManager.HmdTransform;

        Transform = HandTransform
            .TranslatedLocal(Vector3.Forward * (_length * 0.5f))
            .RotatedLocal(Vector3.Right, RotationOffset);

        // scale to make it a laser
        Transform = Transform.ScaledLocal(new Vector3(1, _length / WidthInMeters, 1));

        // billboard towards hmd
        var viewDirection = HandTransform.origin - hmd.origin;

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

        Transform = Transform.RotatedLocal(Vector3.Up, step * bestAt);

        UploadTransform();
    }

    private readonly List<PointerHit> _pointerHits = new(OverlayManager.MaxInteractableOverlays);
    private PointerHit? _lastHit;

    public void TestInteractions(IEnumerable<InteractableOverlay> targets)
    {
        if (_grabbedTarget != null)
        {
            HandleGrabbedInteractions();
            return;
        }

        _pointerHits.Clear();


        HandTransform.origin.CopyTo(ref IntersectionParams.vSource);
        (-HandTransform.basis.z).CopyTo(ref IntersectionParams.vDirection);

        foreach (var overlay in targets)
            if (ComputeIntersection(overlay, out var hitData))
                _pointerHits.Add(hitData);

        if (_pointerHits.Count > 0)
        {
            _pointerHits.Sort((a, b) => a.distance.CompareTo(b.distance));

            var newHit = _pointerHits.First();
            if (_lastHit != null && _lastHit.overlay != newHit.overlay)
                _lastHit.overlay.OnPointerLeft(Hand);

            HandlePointerInteractions(newHit);
            _lastHit = newHit;

            if (!Visible)
                Show();

            Color = ModeColors[(int)Mode];
        }
        else
        {
            if (_lastHit != null)
            {
                _lastHit.overlay.OnPointerLeft(Hand);
                _lastHit = null;
            }

            _interactLength = null;

            foreach (var func in OverlayManager.Instance.PointerInteractions)
            {
                var result = func.Invoke(new InteractionArgs { Hand = Hand, Mode = Mode, HandTransform = HandTransform, Click = ClickNow && !ClickBefore });
                if (!result.Handled)
                    continue;

                if (result.Length > float.Epsilon)
                {
                    Color = result.Color;
                    _interactLength = result.Length;
                    if (!Visible)
                        Show();
                    return;
                }
                break;
            }

            if (Visible)
                Hide();
        }
    }

    private bool ComputeIntersection(InteractableOverlay target, out PointerHit hitData)
    {
        var wasHit = OpenVR.Overlay.ComputeOverlayIntersection(target.ChildOverlay?.Handle ?? target.Handle, ref IntersectionParams, ref IntersectionResults);
        if (!wasHit || !target.TryTransformToLocal((Vector2)IntersectionResults.vUVs, out var localUv))
        {
            hitData = null!;
            return false;
        }

        hitData = new PointerHit(this, target, IntersectionResults, localUv);
        return wasHit;
    }

    /// <summary>
    /// Runs on the overlay that's being grabbed.
    /// </summary>
    private void HandleGrabbedInteractions()
    {
        if (!GrabNow)
        {
            _grabbedTarget!.OnDropped();
            _grabbedTarget = null;
            return;
        }
        if (Mathf.Abs(Scroll) > 0.1f)
        {
            if (Mode == PointerMode.Middle)
                _grabbedTarget!.OnScrollSize(Scroll);
            else
                _grabbedTarget!.OnScrollDistance(Scroll);
        }

        if (ClickNow && !ClickBefore)
        {
            _grabbedTarget!.OnClickWhileHeld();
        }
        if (AltClickNow && !AltClickBefore)
        {
            _grabbedTarget!.OnAltClickWhileHeld();
        }
        else
            _grabbedTarget!.OnGrabHeld();

        _lastHit!.point = _grabbedTarget.CurvedSurfaceTransformFromUv(_grabbedUv).origin;
        _lastHit!.distance = (_lastHit.point - HandTransform.origin).Length();
    }

    internal void OnPrimaryLost(InteractableOverlay overlay)
    {
        if (_grabbedTarget == overlay)
            _grabbedTarget = null;
    }

    private void HandlePointerInteractions(PointerHit hitData)
    {
        if (GrabNow && !GrabBefore && hitData.overlay is GrabbableOverlay go)
        {
            go.OnGrabbed(hitData);
            go.TryTransformToLocal(hitData.uv, out _grabbedUv);
            _grabbedTarget = go;
            return;
        }

        hitData.overlay.OnPointerHover(hitData);

        if (ClickNow && !ClickBefore)
            hitData.overlay.OnPointerDown(hitData);
        else if (!ClickNow && ClickBefore)
            hitData.overlay.OnPointerUp(hitData);

        if (Mathf.Abs(Scroll) > 0.1f)
            hitData.overlay.OnScroll(hitData, Scroll);
    }

    protected internal override void Render()
    {
        RecalculateTransform();
        UploadColor();
    }

    public override void SetBrightness(float brightness)
    {
        Brightness = brightness;
        // don't upload, since we'll do that later
    }
}

public class InteractionArgs
{
    public Transform3D HandTransform;
    public PointerMode Mode;
    public LeftRight Hand;
    public bool Click;
}

public struct InteractionResult
{
    public bool Handled;
    public float Length;
    public Vector3 Color;

    public static readonly InteractionResult Unhandled = new() { Handled = false };
}

public class PointerHit
{
    public readonly InteractableOverlay overlay;
    public readonly LaserPointer pointer;
    public readonly LeftRight hand;
    public float distance;
    public Vector2 uv;
    public Vector3 point;
    public Vector3 normal;
    public PointerMode modifier;

    public PointerHit(LaserPointer p, InteractableOverlay o, VROverlayIntersectionResults_t h, Vector2 localUv)
    {
        overlay = o;
        pointer = p;
        hand = p.Hand;
        modifier = p.Mode;
        distance = h.fDistance;
        uv = localUv;
        normal = h.vNormal.ToVector3();
        point = h.vPoint.ToVector3();
    }

    public override string ToString()
    {
        return $"{hand} on {overlay.Key} at {uv} ({point})";
    }
}

public enum PointerMode : uint
{
    Left,
    Right,
    Middle,
}

public enum LeftRight : uint
{
    Left = 0U,
    Right = 1U
}

