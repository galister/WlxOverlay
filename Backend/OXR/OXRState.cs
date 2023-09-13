using Silk.NET.Core;
using Silk.NET.Core.Native;
using Silk.NET.OpenGL;
using Silk.NET.OpenXR;
using WlxOverlay.GFX;
using WlxOverlay.GFX.OpenGL;
using WlxOverlay.Numerics;
using Action = Silk.NET.OpenXR.Action;

namespace WlxOverlay.Backend.OXR;

public class OXRState
{
    public readonly XR Api;
    public Instance Instance;
    public ulong System;
    public Session Session;

    public Space PlaySpace;
    public ActionSet ActionSet;

    public long PredictedDisplayTime;
    public bool ShouldRender;

    public EnvironmentBlendMode BlendMode;
    public ViewConfigurationType ViewConfigType;

    public Swapchain Swapchain;
    public SwapchainImageOpenGLKHR[] SwapchainImages;

    public uint NumViews;
    public View[] Views;
    public CompositionLayerProjectionView[] ProjectionViews;

    private bool _sessionRunning;

    public OXRState()
    {
        Api = XR.GetApi();
    }

    public unsafe void CreateInstance(params string[] extensions)
    {
        var wantExtensions = new List<string>(extensions);
        uint propCount = 0;
        Api.EnumerateInstanceExtensionProperties((byte*)null, 0, &propCount, null);

        var props = new ExtensionProperties[propCount];
        for (var i = 0; i < props.Length; i++)
        {
            props[i].Type = StructureType.ExtensionProperties;
            props[i].Next = null;
        }

        Api.EnumerateInstanceExtensionProperties((byte*)null, propCount, &propCount, props);
        Console.WriteLine("Supported extensions (" + propCount + "):");

        var availableExtensions = new List<string>();
        for (var i = 0; i < props.Length; i++)
        {
            fixed (void* ptr = props[i].ExtensionName)
            {
                var extension_name = Marshal.PtrToStringAnsi(new IntPtr(ptr));
                if (extension_name == null)
                    continue;
                Console.WriteLine("  " + extension_name);
                availableExtensions.Add(extension_name);
            }
        }

        foreach (var t in wantExtensions)
            if (!availableExtensions.Contains(t))
                throw new ApplicationException("Required extension not available: " + t);

        var appInfo = new ApplicationInfo
        {
            ApiVersion = new Version64(1, 0, 23)
        };
        var appName = new Span<byte>(appInfo.ApplicationName, 128);
        var engName = new Span<byte>(appInfo.EngineName, 128);
        SilkMarshal.StringIntoSpan(AppDomain.CurrentDomain.FriendlyName, appName);
        SilkMarshal.StringIntoSpan("WlxOverlay", engName);

        var requestedExtensions = SilkMarshal.StringArrayToPtr(wantExtensions);
        var instanceCreateInfo = new InstanceCreateInfo
        (
            applicationInfo: appInfo,
            enabledExtensionCount: (uint)wantExtensions.Count,
            enabledExtensionNames: (byte**)requestedExtensions,
            createFlags: 0,
            enabledApiLayerCount: 0,
            enabledApiLayerNames: null
        );

        Instance instance;
        Api.CreateInstance(&instanceCreateInfo, &instance).EnsureSuccess();

        var properties = new InstanceProperties
        {
            Type = StructureType.InstanceProperties,
            Next = null,
        };
        Api.GetInstanceProperties(instance, ref properties).EnsureSuccess();

        var runtimeName = Marshal.PtrToStringAnsi(new IntPtr(properties.RuntimeName));
        var runtimeVersion = ((Version)(Version64)properties.RuntimeVersion).ToString(3);

        Console.WriteLine($"Using OpenXR Runtime \"{runtimeName}\" v{runtimeVersion}");

        Instance = instance;

        ulong system = 0;
        var getInfo = new SystemGetInfo(formFactor: FormFactor.HeadMountedDisplay)
        { Type = StructureType.SystemGetInfo };
        Api.GetSystem(instance, in getInfo, ref system).EnsureSuccess();

        System = system;
    }

