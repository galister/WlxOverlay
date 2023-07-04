using Silk.NET.OpenXR;
using Action = Silk.NET.OpenXR.Action;

namespace WlxOverlay.Backend.OXR;

public struct OXRAction
{
    public Action Action;
    public ActionType Type;
    public string[] Paths;

    public OXRAction(ActionType type, params string[] paths)
    {
        Action = new Action();
        Type = type;
        Paths = paths;
    }

    public static readonly string[] SupportedProfiles =
    {
        //"/interaction_profiles/khr/simple_controller",
        //"/interaction_profiles/google/daydream_controller",
        //"/interaction_profiles/htc/vive_controller",
        //"/interaction_profiles/htc/vive_pro",
        //"/interaction_profiles/microsoft/motion_controller",
        //"/interaction_profiles/hp/mixed_reality_controller",
        //"/interaction_profiles/samsung/odyssey_controller",
        //"/interaction_profiles/oculus/go_controller",
        "/interaction_profiles/oculus/touch_controller",
        //"/interaction_profiles/valve/index_controller",
        //"/interaction_profiles/htc/vive_cosmos_controller",
        //"/interaction_profiles/huawei/controller",
        //"/interaction_profiles/microsoft/hand_interaction",
    };
}