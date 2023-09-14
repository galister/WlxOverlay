using WlxOverlay.GFX;
using WlxOverlay.Numerics;
using WlxOverlay.Types;
using WlxOverlay.Backend;
using WlxOverlay.Core.Interactions;
using WlxOverlay.Core.Interactions.Internal;
using WlxOverlay.Extras;
using WlxOverlay.GUI;
using TaskScheduler = WlxOverlay.Core.TaskScheduler;

namespace WlxOverlay.Overlays;

/// <summary>
/// An overlay that shows chaperone settings
/// </summary>
public class AdvancedSettings : BaseOverlay, IInteractable
{
    private readonly Canvas _canvas;

    private readonly List<Control> _globalControls = new();
    private readonly List<Control> _polyControls = new();

    private ChaperonePolygon? _selection;
    private readonly List<Button> _tabButtons = new();

    private readonly Vector3 ActiveColorBG = HexColor.FromRgb("#77AA77");
    private readonly Vector3 InactiveColorBG = HexColor.FromRgb("#006688");

    private readonly Watch _parent;

    private int AddTabPage(ContentPager p, Button b)
    {
        var idx = p.NewPage();

        b.PointerDown = _ => p.SetActivePage(idx);

        _canvas.AddControl(b);
        _tabButtons.Add(b);

        p.ActivePageChanged += (_, i) =>
          b.SetBgColor(i == idx ? ActiveColorBG : InactiveColorBG);

        return idx;
    }