    public unsafe void CreateSession()
    {
        var req = new GraphicsRequirementsOpenGLKHR
        {
            Type = StructureType.GraphicsRequirementsOpenglKhr,
            MinApiVersionSupported = new Version64(4, 5, 0).Value,
            MaxApiVersionSupported = new Version64(4, 6, 0).Value,
        };

        var pFnVoid = new PfnVoidFunction();
        Api.GetInstanceProcAddr(Instance, "xrGetOpenGLGraphicsRequirementsKHR", ref pFnVoid).EnsureSuccess();
        var pFn = Marshal.GetDelegateForFunctionPointer<xrGetOpenGLGraphicsRequirementsKHR>(pFnVoid);
        pFn(Instance, System, &req).EnsureSuccess();

        var glBinding = ((GlGraphicsEngine)GraphicsEngine.Instance).XrGraphicsBinding();

        var info = new SessionCreateInfo
        {
            Type = StructureType.SessionCreateInfo,
            SystemId = System,
            Next = &glBinding
        };

        Session session;
        Api.CreateSession(Instance, &info, &session).EnsureSuccess();
        Session = session;
    }

    public unsafe void GetViewConfig(out Vector2Int size, float renderScale = 1.0f)
    {
        uint viewConfigCount = 0;
        Api.EnumerateViewConfiguration(Instance, System, 0, ref viewConfigCount, null).EnsureSuccess();
        var viewConfigs = new ViewConfigurationType[viewConfigCount];
        fixed (ViewConfigurationType* ptr = &viewConfigs[0])
            Api.EnumerateViewConfiguration(Instance, System, viewConfigCount, ref viewConfigCount, ptr).EnsureSuccess();

        var viewTypeFound = false;
        for (var i = 0; i < viewConfigCount; i++)
            viewTypeFound |= ViewConfigType == viewConfigs[i];
        if (!viewTypeFound)
            throw new ApplicationException($"The current device doesn't support {ViewConfigType}");

        uint viewCount = 0;
        Api.EnumerateViewConfigurationView(Instance, System, ViewConfigType, 0, ref viewCount, null).EnsureSuccess();
        var viewConfigurationViews = new ViewConfigurationView[viewCount];
        for (var i = 0; i < viewCount; i++)
        {
            viewConfigurationViews[i].Type = StructureType.ViewConfigurationView;
            viewConfigurationViews[i].Next = null;
        }

        fixed (ViewConfigurationView* ptr = &viewConfigurationViews[0])
            Api.EnumerateViewConfigurationView(Instance, System, ViewConfigType, (uint)viewConfigurationViews.Length,
                ref viewCount, ptr).EnsureSuccess();

        size.X = (int)Math.Round(viewConfigurationViews[0].RecommendedImageRectWidth * renderScale) * 2;
        size.Y = (int)Math.Round(viewConfigurationViews[0].RecommendedImageRectHeight * renderScale);

        NumViews = viewCount;
    }

    public unsafe void CreateProjectionViews(Vector2Int size)
    {
        Views = new View[NumViews];
        for (var i = 0; i < NumViews; i++)
        {
            Views[i].Type = StructureType.View;
            Views[i].Next = null;
        }

        ProjectionViews = new CompositionLayerProjectionView[NumViews];
        for (var i = 0; i < NumViews; i++)
        {
            ProjectionViews[i].Type = StructureType.CompositionLayerProjectionView;
            ProjectionViews[i].Next = null;
            ProjectionViews[i].SubImage.Swapchain = Swapchain;
            ProjectionViews[i].SubImage.ImageArrayIndex = 0;
            ProjectionViews[i].SubImage.ImageRect.Offset.X = size.X * i / 2;
            ProjectionViews[i].SubImage.ImageRect.Offset.Y = 0;
            ProjectionViews[i].SubImage.ImageRect.Extent.Width = size.X / 2;
            ProjectionViews[i].SubImage.ImageRect.Extent.Height = size.Y;
        }
    }

