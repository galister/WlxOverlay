using WlxOverlay.Core;
using WlxOverlay.GFX;
using WlxOverlay.Numerics;
using WlxOverlay.Overlays.Simple;
using WlxOverlay.Types;
using WlxOverlay.UI;

namespace WlxOverlay.Overlays;

/// <summary>
/// An overlay that shows chaperone settings
/// </summary>
public class ChaperoneSettings : InteractableOverlay
{
    private readonly Canvas _canvas;

    private readonly string _strPose;
    private readonly Vector3 _vec3RelToHand = new(-0.05f, -0.05f, 0.15f);
    private readonly Vector3 _vec3InsideUnit = Vector3.Right;

    private List<Control> _globalControls = new();
    private List<Control> _polyControls = new();

    private ChaperonePolygon? _selection;

    public ChaperoneSettings(BaseOverlay parent) : base("ChaperoneSettings")
    {
        _strPose = $"{Config.Instance.WatchHand}Hand";
        if (Config.Instance.WatchHand == LeftRight.Right)
        {
            _vec3RelToHand.x *= -1;
            _vec3InsideUnit.x *= -1;
        }

        WidthInMeters = 0.115f;
        ShowHideBinding = false;
        ZOrder = 68;

        // 400 x 200
        _canvas = new Canvas(400, 200);

        Canvas.CurrentBgColor = HexColor.FromRgb("#353535");

        _canvas.AddControl(new Panel(0, 0, 400, 200));

        Canvas.CurrentFgColor = HexColor.FromRgb("#FFFFFF");

        Canvas.CurrentFont = FontCollection.Get(14, FontStyle.Bold);
        Canvas.CurrentFgColor = HexColor.FromRgb("#222222");

        var label = new LabelCentered("", 0, 180, 400, 40);
        
        _canvas.AddControl(new Button("Toast", 200, 100, 50, 36)
        {
            PointerDown = () =>
            {
                NotificationsManager.Toast("Hello world!", "This is a test toast. あいうえお");
            }
        });

        // Top row
        Canvas.CurrentBgColor = HexColor.FromRgb("#444488");
        _canvas.AddControl(new Button("Clear", 2, 162, 130, 36)
        {
            PointerDown = () =>
            {
                ChaperoneManager.Instance.Polygons.Clear();
                ChaperoneManager.Instance.PolygonsChanged();
            }
        });
        Canvas.CurrentBgColor = HexColor.FromRgb("#666666");
        _canvas.AddControl(new Button("Load", 136, 162, 129, 36)
        {
            PointerDown = () =>
            {
                for (var i = 0; i < 10; i++)
                {
                    var i1 = i;
                    OverlayManager.Instance.ScheduleTask(DateTime.UtcNow.AddSeconds(1), () =>
                    {
                        label.Text = $"Loading in {10-i1}s...";
                    });
                }
                OverlayManager.Instance.ScheduleTask(DateTime.UtcNow.AddSeconds(10), () =>
                {
                    ChaperoneManager.Instance.LoadFromFile();
                    label.Text = "";
                });
            }
        });
        _canvas.AddControl(new Button("Save", 269, 162, 129, 36)
        {
            PointerDown = () =>
            {
                for (var i = 0; i < 10; i++)
                {
                    var i1 = i;
                    OverlayManager.Instance.ScheduleTask(DateTime.UtcNow.AddSeconds(1), () =>
                    {
                        label.Text = $"Saving in {10-i1}s...";
                    });
                }
                OverlayManager.Instance.ScheduleTask(DateTime.UtcNow.AddSeconds(10), () =>
                {
                    ChaperoneManager.Instance.SaveToFile();
                    label.Text = "";
                });
            }
        });

        // Bottom row
        Canvas.CurrentBgColor = HexColor.FromRgb("#882222");
        _canvas.AddControl(new Button("Close", 2, 2, 96, 36)
        {
            PointerDown = () =>
            {
                if (_selection != null)
                {
                    ChaperoneManager.Instance.Polygons.Remove(_selection);
                    ChaperoneManager.Instance.PolygonsChanged();
                    _selection = null;
                }
                
                Dispose();
                parent.WantVisible = true;
                parent.Show();
            }
        });
        Canvas.CurrentBgColor = HexColor.FromRgb("#228822");
        _canvas.AddControl(new Button("New Polygon", 102, 2, 296, 36)
        {
            PointerDown = () =>
            {
                if (!FinalizePolygon())
                    OverlayManager.Instance.PointerInteractions.Add(NewPolygonAction);
            }
        });

        _canvas.BuildInteractiveLayer();
    }