    public AdvancedSettings(Watch parent) : base("AdvancedSettings")
    {
        _parent = parent;

        WantVisible = true;
        WidthInMeters = 0.115f;
        ShowHideBinding = false;
        ZOrder = 68;

        // 400 x 200
        _canvas = new Canvas(400, 200);

        Canvas.CurrentBgColor = HexColor.FromRgb("#353535");

        _canvas.AddControl(new Panel(0, 0, 400, 200));

        Canvas.CurrentFgColor = HexColor.FromRgb("#FFFFFF");
        var label = new LabelCentered("", 55, 2, 360, 32);
        _canvas.AddControl(label);

        Canvas.CurrentFont = FontCollection.Get(14, FontStyle.Bold);
        Canvas.CurrentFgColor = HexColor.FromRgb("#222222");

        var pager = new ContentPager(_canvas);

        // --------------- settings ------------------

        Canvas.CurrentFgColor = Vector3.One;
        Canvas.CurrentBgColor = ActiveColorBG;
        var settings = AddTabPage(pager, new Button("Settings", 2, 162, 130, 36));

        pager.AddControl(settings, new LabelCentered("Watch", 15, 136, 130, 24));

        Canvas.CurrentBgColor = HexColor.FromRgb("#444488");
        pager.AddControl(settings, new Button("Swap Hand", 15, 100, 130, 30)
        {
            PointerDown = _ => parent.SwapHands()
        });

        Canvas.CurrentBgColor = HexColor.FromRgb("#884444");
        pager.AddControl(settings, new Button("Hide", 15, 64, 130, 30)
        {
            PointerDown = _ =>
            {
                parent.Hidden = true;
                NotificationsManager.Toast("Watch Hidden", "Use Show/Hide binding to get it back.", alwaysShow: true);
                Dispose();
            }
        });

        pager.AddControl(settings, new LabelCentered("Play Space", 160, 136, 130, 24));

        Canvas.CurrentBgColor = HexColor.FromRgb("#444488");

        pager.AddControl(settings, new Button("Fix Floor", 160, 100, 130, 30)
        {
            PointerDown = _ =>
            {
                for (var i = 0; i < 5; i++)
                {
                    var i1 = i;
                    TaskScheduler.ScheduleTask(DateTime.UtcNow.AddSeconds(i), () =>
                    {
                        label.Text = $"Put controller on floor! {5 - i1}s";
                    });
                }
                TaskScheduler.ScheduleTask(DateTime.UtcNow.AddSeconds(5), () =>
                {
                    PlaySpaceMover.FixFloor();
                    label.Text = "";
                });
            }
        });
        pager.AddControl(settings, new Button("Reset Offset", 160, 64, 130, 30)
        {
            PointerDown = _ => PlaySpaceMover.ResetOffset()
        });
        pager.AddControl(settings, new Button("Make Default", 160, 28, 130, 30)
        {
            PointerDown = _ => PlaySpaceMover.SetAsDefault()
        });

        pager.AddControl(settings, new LabelCentered("Popups", 305, 136, 80, 24));

        Canvas.CurrentBgColor = Session.Instance.NotificationsDnd ? InactiveColorBG : ActiveColorBG;
        pager.AddControl(settings, new Button("Enable", 305, 100, 80, 30)
        {
            PointerDown = b =>
            {
                Session.Instance.NotificationsDnd = !Session.Instance.NotificationsDnd;
                b.SetBgColor(Session.Instance.NotificationsDnd ? InactiveColorBG : ActiveColorBG);
                Session.Instance.Persist();
            }
        });

        Canvas.CurrentBgColor = Session.Instance.NotificationsMuteAudio ? InactiveColorBG : ActiveColorBG;
        pager.AddControl(settings, new Button("Audio", 305, 64, 80, 30)
        {
            PointerDown = b =>
            {
                Session.Instance.NotificationsMuteAudio = !Session.Instance.NotificationsMuteAudio;
                b.SetBgColor(Session.Instance.NotificationsMuteAudio ? InactiveColorBG : ActiveColorBG);
                Session.Instance.Persist();
            }
        });

        Canvas.CurrentBgColor = HexColor.FromRgb("#444488");
        pager.AddControl(settings, new Button("Test", 305, 28, 80, 30)
        {
            PointerDown = _ => NotificationsManager.Toast("Hello world!", "This is a test toast.\nあいうえお")
        });

        // --------------- video ------------------

        Canvas.CurrentFgColor = Vector3.One;
        Canvas.CurrentBgColor = InactiveColorBG;
        var video = AddTabPage(pager, new Button("Video", 136, 162, 129, 36));

        Canvas.CurrentBgColor = HexColor.FromRgb("#444488");
        Canvas.CurrentFgColor = Vector3.One;

        pager.AddControl(video, new Button("Reset", 35, 84, 70, 32)
        {
            PointerDown = _ =>
            {
                XrBackend.Current.AdjustGain(0, 1f);
                XrBackend.Current.AdjustGain(1, 1f);
                XrBackend.Current.AdjustGain(2, 1f);
            }
        });

        Canvas.CurrentBgColor = HexColor.FromRgb("#909090");
        pager.AddControl(video, new Button("+", 127, 116, 46, 32)
        {
            PointerDown = _ =>
            {
                XrBackend.Current.AdjustGain(0, 0.1f);
                XrBackend.Current.AdjustGain(1, 0.1f);
                XrBackend.Current.AdjustGain(2, 0.1f);
            }
        });

        pager.AddControl(video, new Button("-", 127, 52, 46, 32)
        {
            PointerDown = _ =>
            {
                XrBackend.Current.AdjustGain(0, -0.1f);
                XrBackend.Current.AdjustGain(1, -0.1f);
                XrBackend.Current.AdjustGain(2, -0.1f);
            }
        });

        Canvas.CurrentBgColor = HexColor.FromRgb("#DD0000");
        pager.AddControl(video, new Button("+", 207, 116, 46, 32)
        {
            PointerDown = _ => XrBackend.Current.AdjustGain(0, 0.1f)
        });

        pager.AddControl(video, new Button("-", 207, 52, 46, 32)
        {
            PointerDown = _ => XrBackend.Current.AdjustGain(0, -0.1f)
        });

        Canvas.CurrentBgColor = HexColor.FromRgb("#00AA00");
        pager.AddControl(video, new Button("+", 267, 116, 46, 32)
        {
            PointerDown = _ => XrBackend.Current.AdjustGain(1, 0.1f)
        });

        pager.AddControl(video, new Button("-", 267, 52, 46, 32)
        {
            PointerDown = _ => XrBackend.Current.AdjustGain(1, -0.1f)
        });

        Canvas.CurrentBgColor = HexColor.FromRgb("#0000DD");
        pager.AddControl(video, new Button("+", 327, 116, 46, 32)
        {
            PointerDown = _ => XrBackend.Current.AdjustGain(2, 0.1f)
        });

        pager.AddControl(video, new Button("-", 327, 52, 46, 32)
        {
            PointerDown = _ => XrBackend.Current.AdjustGain(2, -0.1f)
        });

        // --------------- strokes ------------------

        Canvas.CurrentFgColor = Vector3.One;
        Canvas.CurrentBgColor = InactiveColorBG;
        var strokes = AddTabPage(pager, new Button("Strokes", 269, 162, 129, 36));

        Canvas.CurrentFgColor = Vector3.One;

        Canvas.CurrentBgColor = HexColor.FromRgb("#444488");
        pager.AddControl(strokes, new Button("Clear", 55, 48, 130, 30)
        {
            PointerDown = _ =>
            {
                ChaperoneManager.Instance.Polygons.Clear();
                ChaperoneManager.Instance.PolygonsChanged();
            }
        });
        Canvas.CurrentBgColor = HexColor.FromRgb("#666666");
        pager.AddControl(strokes, new Button("Load", 55, 84, 130, 30)
        {
            PointerDown = _ =>
            {
                for (var i = 0; i < 10; i++)
                {
                    var i1 = i;
                    TaskScheduler.ScheduleTask(DateTime.UtcNow.AddSeconds(i), () =>
                    {
                        label.Text = $"Loading in {10 - i1}s...";
                    });
                }
                TaskScheduler.ScheduleTask(DateTime.UtcNow.AddSeconds(10), () =>
                {
                    ChaperoneManager.Instance.LoadFromFile();
                    label.Text = "";
                });
            }
        });
        pager.AddControl(strokes, new Button("Save", 55, 120, 130, 30)
        {
            PointerDown = _ =>
            {
                for (var i = 0; i < 10; i++)
                {
                    var i1 = i;
                    TaskScheduler.ScheduleTask(DateTime.UtcNow.AddSeconds(i), () =>
                    {
                        label.Text = $"Saving in {10 - i1}s...";
                    });
                }
                TaskScheduler.ScheduleTask(DateTime.UtcNow.AddSeconds(10), () =>
                {
                    ChaperoneManager.Instance.SaveToFile();
                    label.Text = "";
                });
            }
        });

        Canvas.CurrentBgColor = HexColor.FromRgb("#228822");
        var newBtn = new Button("New Polygon", 215, 120, 130, 30)
        {
            PointerDown = b =>
            {
                if (!FinalizePolygon())
                {
                    InteractionsHandler.RegisterCustomInteraction(nameof(NewPolygonAction), NewPolygonAction);
                    b.SetText("Finish");
                    label.Text = "Add segments using right hand.";
                }
                else
                {
                    b.SetText("New Polygon");
                    label.Text = "";
                }

            }
        };
        pager.AddControl(strokes, newBtn);

        pager.AddControl(strokes, new LabelCentered("See Wiki", 215, 84, 130, 30));
        pager.AddControl(strokes, new LabelCentered("for help", 215, 48, 130, 30));

        // Bottom row
        Canvas.CurrentBgColor = HexColor.FromRgb("#882222");
        _canvas.AddControl(new Button("X", 2, 2, 36, 36)
        {
            PointerDown = _ =>
            {
                if (_selection != null)
                {
                    ChaperoneManager.Instance.Polygons.Remove(_selection);
                    ChaperoneManager.Instance.PolygonsChanged();
                    _selection = null;
                    newBtn.SetText("New Polygon");
                    label.Text = "";
                }

                Dispose();
                parent.Hidden = false;
                parent.Show();
            }
        });

        pager.SetActivePage(0);
        _canvas.BuildInteractiveLayer();
    }