    public unsafe void CreateSwapchain(Vector2Int size, InternalFormat format = InternalFormat.Srgb8Alpha8)
    {
        var info = new SwapchainCreateInfo
        {
            Type = StructureType.SwapchainCreateInfo,
            UsageFlags = SwapchainUsageFlags.TransferDstBit |
                         SwapchainUsageFlags.SampledBit |
                         SwapchainUsageFlags.ColorAttachmentBit,
            Format = (long)format,
            SampleCount = 1,
            Width = (uint)size.X,
            Height = (uint)size.Y,
            FaceCount = 1,
            ArraySize = 1,
            MipCount = 1,
            Next = null,
        };
        Swapchain swapchain;
        Api.CreateSwapchain(Session, info, &swapchain).EnsureSuccess();
        Console.WriteLine($"Created Swapchain of {format} {size}");
        Swapchain = swapchain;
    }

    public unsafe void EnumerateSwapchainImages()
    {
        var imgCount = 0u;
        Api.EnumerateSwapchainImages(Swapchain, 0, ref imgCount, null).EnsureSuccess();

        var images = new SwapchainImageOpenGLKHR[imgCount];
        for (var i = 0; i < imgCount; i++)
        {
            images[i].Type = StructureType.SwapchainImageOpenglKhr;
            images[i].Next = null;
        }

        fixed (void* ptr = &images[0])
            Api.EnumerateSwapchainImages(Swapchain, imgCount, ref imgCount, (SwapchainImageBaseHeader*)ptr)
                .EnsureSuccess();
        SwapchainImages = images;
    }

    public unsafe void CreateReferenceSpace(ReferenceSpaceType type, Transform3D poseInSpace)
    {
        var info = new ReferenceSpaceCreateInfo
        {
            Type = StructureType.ReferenceSpaceCreateInfo,
            Next = null,
            ReferenceSpaceType = type,
            PoseInReferenceSpace = poseInSpace.ToOxr(),
        };

        Api.CreateReferenceSpace(Session, &info, ref PlaySpace).EnsureSuccess();
    }

    public unsafe void CreateActionSet()
    {
        var info = new ActionSetCreateInfo
        {
            Type = StructureType.ActionSetCreateInfo,
            Next = null,
        };

        var actionSetName = new Span<byte>(info.ActionSetName, 16);
        var localizedName = new Span<byte>(info.LocalizedActionSetName, 16);
        SilkMarshal.StringIntoSpan("actionset\0", actionSetName);
        SilkMarshal.StringIntoSpan("ActionSet\0", localizedName);

        ActionSet actionSet;
        Api.CreateActionSet(Instance, &info, &actionSet).EnsureSuccess();
        ActionSet = actionSet;
    }

    public unsafe void AttachActionSet()
    {
        var array = new[] { ActionSet };
        fixed (ActionSet* ptr = array)
        {
            var info = new SessionActionSetsAttachInfo
            {
                Type = StructureType.SessionActionSetsAttachInfo,
                Next = null,
                CountActionSets = (uint)array.Length,
                ActionSets = ptr
            };
            Api.AttachSessionActionSets(Session, &info).EnsureSuccess();
        }
    }

    public unsafe Space CreateActionSpace(Action xrAction, Transform3D poseInSpace)
    {
        var info = new ActionSpaceCreateInfo
        {
            Type = StructureType.ActionSpaceCreateInfo,
            Next = null,
            Action = xrAction,
            PoseInActionSpace = poseInSpace.ToOxr(),
        };

        Space space;
        Api.CreateActionSpace(Session, &info, &space).EnsureSuccess();
        return space;
    }

    public unsafe bool TryGetBoolAction(Action xrAction, out bool value)
    {
        var info = new ActionStateGetInfo
        {
            Action = xrAction,
            Type = StructureType.ActionStateGetInfo
        };

        var result = new ActionStateBoolean
        {
            Type = StructureType.ActionStateBoolean
        };

        Api.GetActionStateBoolean(Session, &info, &result).LogOnFail();
        value = result.CurrentState != 0;

        return result.IsActive != 0;
    }

    public unsafe bool TryGetFloatAction(Action xrAction, out float value)
    {
        var info = new ActionStateGetInfo
        {
            Action = xrAction,
            Type = StructureType.ActionStateGetInfo
        };

        var result = new ActionStateFloat
        {
            Type = StructureType.ActionStateFloat
        };

        Api.GetActionStateFloat(Session, &info, &result).LogOnFail();
        value = result.CurrentState;

        return result.IsActive != 0;
    }

