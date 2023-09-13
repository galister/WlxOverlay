using Silk.NET.OpenXR;
using WlxOverlay.Numerics;
using WlxOverlay.Overlays;

namespace WlxOverlay.Backend.OXR;

public class OXRBackend : IXrBackend
{
    private readonly OXRState _oxr;
    private readonly OXRInput _input;
    private readonly OXRRenderer _renderer;
    public IXrInput Input => _input;

    private SessionState state;

    private uint swapchainIndex;

    public float DisplayFrequency { get; private set; }
    public IList<TrackedDevice> GetBatteryStates()
    {
        return new List<TrackedDevice>(); // TODO
    }

    public OXRBackend()
    {
        _oxr = new OXRState();
        _input = new OXRInput(_oxr);
        _renderer = new OXRRenderer(_oxr);

        _oxr.BlendMode = EnvironmentBlendMode.Opaque;
        _oxr.ViewConfigType = ViewConfigurationType.PrimaryStereo;
    }

    public void Initialize()
    {
        _oxr.CreateInstance("XR_KHR_opengl_enable", "XR_MNDX_egl_enable", "XR_KHR_composition_layer_depth", "XR_EXTX_overlay");
        _oxr.GetViewConfig(out var renderSize);
        _oxr.CreateSession();

        _oxr.CreateSwapchain(renderSize);
        _oxr.EnumerateSwapchainImages();

        _oxr.CreateProjectionViews(renderSize);
        _oxr.CreateReferenceSpace(ReferenceSpaceType.Local, Transform3D.Identity);

        _oxr.CreateActionSet();
        _input.Initialize();
        _oxr.AttachActionSet();

        _renderer.Initialize();

        DisplayFrequency = 90; // TODO
    }

    public LoopShould BeginFrame()
    {
        if (!HandleEvents())
            return LoopShould.Idle;

        if (state is < SessionState.Ready or > SessionState.Focused)
            return LoopShould.Idle;

        _oxr.WaitFrame();
        _oxr.BeginFrame();
        if (!_oxr.ShouldRender)
            return LoopShould.NotRender;

        if (!_oxr.TryAcquireSwapchainImage(out swapchainIndex))
            return LoopShould.NotRender;

        _oxr.LocateView();

        _input.Update();

        _renderer.Clear();

        return LoopShould.Render;
    }

    public void EndFrame(LoopShould should)
    {
        if (should != LoopShould.Render)
        {
            _oxr.EndFrame(nullFrame: true);
            return;
        }
        _renderer.Render(swapchainIndex);

        _oxr.ReleaseSwapchainImage();
        _oxr.EndFrame();
    }

    public void SetZeroPose(Vector3 offset)
    {
    }

    public void AdjustGain(int ch, float gain)
    {
    }

    public IOverlay CreateOverlay(BaseOverlay overlay)
    {
        return new OXROverlay(overlay, _renderer);
    }

    public void Destroy()
    {
    }

    private bool HandleEvents()
    {
        while (_oxr.PollEvent(out var eventData))
        {
            switch (eventData.Type)
            {
                case StructureType.EventDataInstanceLossPending:
                    {
                        var lossEvent = Unsafe.As<EventDataBuffer, EventDataInstanceLossPending>(ref eventData);
                        Console.WriteLine("[Err] OpenXR instance loss pending at " + lossEvent.LossTime + ". Destroying instance.");
                        return false;
                    }
                case StructureType.EventDataSessionStateChanged:
                    {
                        var sessionEvent = Unsafe.As<EventDataBuffer, EventDataSessionStateChanged>(ref eventData);
                        Console.WriteLine("[Info] OpenXR session state changed " + state + " -> " + sessionEvent.State);
                        state = sessionEvent.State;
                        switch (sessionEvent.State)
                        {
                            case SessionState.Idle:
                            case SessionState.Unknown:
                                {
                                    return false;
                                }
                            case SessionState.Ready:
                                {
                                    _oxr.BeginSession();
                                    return true;
                                }
                            case SessionState.Stopping:
                                {
                                    _oxr.EndSession();
                                    return false;
                                }
                            case SessionState.LossPending:
                            case SessionState.Exiting:
                                {
                                    _oxr.DestroySession();
                                    return false;
                                }
                        }
                        break;
                    }
                case StructureType.EventDataInteractionProfileChanged:
                    {
                        var topLevelPath = _oxr.StringToPath("/user/hand/left");
                        var profilePath = _oxr.GetCurrentInteractionProfile(topLevelPath);
                        var profileName = _oxr.PathToString(profilePath);

                        Console.WriteLine($"[Info] OpenXR interaction profile changed: {profileName}");
                        break;
                    }
                default:
                    {
                        Console.WriteLine("[Info] OpenXR event " + eventData.Type);
                        break;
                    }
            }
        }

        return true;
    }
}