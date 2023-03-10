using WlxOverlay.Numerics;
using WlxOverlay.Overlays.Simple;

namespace WlxOverlay.Overlays;

public class ChaperoneLine : BaseLine
{
    private static int _counter;

    public ChaperoneLine() : base($"Chaperone{_counter++}")
    {
    }

    public float DistanceTo(Vector3 p)
    {
        var pointToStart = Start - p;
        var pointToEnd = End - p;

        var hmdToLine = pointToStart.Cross(pointToEnd);
        var lineLength = (End - Start).Length();

        return hmdToLine.Length() / lineLength;
    }
}