    private InteractionResult NewPolygonAction(InteractionArgs args)
    {
        if (args.Hand == Config.Instance.WatchHand)
            return InteractionResult.Unhandled;

        if (!args.Before.Click && args.Now.Click)
        {
            _selection = new ChaperonePolygon
            {
                Color = new Vector3(1, 1, 0),
                Points = new List<Vector3> { args.HandTransform.origin, args.HandTransform.origin }
            };
            ChaperoneManager.Instance.Polygons.Add(_selection);
            InteractionsHandler.UnregisterCustomInteraction(nameof(NewPolygonAction));
            InteractionsHandler.RegisterCustomInteraction(nameof(BuildPolygonAction), BuildPolygonAction);
        }
        return new InteractionResult { Length = 0.002f, Color = Vector3.One, Handled = true };
    }

    private InteractionResult BuildPolygonAction(InteractionArgs args)
    {
        if (_selection == null)
        {
            InteractionsHandler.UnregisterCustomInteraction(nameof(BuildPolygonAction));
            return new InteractionResult { Handled = true };
        }

        if (args.Hand == Config.Instance.WatchHand)
            return InteractionResult.Unhandled;

        var lastIdx = _selection!.Points.Count - 1;

        var origin = args.HandTransform.origin;
        if (args.Mode != PointerMode.Right)
            origin.y = _selection.Points[lastIdx - 1].y;

        _selection.Points[lastIdx] = origin;
        if (!args.Before.Click && args.Now.Click)
            _selection.Points.Add(args.HandTransform.origin);
        ChaperoneManager.Instance.PolygonsChanged();
        return new InteractionResult { Length = 0.002f, Color = Vector3.One, Handled = true };
    }

    private bool FinalizePolygon()
    {
        if (_selection == null) return false;

        InteractionsHandler.UnregisterCustomInteraction(nameof(BuildPolygonAction));
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
        base.Initialize();
    }

    protected internal override void Render()
    {
        _canvas.Render();

        base.Render();
    }

    protected internal override void AfterInput()
    {
        base.AfterInput();

        var controller = XrBackend.Current.Input.HandTransform(_parent.Hand);
        var tgt = controller.TranslatedLocal(_parent.Vec3InsideUnit).TranslatedLocal(_parent.Vec3RelToHand);
        Transform = controller.TranslatedLocal(_parent.Vec3RelToHand).LookingAt(tgt.origin, -controller.basis.y);

        UploadTransform();

        var toHmd = (XrBackend.Current.Input.HmdTransform.origin - Transform.origin).Normalized();
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

    public void OnPointerDown(PointerHit hitData)
    {
        var action = _canvas.OnPointerDown(hitData.uv, hitData.pointer.Hand);
        hitData.pointer.AddReleaseAction(action);
    }

    public void OnPointerUp(PointerHit hitData)
    {
    }

    public void OnPointerHover(PointerHit hitData)
    {
        _canvas.OnPointerHover(hitData.uv, hitData.pointer.Hand);
    }

    public void OnPointerLeft(LeftRight hand)
    {
        _canvas.OnPointerLeft(hand);
    }

    public void OnScroll(PointerHit hitData, float value) { }
}
