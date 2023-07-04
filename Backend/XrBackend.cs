using WlxOverlay.Backend.OVR;
using WlxOverlay.Backend.OXR;
using WlxOverlay.Core;
using WlxOverlay.Core.Interactions;
using WlxOverlay.Core.Interactions.Internal;
using WlxOverlay.Types;

namespace WlxOverlay.Backend;

public static class XrBackend
{ 
    public static IXrBackend Current = null!;

    public static void UseOpenVR()
    {
        Current = new OVRBackend();
        
        var left = new OVRPointer(LeftRight.Left);
        var right = new OVRPointer(LeftRight.Right);
        
        var pointers = Config.Instance.PrimaryHand == LeftRight.Left 
            ? new [] { left, right }
            : new [] { right, left };

        InteractionsHandler.RegisterPointers(pointers[0], pointers[1]);
        OverlayRegistry.Register(pointers[0]);
        OverlayRegistry.Register(pointers[1]);
    }

    public static void UseOpenXR()
    {
        Current = new OXRBackend();
    }
}