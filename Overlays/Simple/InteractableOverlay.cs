using Valve.VR;
using X11Overlay.GFX;
using X11Overlay.Numerics;
using X11Overlay.Types;

namespace X11Overlay.Overlays.Simple;

/// <summary>
/// Base class for all overlays supporting pointer events.
/// </summary>
public abstract class InteractableOverlay : BaseOverlay
{
    internal LaserPointer? PrimaryPointer;
    protected List<PointerHit> HitsThisFrame = new(2);

    /// <summary>
    /// Transforms texture UV (rect) to overlay UV (square)
    /// </summary>
    protected Transform2D InteractionTransform;

    /// <summary>
    /// Transforms overlay UV (square) to texture UV (rect) 
    /// </summary>
    protected Transform2D InvInteractionTransform;

    protected InteractableOverlay(string key) : base(key) { }

    protected void UpdateInteractionTransform()
    {
        if (Texture == null)
            return;

        var w = Texture.GetWidth();
        var h = Texture.GetHeight();

        InteractionTransform = Transform2D.Identity;
        if (w > h)
        {
            InteractionTransform.y *= h / (float)w;
            InteractionTransform.origin = Vector2.Down * ((w - h) * 0.5f / w);
        }
        else if (h > w)
        {
            InteractionTransform.x *= w / (float)h;
            InteractionTransform.origin = Vector2.Right * ((h - w) * 0.5f / h);
        }

        InvInteractionTransform = InteractionTransform.AffineInverse();
    }

    protected internal override void Render()
    {
        if (Config.Instance.FallbackCursors)
        {
            foreach (var hit in HitsThisFrame)
            {
                var x = (int)(hit.uv.x * Texture!.GetWidth());
                var y = (int)(hit.uv.y * Texture!.GetHeight());
                var color = hit.pointer == PrimaryPointer
                    ? hit.pointer.Color
                    : hit.pointer.Color * 0.66f;

                DrawFallbackCursor(x, y, Vector3.One, 1);
                DrawFallbackCursor(x, y, color);
            }
        }
        HitsThisFrame.Clear();

        base.Render();
    }

    protected void EnsurePrimary(LaserPointer pointer)
    {
        if (PrimaryPointer != null)
        {
            if (PrimaryPointer == pointer)
                return;

            PrimaryPointer.OnPrimaryLost(this);
        }

        PrimaryPointer = pointer;
    }

    protected internal virtual void OnPointerHover(PointerHit hitData)
    {
        PrimaryPointer ??= hitData.pointer;
        HitsThisFrame.Add(hitData);
    }

    protected internal virtual void OnPointerLeft(LeftRight hand)
    {
        if (PrimaryPointer?.Hand == hand)
            PrimaryPointer = null;
    }

    protected internal virtual void OnPointerDown(PointerHit hitData)
    {
        EnsurePrimary(hitData.pointer);
    }

    protected internal virtual void OnPointerUp(PointerHit hitData)
    {

    }

    protected internal virtual void OnScroll(PointerHit hitData, float value)
    {

    }

    protected void DrawFallbackCursor(int x, int y, Vector3 color, int extraWidth = 0)
    {
        var halfSize = Math.Max((int)(Texture!.GetWidth() / 640f), 4) + extraWidth;
        var size = halfSize * 2 + 1;
        var sizePow2 = size * size;

        var array = new Vector3[sizePow2];
        for (var i = 0; i < sizePow2; i++)
            array[i] = color;

        x = (int)Math.Clamp(x - halfSize, 0, Texture!.GetWidth() - size - 1);
        y = (int)Math.Clamp(y - halfSize, 0, Texture!.GetHeight() - size - 1);

        unsafe
        {
            fixed (Vector3* pArray = array)
            {
                var ptr = new IntPtr(pArray);
                Texture!.LoadRawSubImage(ptr, GraphicsFormat.RGB_Float, x, y, size, size);
            }
        }
    }
    protected void DrawFallbackCross(int x, int y, Vector3 color, int extraWidth = 0)
    {
        var halfSize = Math.Max((int)(Texture!.GetWidth() / 640f), 4) + extraWidth;
        var size = halfSize * 2 + 1;

        var array = new Vector3[size];
        for (var i = 0; i < size; i++)
            array[i] = color;

        x = (int)Math.Clamp(x - halfSize, 0, Texture!.GetWidth() - size - 1);
        y = (int)Math.Clamp(y - halfSize, 0, Texture!.GetHeight() - size - 1);

        unsafe
        {
            fixed (Vector3* pArray = array)
            {
                var ptr = new IntPtr(pArray);
                Texture!.LoadRawSubImage(ptr, GraphicsFormat.RGB_Float, x + halfSize, y, 1, size);
                Texture!.LoadRawSubImage(ptr, GraphicsFormat.RGB_Float, x, y + halfSize, size, 1);
            }
        }
    }

    public bool TryTransformToLocal(Vector2 uvIn, out Vector2 uvOut)
    {
        var uv = InvInteractionTransform * uvIn;
        if (uv.x is < 0f or > 1f
            || uv.y is < 0f or > 1f)
        {
            uvOut = default;
            return false;
        }

        uvOut = uv;
        return true;
    }

    protected internal Transform3D CurvedSurfaceTransformFromUv(Vector2 localUv)
    {
        var ovrUv = InteractionTransform * localUv - new Vector2(0.5f, 0.5f);

        var tCursor = Transform.TranslatedLocal(new Vector3(WidthInMeters * ovrUv.x, WidthInMeters * ovrUv.y, 0));

        if (Mathf.Abs(Curvature) < float.Epsilon)
            return tCursor;

        var theta = Mathf.Pi * 4f * Curvature;
        var halfTheta = theta / 2f;
        var r = WidthInMeters * 2 / theta;

        var tOrigin = Transform.TranslatedLocal(Vector3.Back * r);
        tOrigin.origin.y = tCursor.origin.y;

        var offsetAngle = ovrUv.x * halfTheta;
        tCursor = tOrigin.RotatedLocal(Vector3.Up, -offsetAngle)
            .TranslatedLocal(Vector3.Forward * r);

        return tCursor;
    }
}