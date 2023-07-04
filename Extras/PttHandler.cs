using WlxOverlay.Core.Interactions;
using WlxOverlay.Core.Interactions.Internal;
using WlxOverlay.Types;

namespace WlxOverlay.Extras;

public static class PttHandler
{
    public static void Add(LeftRight hand)
    {
        var dnCmd = hand == LeftRight.Left ? Config.Instance.LeftPttDnCmd : Config.Instance.RightPttDnCmd;
        var upCmd = hand == LeftRight.Left ? Config.Instance.LeftPttUpCmd : Config.Instance.RightPttUpCmd;

        var dnStart = Runner.StartInfoFromArgs(dnCmd);
        var upStart = Runner.StartInfoFromArgs(upCmd);
        
        InteractionsHandler.RegisterCustomInteraction($"PTT-{hand}", args =>
        {
            if (hand != args.Hand)
                return InteractionResult.Unhandled;

            if (!args.Before.AltClick && args.Now.AltClick)
            {
                if (upStart != null) 
                    Process.Start(upStart);
            }
            else if (args.Before.AltClick && !args.Now.AltClick)
            {
                if (dnStart != null)
                    Process.Start(dnStart);
            }
            
            return InteractionResult.OK;
        });
    }
}