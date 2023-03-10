using Newtonsoft.Json;
using WlxOverlay.Numerics;
using WlxOverlay.Overlays;
using WlxOverlay.Types;

namespace WlxOverlay.Core;

public class ChaperoneManager
{
    public static readonly ChaperoneManager Instance = new();

    public bool WantVisible;
    public float MaxAlpha = 1f;
    public float FadeDistance = 5f;
    public List<ChaperonePolygon> Polygons = new();

    private readonly List<ChaperoneLine> _chaperoneLines = new();

    public void Render()
    {
        var distance = HmdDistanceToChaperoneEdge();
        var alpha = Mathf.Clamp(1 - (distance - FadeDistance) / FadeDistance, 0, 1) * MaxAlpha;

        //foreach (var line in ChaperoneLines)
        //    line.Alpha = alpha;
    }

    public void PolygonsChanged()
    {
        var j = 0;
        foreach (var polygon in Polygons)
        {
            var maxIdx = polygon.Points.Count - 1;
            for (var i = 0; i < maxIdx; i++)
            {
                var start = polygon.Points[i];
                var end = polygon.Points[i + 1];

                ChaperoneLine line;
                if (j >= _chaperoneLines.Count)
                {
                    _chaperoneLines.Add(line = new ChaperoneLine { WantVisible = true });
                    OverlayManager.Instance.RegisterChild(line);
                    j++;
                }
                else
                    line = _chaperoneLines[j++];

                line.SetPoints(start, end);
                line.Color = polygon.Color;
            }
        }
        for (; j < _chaperoneLines.Count; j++)
        {
            var lastIdx = _chaperoneLines.Count - 1;
            var chap = _chaperoneLines[lastIdx];
            _chaperoneLines.RemoveAt(lastIdx);
            chap.Dispose();
        }
    }

    public float HmdDistanceToChaperoneEdge()
    {
        return _chaperoneLines.Count < 1
            ? float.MaxValue
            : _chaperoneLines.Min(x => x.DistanceTo(InputManager.HmdTransform.origin));
    }

    private Transform3D ReferenceTransform()
    {
        var pLeft = InputManager.PoseState["LeftHand"];
        var pRight = InputManager.PoseState["RightHand"];

        var baseTransform = Transform3D.Identity;
        baseTransform.origin = (pLeft.origin + pRight.origin) / 2;
        return baseTransform.LookingAt(pRight.origin, Vector3.Up);
    }

    public void LoadFromFile()
    {
        if (!Config.TryGetFile("chaperone.json", out var path))
            return;

        var transform = ReferenceTransform();

        try
        {
            var conf = JsonConvert.DeserializeObject<ChaperoneConfig>(File.ReadAllText(path));
            FadeDistance = conf!.FadeDistance;
            MaxAlpha = conf!.MaxAlpha;
            WantVisible = conf!.Visible;

            Polygons.Clear();
            if (conf.Polygons != null)
            {
                foreach (var poly in conf.Polygons)
                {
                    poly.Apply(transform);
                    Polygons.Add(poly);
                }
            }
            PolygonsChanged();
        }
        catch { /* */ }
    }

    public void SaveToFile()
    {
        var path = Path.Combine(Config.ConfigFolders.First(), "chaperone.json");

        var transform = ReferenceTransform().Inverse();
        var savedPolygons = new List<ChaperonePolygon>();
        foreach (var poly in Polygons)
        {
            var newPoly = new ChaperonePolygon
            {
                Color = poly.Color,
                Points = poly.Points.ToList()
            };
            newPoly.Apply(transform);
            savedPolygons.Add(newPoly);
        }

        var json = JsonConvert.SerializeObject(new ChaperoneConfig
        {
            Visible = WantVisible,
            Polygons = savedPolygons,
            MaxAlpha = MaxAlpha,
            FadeDistance = FadeDistance
        }, Formatting.Indented);
        File.WriteAllText(path, json);
    }

    private struct ChaperoneConfig
    {
        public bool Visible;
        public float FadeDistance;
        public float MaxAlpha;
        public List<ChaperonePolygon>? Polygons;
    }
}

public class ChaperonePolygon
{
    public Vector3 Color;
    public List<Vector3> Points = new();

    public void Apply(Transform3D transform)
    {
        Points = Points.Select(x => transform * x).ToList();
    }
}
