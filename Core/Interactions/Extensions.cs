using WlxOverlay.Core.Interactions.Internal;

namespace WlxOverlay.Core.Interactions;

public static class Extensions
{
    public static void AddReleaseAction(this IPointer pointer, Action? action)
    {
        if (action != null)
            InteractionsHandler.AddPointerReleaseAction(pointer.Hand, action);
    }
}