    public unsafe bool TryGetPoseAction(Action xrAction, Space space, long predictedDisplayTime, out Transform3D value)
    {
        var info = new ActionStateGetInfo
        {
            Action = xrAction,
            Type = StructureType.ActionStateGetInfo,
        };

        var result = new ActionStatePose
        {
            Type = StructureType.ActionStatePose
        };

        var xrVelocity = new SpaceVelocity
        {
            Type = StructureType.SpaceVelocity,
            Next = null
        };

        var xrLocation = new SpaceLocation
        {
            Type = StructureType.SpaceLocation,
            Next = &xrVelocity,
        };

        Api.GetActionStatePose(Session, in info, &result).LogOnFail();
        if (result.IsActive != 0)
        {
            Api.LocateSpace(space, PlaySpace, predictedDisplayTime, &xrLocation).EnsureSuccess();
            value = xrLocation.Pose.ToWlx();
            return true;
        }

        value = Transform3D.Identity;
        return false;
    }

    public unsafe Action CreateAction(ActionType type, string actionName)
    {
        var action_info = new ActionCreateInfo
        {
            Type = StructureType.ActionCreateInfo,
            ActionType = type,
        };

        var n = new Span<byte>(action_info.ActionName, 32);
        var l = new Span<byte>(action_info.LocalizedActionName, 32);
        var fullname = actionName + '\0';
        SilkMarshal.StringIntoSpan(fullname.ToLower(), n);
        SilkMarshal.StringIntoSpan(fullname, l);

        Action action;
        Api.CreateAction(ActionSet, &action_info, &action).LogOnFail();
        return action;
    }

    public unsafe bool SuggestInteractionProfileBinding(ulong profile, params ActionSuggestedBinding[] bindings)
    {
        fixed (ActionSuggestedBinding* ptr = &bindings[0])
        {
            var binding = new InteractionProfileSuggestedBinding
            {
                Type = StructureType.InteractionProfileSuggestedBinding,
                InteractionProfile = profile,
                CountSuggestedBindings = (uint)bindings.Length,
                SuggestedBindings = ptr
            };

            return Api.SuggestInteractionProfileBinding(Instance, &binding) == Result.Success;
        }
    }

    public unsafe void ApplyHapticFeedback(Action xrAction, ulong path, float durationSec, float amplitude, float frequencyHz)
    {
        var info = new HapticActionInfo
        {
            Type = StructureType.HapticActionInfo,
            Action = xrAction,
            Next = null,
            SubactionPath = path,
        };

        var haptic = new HapticVibration
        {
            Type = StructureType.HapticVibration,
            Next = null,
            Amplitude = amplitude,
            Duration = (long)(durationSec * 1_000_000_000L),
            Frequency = frequencyHz,
        };

        Api.ApplyHapticFeedback(Session, &info, (HapticBaseHeader*)(&haptic)).LogOnFail();
    }

    public unsafe void LocateView()
    {
        var info = new ViewLocateInfo
        {
            Type = StructureType.ViewLocateInfo,
            ViewConfigurationType = ViewConfigurationType.PrimaryStereo,
            DisplayTime = PredictedDisplayTime,
            Space = PlaySpace,
            Next = null,
        };
        var state = new ViewState
        {
            Type = StructureType.ViewState,
            Next = null,
        };

        var viewCount = NumViews;
        Api.LocateView(Session, &info, &state, viewCount, &viewCount, Views).EnsureSuccess();
        NumViews = viewCount;

        for (var i = 0; i < viewCount; i++)
        {
            ProjectionViews[i].Fov = Views[i].Fov;
            ProjectionViews[i].Pose = Views[i].Pose;
        }
    }

    public unsafe void WaitFrame()
    {
        var state = new FrameState
        {
            Type = StructureType.FrameState,
            Next = null,
        };
        Api.WaitFrame(Session, null, &state).EnsureSuccess();

        PredictedDisplayTime = state.PredictedDisplayTime;
        ShouldRender = (Bool32)state.ShouldRender;
    }


