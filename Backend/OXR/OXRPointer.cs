using Silk.NET.OpenXR;
using WlxOverlay.Core.Interactions;
using WlxOverlay.Numerics;

namespace WlxOverlay.Backend.OXR;

public class OXRPointer : IPointer
{
    public Transform3D Transform3D;
    private float _length = 2f;
    private Vector3 _color;

    private readonly OXRState _oxr;
    private readonly string _myPrefix;

    private Space _mySpace;
    public InputState State;

    private const int IN_POSE = 0;
    private const int IN_TRIGGER = 1;
    private const int IN_STICK_Y = 2;
    private const int IN_GRIP = 3;
    private const int IN_X_TOUCH = 4;
    private const int IN_Y = 5;
    private const int IN_Y_TOUCH = 6;
    private const int IN_MENU = 7;
    private const int IN_SYS = 8;
    private const int OUT_HAPTIC = 9;

    private readonly OXRAction[] Actions =
    {
        new(ActionType.PoseInput, "/input/grip/pose" ),
        new(ActionType.FloatInput, "/input/trigger/value", "/input/select/value" ),
        new(ActionType.FloatInput, "/input/thumbstick/y", "/input/trackpad/y" ),
        new(ActionType.FloatInput, "/input/squeeze/value" ),
        new(ActionType.BooleanInput, "/input/x/touch", "/input/a/touch" ),
        new(ActionType.BooleanInput, "/input/y/click", "/input/b/click" ),
        new(ActionType.BooleanInput, "/input/y/touch", "/input/b/touch" ),
        new(ActionType.BooleanInput, "/input/menu/click" ),
        new(ActionType.BooleanInput, "/input/system/click" ),
        new(ActionType.VibrationOutput, "/output/haptic" ),
    };

    public OXRPointer(OXRState oxr, LeftRight hand)
    {
        _oxr = oxr;
        _myPrefix = hand == LeftRight.Left ? "/user/hand/left" : "/user/hand/right";
        Hand = hand;
    }

    public void Initialize()
    {
        for (var i = 0; i < Actions.Length; i++)
        {
            ref var myAction = ref Actions[i];
            // e.g. "/input/grip/pose" -> "LeftGripPose"
            var name = Hand + string.Join("",
                myAction.Paths[0].Split('/', StringSplitOptions.RemoveEmptyEntries)[1..].Select(x => char.ToUpper(x[0]) + x[1..]));
            myAction.Action = _oxr.CreateAction(myAction.Type, name);
        }

        foreach (var profile in OXRAction.SupportedProfiles)
        {
            var profileId = _oxr.StringToPath(profile);
            var bindings = new List<ActionSuggestedBinding>();

            for (var i = 0; i < Actions.Length; i++)
            {
                ref var myAction = ref Actions[i];
                foreach (var maybePath in myAction.Paths)
                {
                    var fullPathStr = _myPrefix + maybePath;
                    var pathId = _oxr.StringToPath(fullPathStr);

                    var suggest = new ActionSuggestedBinding
                    {
                        Action = myAction.Action,
                        Binding = pathId
                    };

                    if (_oxr.SuggestInteractionProfileBinding(profileId, suggest))
                    {
                        bindings.Add(suggest);
                        break;
                    }
                }
            }

            if (bindings.Count > 0)
                _oxr.SuggestInteractionProfileBinding(profileId, bindings.ToArray());
        }

        _mySpace = _oxr.CreateActionSpace(Actions[IN_POSE].Action, Transform3D.Identity);
    }

    public LeftRight Hand { get; }
    public void SetLength(float length)
    {
        _length = length;
    }

    public void SetColor(Vector3 color)
    {
        _color = new Vector3(color.x, color.y, color.z);
    }

    public void ApplyHapticFeedback(float durationSec, float amplitude, float frequencyHz = 5)
    {
        _oxr.ApplyHapticFeedback(Actions[OUT_HAPTIC].Action, 0, durationSec, amplitude, frequencyHz);
    }

    public void Update()
    {
        if (_oxr.TryGetPoseAction(Actions[IN_POSE].Action, _mySpace, _oxr.PredictedDisplayTime, out var pose))
            Transform3D = pose;

        if (_oxr.TryGetFloatAction(Actions[IN_TRIGGER].Action, out var fVal))
            State.Click = fVal > 0.5f;

        if (_oxr.TryGetFloatAction(Actions[IN_GRIP].Action, out fVal))
            State.Grab = fVal > 0.5f;

        if (_oxr.TryGetFloatAction(Actions[IN_STICK_Y].Action, out fVal))
            State.Scroll = fVal;

        if (_oxr.TryGetBoolAction(Actions[IN_X_TOUCH].Action, out var bVal))
            State.ClickModifierMiddle = bVal;

        if (_oxr.TryGetBoolAction(Actions[IN_Y_TOUCH].Action, out bVal))
            State.ClickModifierRight = bVal;

        if (_oxr.TryGetBoolAction(Actions[IN_SYS].Action, out bVal))
            State.ShowHide = bVal;

        if (_oxr.TryGetBoolAction(Actions[IN_MENU].Action, out bVal))
            State.ShowHide = bVal;
    }
}