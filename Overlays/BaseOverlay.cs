using WlxOverlay.Backend;
using WlxOverlay.Core;
using WlxOverlay.GFX;
using WlxOverlay.Numerics;

namespace WlxOverlay.Overlays;

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

    public bool SnapUpright;
    public bool CurveWhenUpright;
    public Vector3 SpawnPosition = Vector3.Forward;
    public Vector3? SavedSpawnPosition;

    public bool WantVisible;
    public bool Visible { get; private set; }

    protected float Brightness = 1f;
    private bool _initialized;
    public readonly IOverlay? _overlay;

    public readonly string Key;
    public Vector3 LocalScale = Vector3.One;
    
    private const string Prefix = "WlxOverlay_";

    protected BaseOverlay(string key)
    {
        Key = Prefix + key;
        _overlay = XrBackend.Current.CreateOverlay(this);
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
    protected virtual void Initialize()
    {
    }

    private const float FarDistance = 15f;
    private const float NearDistance = 0.35f;
    private const float FarResetDistance = 20f;
    private const float NearResetDistance = 0.2f;
    
    public virtual void Show()
    {
        if (SavedSpawnPosition.HasValue)
        {
            var len = SavedSpawnPosition.Value.Length();
            if (len is > FarResetDistance or < NearResetDistance)
                SavedSpawnPosition = SpawnPosition;

            var globalRef = XrBackend.Current.Input.HmdTransform.TranslatedLocal(SavedSpawnPosition.Value);

            Transform.origin = globalRef.origin;
            OnOrientationChanged();
        }

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

        Visible = true;
        _overlay!.Show();
        UploadTransform();
        UploadCurvature();
        UploadSortOrder();
        UploadAlpha();
        UploadColor();
        UploadWidth();
    }

    public virtual void Hide()
    {
        Visible = false;
        _overlay!.Hide();
    }

    protected internal virtual void AfterInput() { }

    protected internal virtual void Render()
    {
        _overlay!.Render();
    }

    public virtual void SetBrightness(float brightness)
    {
        Brightness = brightness;
        UploadColor();
    }

    protected void UploadAlpha() => _overlay!.SetAlpha(Alpha);

    private void UploadCurvature() => _overlay!.SetCurvature(Curvature);

    public virtual void UploadTransform()
    {
        _overlay!.SetTransform(Transform);
    }

    private void UploadSortOrder() => _overlay!.SetZOrder(ZOrder);

    private void UploadWidth() => _overlay!.SetWidth(WidthInMeters);

    protected void UploadColor() => _overlay!.SetColor(Color * Brightness);


    public void OnOrientationChanged()
    {
        var tHmd = XrBackend.Current.Input.HmdTransform;
        var vRela = Transform.origin - tHmd.origin;
        var lookPoint = Transform.Translated(vRela).origin;

        if (SnapUpright)
        {
            lookPoint.y = Transform.origin.y;
            Transform = Transform.LookingAt(lookPoint, Vector3.Up).ScaledLocal(LocalScale);

            if (CurveWhenUpright)
            {
                Curvature = 0.2f;
                UploadCurvature();
                UploadTransform();
                return;
            }
        }
        else
        {
            Vector3 upDirection;
            if (Mathf.Abs(tHmd.basis.x.Dot(Vector3.Up)) > 0.2f)
                upDirection = tHmd.basis.y;
            else
            {
                var dot = vRela.Normalized().Dot(tHmd.basis.z);
                var zDist = vRela.Length();

                var yDist = Mathf.Abs(Transform.origin.y - tHmd.origin.y);
                var xAngle = Mathf.Asin(yDist / zDist);

                if (dot < -float.Epsilon) // facing downwards
                {
                    var upPoint = tHmd.origin + zDist / Mathf.Cos(xAngle) * Vector3.Up;
                    upDirection = (upPoint - Transform.origin).Normalized();
                }
                else if (dot > float.Epsilon) // facing upwards
                {
                    var downPoint = tHmd.origin + zDist / Mathf.Cos(xAngle) * Vector3.Down;
                    upDirection = (Transform.origin - downPoint).Normalized();
                }
                else // perfectly upright
                    upDirection = Vector3.Up;
            }

            Transform = Transform.LookingAt(lookPoint, upDirection).ScaledLocal(LocalScale);
        }

        if (Curvature > float.Epsilon)
        {
            Curvature = 0;
            UploadCurvature();
        }
        UploadTransform();
    }

    public void ResetTransform()
    {
        if (SavedSpawnPosition == null)
            return;
        
        SavedSpawnPosition = SpawnPosition;
        var globalRef = XrBackend.Current.Input.HmdTransform.TranslatedLocal(SpawnPosition);
        Transform.origin = globalRef.origin;
        Transform.basis.x = Transform.basis.x.Normalized();
        Transform.basis.y = Transform.basis.y.Normalized();
        Transform.basis.z = Transform.basis.z.Normalized();
        WantVisible = true;
        OnOrientationChanged();
        if (!Visible)
            Show();
    }
    
    public virtual void Dispose()
    {
        Hide();
        _overlay?.Dispose();
        OverlayRegistry.Unregister(this);
    }
}
