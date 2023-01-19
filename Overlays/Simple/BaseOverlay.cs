using OVRSharp;
using Valve.VR;
using X11Overlay.GFX;
using X11Overlay.Numerics;

namespace X11Overlay.Overlays.Simple;

public class BaseOverlay : IDisposable
{
    public Transform3D Transform;
    public ITexture? Texture;
    
    public uint ZOrder = 0;
    public bool ShowHideBinding = true;
    
    public float WidthInMeters;
    public float Curvature;
    public float Alpha = 1f;
    public Vector3 Color = Vector3.One;

    public bool WantVisible;
    public bool Visible { get; protected set; }

    protected float Brightness = 1f;
    private bool _initialized;
    private bool _textureUploaded;
    private Overlay? _overlay;

    public readonly string Key;
    protected Vector3 LocalScale = Vector3.One;
    
    protected static HmdMatrix34_t HmdMatrix;
    protected static VROverlayIntersectionParams_t IntersectionParams = new() { eOrigin = ETrackingUniverseOrigin.TrackingUniverseStanding };
    protected static VROverlayIntersectionResults_t IntersectionResults;

    private const string Prefix = "X11Overlay_";

    public BaseOverlay(string key)
    {
        Key = Prefix+key;
    }

    public ulong Handle => _overlay?.Handle ?? OpenVR.k_ulOverlayHandleInvalid;

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
    protected virtual void Initialize()
    {
    }

    public virtual void ResetPosition()
    {
    }
    
    public virtual void Show()
    {
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
        
        if (Handle == OpenVR.k_ulOverlayHandleInvalid)
        {
            _overlay = new Overlay(Key, Key);
            if (Alpha < 1f)
                UploadAlpha();
            if (Color != Vector3.One || Brightness < 1f - float.Epsilon)
                UploadColor();
            if (Curvature != 0f)
                UploadCurvature();
            
            UploadSortOrder();
            UploadTransform();
            UploadWidth();
        }
        

        if (!_textureUploaded && !Texture.IsDynamic())
        {
            UploadTexture();
            _textureUploaded = true;
        }
        
        Visible = true;
        _overlay!.Show();
    }

    public virtual void Hide()
    {
        Visible = false;
        if (_overlay == null)
            return;
        
        if (Texture!.IsDynamic())
        {
            // To prevent SteamVR from crashing on Linux, we destroy/recreate instead of hide/show.
            _overlay.Destroy();
            _overlay = null;
        }
        else // Overlays where the texture is only uploaded once do not seem to cause crash.
            _overlay.Hide();
    }
    
    protected internal virtual void AfterInput(bool batteryStateUpdated) { }
    
    protected internal virtual void Render()
    {
        if (Texture!.IsDynamic())
            UploadTexture();
    }

    public virtual void SetBrightness(float brightness)
    {
        Brightness = brightness;
        if (Handle != OpenVR.k_ulOverlayHandleInvalid)
            UploadColor();
    }

    protected void UploadAlpha()
    {
        if (_overlay == null)
            return;
        _overlay.Alpha = Alpha;
    }

    protected void UploadCurvature()
    {
        if (_overlay == null)
            return;
        _overlay.Curvature = Curvature;
    }

    protected void UploadTransform()
    {
        if (_overlay == null)
            return;
        Transform.CopyTo(ref HmdMatrix);
        _overlay!.Transform = HmdMatrix;
    }

    private void UploadSortOrder()
    {
        var err = OpenVR.Overlay.SetOverlaySortOrder(Handle, ZOrder);
        if (err != EVROverlayError.None)
            Console.WriteLine($"[Err] SetOverlayColor {Color}: " + OpenVR.Overlay.GetOverlayErrorNameFromEnum(err));
    }

    private void UploadWidth()
    {
        if (_overlay == null)
            return;
        _overlay!.WidthInMeters = WidthInMeters;
    }

    protected void UploadColor()
    {
        var err = OpenVR.Overlay.SetOverlayColor(Handle, Color.x * Brightness, Color.y * Brightness, Color.z * Brightness);
        if (err != EVROverlayError.None)
            Console.WriteLine($"[Err] SetOverlayColor {Color}: " + OpenVR.Overlay.GetOverlayErrorNameFromEnum(err));
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

        _overlay!.SetTexture(tex);
    }

    public virtual void Dispose()
    {
    }
}