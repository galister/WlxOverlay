using Valve.VR;
using X11Overlay.Core;
using X11Overlay.GFX;
using X11Overlay.Numerics;
using X11Overlay.Overlays.Simple;

namespace X11Overlay.Overlays;

/// <summary>
/// A long and thin overlay originating from the controller's tip.
/// Also handles interactions.
/// </summary>
public class LaserPointer : BaseOverlay
{
    public readonly LeftRight Hand;
    public PointerMode Mode { get; private set; }
    public GrabbableOverlay? GrabbedTarget;

    public Transform3D HandTransform;

    public Action? ReleaseAction;
    
    private string myPose;
    private float length;

    private static readonly Vector3[] ModeColors = {
        new(0, 0.37f, 0.5f),
        new(0.69f, 0.19f, 0),
        new(0.37f, 0, 0.5f),
        new(0.62f, 0.62f, 0.62f)
    };

    private static readonly Vector3[] DiminishedColors = {
        new(0, 0.19f, 0.25f),
        new(0.37f, 0.16f, 0),
        new(0.19f, 0, 0.25f),
        new(0.62f, 0.62f, 0.62f)
    };

    private static ITexture? _sharedTexture;

    public LaserPointer(LeftRight hand) : base($"Pointer{hand}")
    {
        Hand = hand;
        myPose = $"{hand}Hand";

        ZOrder = 69;
        WidthInMeters = 0.002f;
        ShowHideBinding = false;
    }

    public override void Initialize()
    {
        length = 2f;
        if (_sharedTexture == null)
        {
            var pixels = new byte[] { 255, 255, 255 };
            _sharedTexture = GraphicsEngine.Instance.TextureFromRaw(1, 1, GraphicsFormat.RGB8, pixels);
        }
        
        Texture = _sharedTexture;
        UploadColor(new Vector3(1,1,1));
        
        base.Initialize();
    }

    protected internal override void AfterInput(bool batteryStateUpdated)
    {
        HandTransform = InputManager.PoseState[myPose];
        EvaluateInput();
        
        if (!ClickNow && ClickBefore)
            ReleaseAction?.Invoke();
        
        if (_showHideNow && !_showHideBefore)
            OverlayManager.Instance.ShowHide();
    }
    
    private static readonly float RotationOffset = Mathf.DegToRad(-90);
    private void RecalculateTransform()
    {
        length = _lastHit?.distance ?? 25f;
        var hmd = InputManager.HmdTransform;
        
        Transform = HandTransform
            .TranslatedLocal(Vector3.Forward * (length * 0.5f))
            .RotatedLocal(Vector3.Right, RotationOffset);
        
        // scale to make it a laser
        Transform = Transform.ScaledLocal(new Vector3(1, length / WidthInMeters, 1));
        
        // billboard towards hmd
        var viewDirection = hmd.origin - HandTransform.origin;

        var x1 = HandTransform.basis.y.Dot(viewDirection);
        var x2 = HandTransform.basis.x.Dot(viewDirection);

        var pies = (x1 - 1) * -0.5f * Mathf.Pi;
        if (x2 < 0)
            pies *= -1;

        Transform = Transform.RotatedLocal(Vector3.Up, pies);
        
        UploadTransform();
    }

    protected bool ClickNow;
    protected bool ClickBefore;

    protected bool GrabNow;
    protected bool GrabBefore;

    protected bool AltClickNow;
    protected bool AltClickBefore;

    private bool _showHideNow;
    private bool _showHideBefore;

    protected float Scroll;
    
    private void EvaluateInput()
    {
        ClickBefore = ClickNow;
        ClickNow = InputManager.BooleanState["Click"][(int)Hand];

        GrabBefore = GrabNow;
        GrabNow = InputManager.BooleanState["Grab"][(int)Hand];

        AltClickBefore = AltClickNow;
        AltClickNow = InputManager.BooleanState["AltClick"][(int)Hand];

        _showHideBefore = _showHideNow;
        _showHideNow = InputManager.BooleanState["ShowHide"][(int)Hand];
        
        Scroll = InputManager.Vector2State["Scroll"][(int)Hand].y;
        
        RecalculateModifier();
    }
    
    private void RecalculateModifier()
    {
        var hmdUp = InputManager.HmdTransform.basis.y;
        var dot = hmdUp.Dot(HandTransform.basis.x) * (1 - 2 * (int)Hand);
        
        Mode = dot switch
        {
            < -0.85f => PointerMode.Shift,
            > 0.7f => PointerMode.Alt,
            _ => PointerMode.Normal
        };
    }

    private readonly List<PointerHit> _pointerHits = new(OverlayManager.MaxInteractableOverlays);
    private PointerHit? _lastHit;
    
    public void TestInteractions(IEnumerable<InteractableOverlay> targets)
    {
        if (GrabbedTarget != null)
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
        }
        else {
            if (_lastHit != null)
            {
                _lastHit.overlay.OnPointerLeft(Hand);
                _lastHit = null;
            }
            
            if (Visible)
                Hide();
        }
    }

    private bool ComputeIntersection(InteractableOverlay target, out PointerHit hitData)
    {
        var wasHit = OpenVR.Overlay.ComputeOverlayIntersection(target.Handle, ref IntersectionParams, ref IntersectionResults);
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
            GrabbedTarget!.OnDropped();
            GrabbedTarget = null;
            return;
        }
        if (Mathf.Abs(Scroll) > 0.1f)
        {
            if (Mode == PointerMode.Alt)
                GrabbedTarget!.OnScrollSize(Scroll);
            else
                GrabbedTarget!.OnScrollDistance(Scroll);
        }

        if (ClickNow && !ClickBefore)
        {
            GrabbedTarget!.OnClickWhileHeld();
        }
        if (AltClickNow && !AltClickBefore)
        {
            GrabbedTarget!.OnAltClickWhileHeld();
        }
        else 
            GrabbedTarget!.OnGrabHeld();
    }

    internal void OnPrimaryLost(InteractableOverlay overlay)
    {
        if (GrabbedTarget == overlay)
            GrabbedTarget = null;
    }

    private void HandlePointerInteractions(PointerHit hitData)
    {
        if (GrabNow && !GrabBefore && hitData.overlay is GrabbableOverlay go)
        {
            go.OnGrabbed(hitData);
            GrabbedTarget = go;
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
        UploadColor(ModeColors[(int)Mode]);
    }
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
    Normal,
    Shift,
    Alt,
}

public enum LeftRight : uint
{
    Left  = 0U,
    Right = 1U
}