    public unsafe void BeginFrame()
    {
        Api.BeginFrame(Session, null).EnsureSuccess();
    }

    public unsafe void EndFrame(bool nullFrame = false)
    {
        var projections = stackalloc CompositionLayerProjection[1];
        var numProjections = 0U;

        fixed (CompositionLayerProjectionView* ptrView = ProjectionViews)
        {
            if (!nullFrame)
            {
                projections[0] = new CompositionLayerProjection
                {
                    Type = StructureType.CompositionLayerProjection,
                    Next = null,
                    LayerFlags = CompositionLayerFlags.None,
                    Space = PlaySpace,
                    ViewCount = NumViews,
                    Views = ptrView
                };
                numProjections = 1;
            }

            var frameEndInfo = new FrameEndInfo
            {
                Type = StructureType.FrameEndInfo,
                DisplayTime = PredictedDisplayTime,
                EnvironmentBlendMode = BlendMode,
                LayerCount = numProjections,
                Layers = (CompositionLayerBaseHeader**)&projections,
                Next = null,
            };

            Api.EndFrame(Session, &frameEndInfo).EnsureSuccess();
        }
    }

    public unsafe bool TryAcquireSwapchainImage(out uint swapchainIndex)
    {
        var index = 0u;
        Api.AcquireSwapchainImage(Swapchain, null, &index).EnsureSuccess();
        swapchainIndex = index;

        var waitInfo = new SwapchainImageWaitInfo(timeout: long.MaxValue)
        {
            Type = StructureType.SwapchainImageWaitInfo,
            Next = null,
        };

        if (Api.WaitSwapchainImage(Swapchain, in waitInfo) == Result.Success)
            return true;

        var releaseInfo = new SwapchainImageReleaseInfo
        {
            Type = StructureType.SwapchainImageReleaseInfo,
            Next = null,
        };
        Api.ReleaseSwapchainImage(Swapchain, &releaseInfo).EnsureSuccess();
        return false;
    }

    public unsafe void ReleaseSwapchainImage()
    {
        Api.ReleaseSwapchainImage(Swapchain, null);
    }

    public unsafe bool PollEvent(out EventDataBuffer eventData)
    {
        var buffer = new EventDataBuffer
        {
            Type = StructureType.EventDataBuffer,
            Next = null
        };
        var result = Api.PollEvent(Instance, &buffer);
        eventData = buffer;
        return result == Result.Success;
    }

    public unsafe ulong StringToPath(string pathString)
    {
        ulong path;
        Api.StringToPath(Instance, pathString, &path);
        return path;
    }

    public unsafe string PathToString(ulong path)
    {
        var buf = stackalloc byte[256];

        var length = 0u;
        Api.PathToString(Instance, path, 256U, &length, buf);

        return Encoding.UTF8.GetString(buf, (int)length);
    }

    public unsafe ulong GetCurrentInteractionProfile(ulong topLevelPath)
    {
        var state = new InteractionProfileState
        {
            Type = StructureType.InteractionProfileState,
            Next = null
        };

        Api.GetCurrentInteractionProfile(Session, topLevelPath, &state).EnsureSuccess();
        return state.InteractionProfile;
    }

    public unsafe void BeginSession(bool overlay = false)
    {
        if (_sessionRunning)
            return;

        var overlayInfo = new SessionCreateInfoOverlayEXTX
        {
            Type = StructureType.SessionCreateInfoOverlayExtx,
            Next = null,
            CreateFlags = OverlaySessionCreateFlagsEXTX.None,
        };

        var info = new SessionBeginInfo
        {
            Type = StructureType.SessionBeginInfo,
            Next = overlay ? &overlayInfo : null,
            PrimaryViewConfigurationType = ViewConfigType
        };
        Api.BeginSession(Session, &info).EnsureSuccess();
        _sessionRunning = true;
    }

    public void EndSession()
    {
        if (!_sessionRunning)
            return;
        Api.EndSession(Session);
        _sessionRunning = false;
    }

    public void DestroySession() => Api.DestroySession(Session);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private unsafe delegate Result xrGetOpenGLGraphicsRequirementsKHR(Instance instance, ulong sys_id, GraphicsRequirementsOpenGLKHR* req);
}