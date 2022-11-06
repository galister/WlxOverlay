using OVRSharp;
using Valve.VR;
using X11Overlay.GFX;
using X11Overlay.Numerics;

namespace X11Overlay.Overlays.Simple;

public class BaseOverlay : Overlay
{
    public new Transform3D Transform;
    public ITexture? Texture;
    
    public uint ZOrder = 0;
    public bool ShowHideBinding = true;
    
    public new float WidthInMeters;
    public new float Curvature;

    public bool WantVisible = false;
    public bool Visible { get; protected set; }

    private bool _initialized;
    private bool _textureUploaded;
    
    protected Vector3 LocalScale = Vector3.One;
    
    protected static HmdMatrix34_t HmdMatrix;
    protected static VROverlayIntersectionParams_t IntersectionParams = new() { eOrigin = ETrackingUniverseOrigin.TrackingUniverseStanding };
    protected static VROverlayIntersectionResults_t IntersectionResults;

    private const string Prefix = "X11Overlay_";

    public BaseOverlay(string key) : base(Prefix+key, Prefix+key)
    {
        
    }

    public void ToggleVisible()
    {
        if (Visible)
        {
            Hide();
            WantVisible = false;
        }
        else
        {
            Show();
            WantVisible = true;
        }
    }
    
    /// <summary>
    /// Runs before the showing for the first time.
    /// </summary>
    public virtual void Initialize()
    {
        UploadTransform();
        UploadWidth();
    }

    public virtual void ResetPosition()
    {
        
    }
    
    public new virtual void Show()
    {
        if (Handle == OpenVR.k_ulOverlayHandleInvalid)
            return;

        if (!_initialized)
        {
            Initialize();
            _initialized = true;
        }

        if (Texture == null)
        {
            Console.WriteLine($"Not showing {Key}: No texture set.");
            WantVisible = false;
            return;
        }

        OpenVR.Overlay.SetOverlaySortOrder(Handle, ZOrder);

        if (!_textureUploaded && !Texture.IsDynamic())
        {
            UploadTexture();
            _textureUploaded = true;
        }
        
        Visible = true;
        base.Show();
    }

    public new virtual void Hide()
    {
        Visible = false;
        base.Hide();
    }
    
    internal protected virtual void AfterInput(bool batteryStateUpdated) { }
    
    internal protected virtual void Render()
    {
        if (Texture!.IsDynamic())
            UploadTexture();
    }

    public void UploadCurvature()
    {
        base.Curvature = Curvature;
    }
    
    public void UploadTransform()
    {
        Transform.CopyTo(ref HmdMatrix);
        base.Transform = HmdMatrix;
    }

    public void UploadWidth()
    {
        base.WidthInMeters = WidthInMeters;
    }
    
    private void UploadTexture()
    {
        var tex = new Texture_t
        {
            handle = Texture!.GetNativeTexturePtr(),
            eType = GraphicsEngine.Instance.GetTextureType(),
            eColorSpace = EColorSpace.Auto
        };

        if (tex.handle == IntPtr.Zero)
        {
            Console.WriteLine("Cannot upload texture: Handle is null.");
            return;
        }

        SetTexture(tex);
    }
    
    protected void UploadColor(Vector3 color)
    {
        var err = OpenVR.Overlay.SetOverlayColor(Handle, color.x, color.y, color.z);
        if (err != EVROverlayError.None)
            Console.WriteLine($"[Err] SetOverlayColor {color}: " + OpenVR.Overlay.GetOverlayErrorNameFromEnum(err));
    }
}