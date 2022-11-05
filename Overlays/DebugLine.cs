#if DEBUG

using X11Overlay.Core;
using X11Overlay.GFX;
using X11Overlay.Numerics;
using X11Overlay.Overlays.Simple;

namespace X11Overlay.Overlays;

/// <summary>
/// Use this to help visualize issues during development
/// </summary>
public class DebugLine : BaseOverlay
{
    private static readonly Dictionary<Vector3, DebugLine> Lines = new();

    public static void Draw(string hexColor, Vector3 start, Vector3 end)
    {
        Draw(HexColor.FromRgb(hexColor), start, end);
    }
    
    public static void Draw(Vector3 color, Vector3 start, Vector3 end)
    {
        if (!Lines.TryGetValue(color, out var line)) 
            line = Lines[color] = new DebugLine(color);

        line.SetTransform(start, end);
        if (!line.Visible)
            line.Show();
        line.Render();
    }

    private Vector3 _color;

    private static ITexture? _sharedTexture;
    private DebugLine(Vector3 color) : base($"Debug-{color}")
    {
        ZOrder = 100;
        WidthInMeters = 0.002f;
        ShowHideBinding = false;
        WantVisible = true;
        _color = color;
    }
    
    public override void Initialize()
    {
        if (_sharedTexture == null)
        {
            var pixels = new byte[] { 255, 255, 255 };
            _sharedTexture = GraphicsEngine.Instance.TextureFromRaw(1, 1, GraphicsFormat.RGB8, pixels);
        }
        
        Texture = _sharedTexture;
        UploadColor(_color);
        Alpha = 0.5f;
        
        base.Initialize();
    }
    
    private static readonly float RotationOffset = Mathf.DegToRad(-90);
    private void SetTransform(Vector3 start, Vector3 end)
    {
        var hmd = InputManager.HmdTransform;
        var length = (end - start).Length();

        Transform = Transform3D.Identity.Translated(start)
            .LookingAt(end, Vector3.Up)
            .TranslatedLocal(Vector3.Forward * length * 0.5f)
            .RotatedLocal(Vector3.Right, RotationOffset)
            .ScaledLocal(new Vector3(1, length / WidthInMeters, 1));
        
        // billboard towards hmd
        var viewDirection = hmd.origin - start;

        var x1 = Transform.basis.z.Dot(viewDirection);
        var x2 = Transform.basis.x.Dot(viewDirection);

        var pies = (x1 - 1) * -0.5f * Mathf.Pi;
        if (x2 < 0)
            pies *= -1;

        Transform = Transform.RotatedLocal(Vector3.Up, pies);
        
        UploadTransform();
    }
}

#endif