    private InteractionResult NewPolygonAction(InteractionArgs args)
    {
        if (args.Hand == Config.Instance.WatchHand)
            return InteractionResult.Unhandled;
        
        if (args.Click)
        {
            _selection = new ChaperonePolygon
            {
                Color = new Vector3(1, 1, 0),
                Points = new List<Vector3> { args.HandTransform.origin, args.HandTransform.origin }
            };
            ChaperoneManager.Instance.Polygons.Add(_selection);
            OverlayManager.Instance.PointerInteractions.Clear();
            OverlayManager.Instance.PointerInteractions.Add(BuildPolygonAction);
        }
        return new InteractionResult{ Length = 0.002f, Color = Vector3.One, Handled = true };
    }

    private InteractionResult BuildPolygonAction(InteractionArgs args)
    {
        if (_selection == null)
        {   
            OverlayManager.Instance.PointerInteractions.Clear();
            return new InteractionResult { Handled = true };
        }

        if (args.Hand == Config.Instance.WatchHand)
            return InteractionResult.Unhandled;
        
        var lastIdx = _selection!.Points.Count - 1;

        var origin = args.HandTransform.origin;
        if (args.Mode != PointerMode.Right) 
            origin.y = _selection.Points[lastIdx - 1].y;

        _selection.Points[lastIdx] = origin;
        if (args.Click)
            _selection.Points.Add(args.HandTransform.origin);
        ChaperoneManager.Instance.PolygonsChanged();
        return new InteractionResult{ Length = 0.002f, Color = Vector3.One, Handled = true };
    }

    private bool FinalizePolygon()
    {
        if (_selection == null) return false;
        
        OverlayManager.Instance.PointerInteractions.Clear();
        var lastIdx = _selection!.Points.Count - 1;
        _selection.Points[lastIdx] = _selection.Points[0];
        _selection = null;
        ChaperoneManager.Instance.PolygonsChanged();
        return true;
    }

    private void SelectionChanged()
    {
        if (_selection != null)
        {
            foreach (var c in _globalControls)
                _canvas.RemoveControl(c);
            foreach (var c in _polyControls)
                _canvas.AddControl(c);
        }
        else
        {
            foreach (var c in _polyControls)
                _canvas.RemoveControl(c);
            foreach (var c in _globalControls)
                _canvas.AddControl(c);
        }
    }

    protected override void Initialize()
    {
        Texture = _canvas.Initialize();

        UpdateInteractionTransform();
        base.Initialize();
    }

    protected internal override void Render()
    {
        _canvas.Render();

        base.Render();
    }

    protected internal override void AfterInput(bool batteryStateUpdated)
    {
        base.AfterInput(batteryStateUpdated);

        var controller = InputManager.PoseState[_strPose];
        var tgt = controller.TranslatedLocal(_vec3InsideUnit).TranslatedLocal(_vec3RelToHand);
        Transform = controller.TranslatedLocal(_vec3RelToHand).LookingAt(tgt.origin, -controller.basis.y);

        UploadTransform();

        var toHmd = (InputManager.HmdTransform.origin - Transform.origin).Normalized();
        var unclampedAlpha = MathF.Log(0.7f, Transform.basis.z.Dot(toHmd)) - 1f;
        Alpha = Mathf.Clamp(unclampedAlpha, 0f, 1f);
        if (Alpha < float.Epsilon)
        {
            if (Visible)
                Hide();
        }
        else
        {
            if (!Visible)
                Show();
            else
                UploadAlpha();
        }
    }

    protected internal override void OnPointerDown(PointerHit hitData)
    {
        base.OnPointerDown(hitData);
        var action = _canvas.OnPointerDown(hitData.uv, hitData.hand);
        hitData.pointer.ReleaseAction = action;
    }

    protected internal override void OnPointerHover(PointerHit hitData)
    {
        base.OnPointerHover(hitData);
        _canvas.OnPointerHover(hitData.uv, hitData.hand);
    }

    protected internal override void OnPointerLeft(LeftRight hand)
    {
        base.OnPointerLeft(hand);
        _canvas.OnPointerLeft(hand);
    }

    protected internal override void OnScroll(PointerHit hitData, float value)
    {
        base.OnScroll(hitData, value);
    }
}
