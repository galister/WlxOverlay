using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using static WaylandSharp.Client;

#nullable enable
namespace WaylandSharp
{
    internal readonly ref struct _WlProxy
    {
    }

    internal readonly ref struct _WlDisplay
    {
    }

    internal readonly ref struct _WlEventQueue
    {
    }

    public unsafe struct WlArray : IEquatable<WlArray>
    {
        private readonly _WlArray* _array;
        public int Size => _array->Size;
        public int Capacity => _array->Alloc;
        public IntPtr Data => (IntPtr)_array->Data;
        internal WlArray(_WlArray* array)
        {
            _array = array;
        }

        public bool Equals(WlArray other)
        {
            return _array == other._array;
        }

        public override bool Equals(object? obj)
        {
            return obj is WlArray array && Equals(array);
        }

        public override int GetHashCode()
        {
            return (int)_array;
        }

        public static bool operator ==(WlArray left, WlArray right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(WlArray left, WlArray right)
        {
            return !(left == right);
        }
    }

    public abstract unsafe class WlClientObject : IEquatable<WlClientObject>, IDisposable
    {
        internal readonly _WlProxy* _proxyObject;
        private GCHandle _dispatcherPin;
        private int _disposed;
        private readonly object _syncLock = new();
        private static readonly _WlDispatcherFuncT _eventSentinel = (_, _, _, _, _) => -1;
        private static readonly ConcurrentDictionary<ulong, WlClientObject> _objects = new();
        public IntPtr RawPointer => (IntPtr)_proxyObject;
        internal WlClientObject(_WlProxy* proxyObject)
        {
            _proxyObject = proxyObject;
            if (!_objects.TryAdd((ulong)proxyObject, this))
                throw new WlClientException("Attempted to track duplicate wl_proxy");
        }

        internal static WlClientObject GetObject(_WlProxy* proxyObject)
        {
            return _objects.TryGetValue((ulong)proxyObject, out var wlObj) ? wlObj : throw new WlClientException("Attempted to retrieve untracked wl_proxy");
        }

        internal static T GetObject<T>(_WlProxy* proxyObject)
            where T : WlClientObject
        {
            return (T)GetObject(proxyObject);
        }

        public uint GetVersion()
        {
            CheckIfDisposed();
            return WlProxyGetVersion(_proxyObject);
        }

        public uint GetId()
        {
            CheckIfDisposed();
            return WlProxyGetId(_proxyObject);
        }

        protected void HookDispatcher()
        {
            lock (_syncLock)
            {
                if (_dispatcherPin.IsAllocated)
                    return;
                var dispatcher = CreateDispatcher();
                if (dispatcher == _eventSentinel)
                    throw new WlClientException("Dispatcher not implemented");
                HookDispatcher(dispatcher);
            }
        }

        internal virtual _WlDispatcherFuncT CreateDispatcher()
        {
            return _eventSentinel;
        }

        internal void HookDispatcher(_WlDispatcherFuncT dispatcher)
        {
            var handle = GCHandle.Alloc(dispatcher);
            _dispatcherPin = GCHandle.Alloc(handle);
            if (WlProxyAddDispatcher(_proxyObject, dispatcher, null, null) != 0)
                throw new WlClientException("Failed to add dispatcher to proxy");
        }

        public bool Equals(WlClientObject? other)
        {
            return other != null && _proxyObject == other._proxyObject;
        }

        public override bool Equals(object? obj)
        {
            return Equals(obj as WlClientObject);
        }

        public override int GetHashCode()
        {
            return (int)_proxyObject;
        }

        protected void CheckIfDisposed()
        {
            if (_disposed == 1)
                ThrowDisposed();
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 0)
            {
                _objects.Remove((ulong)_proxyObject, out _);
                Destroy(_proxyObject);
                if (_dispatcherPin.IsAllocated)
                    _dispatcherPin.Free();
            }

            GC.SuppressFinalize(this);
        }

        internal virtual void Destroy(_WlProxy* proxy)
        {
            WlProxyDestroy(proxy);
        }

        [DebuggerHidden, MethodImpl(MethodImplOptions.NoInlining)]
        private void ThrowDisposed()
        {
            throw new ObjectDisposedException(GetType().Name);
        }
    }

    public unsafe partial class WlDisplay : WlClientObject
    {
        /// <summary>
        /// Connects to a Wayland display, optionally using the given name.
        /// </summary>
        /// <param name = "name">The name of the wayland path to connect to.</param>
        /// <returns>An instance of <see cref = "WlDisplay"/>.</returns>
        /// <exception cref = "WlClientException">Thrown when we can't connect to the wayland display server.</exception>
        public static WlDisplay Connect(string? name = null)
        {
            var displayName = name != null ? (char*)Marshal.StringToHGlobalAnsi(name) : null;
            var display = WlDisplayConnect(displayName);
            if (display == null)
            {
                throw new WlClientException("Failed to connect to wayland display server");
            }

            return new WlDisplay((_WlProxy*)display);
        }

        public int Roundtrip()
        {
            CheckIfDisposed();
            return WlDisplayRoundtrip((_WlDisplay*)_proxyObject);
        }

        public int Dispatch()
        {
            CheckIfDisposed();
            return WlDisplayDispatch((_WlDisplay*)_proxyObject);
        }
    }

    public class WlClientException : Exception
    {
        public WlClientException(string message) : base(message)
        {
        }
    }

    public enum WlDisplayError : uint
    {
        InvalidObject = 0,
        InvalidMethod = 1,
        NoMemory = 2,
        Implementation = 3
    }

    public unsafe partial class WlDisplay : WlClientObject
    {
        internal WlDisplay(_WlProxy* proxyObject) : base(proxyObject)
        {
        }

        public class ErrorEventArgs : EventArgs
        {
            public WlClientObject ObjectId { get; }

            public uint Code { get; }

            public string Message { get; }

            public ErrorEventArgs(WlClientObject object_id, uint code, string message)
            {
                ObjectId = object_id;
                Code = code;
                Message = message;
            }
        }

        event EventHandler<ErrorEventArgs>? _error;
        public event EventHandler<ErrorEventArgs>? Error
        {
            add
            {
                CheckIfDisposed();
                _error += value;
                HookDispatcher();
            }

            remove => _error -= value;
        }

        public class DeleteIdEventArgs : EventArgs
        {
            public uint Id { get; }

            public DeleteIdEventArgs(uint id)
            {
                Id = id;
            }
        }

        event EventHandler<DeleteIdEventArgs>? _delete_id;
        public event EventHandler<DeleteIdEventArgs>? DeleteId
        {
            add
            {
                CheckIfDisposed();
                _delete_id += value;
                HookDispatcher();
            }

            remove => _delete_id -= value;
        }

        internal sealed override _WlDispatcherFuncT CreateDispatcher()
        {
            int dispatcher(void* data, void* target, uint callbackOpcode, _WlMessage* messageSignature, _WlArgument* args)
            {
                switch (callbackOpcode)
                {
                    case 0:
                        var errorArg0 = WlClientObject.GetObject((_WlProxy*)args[0].o);
                        var errorArg1 = args[1].u;
                        var errorArg2 = Marshal.PtrToStringAnsi((IntPtr)args[2].s)!;
                        _error?.Invoke(this, new ErrorEventArgs(errorArg0, errorArg1, errorArg2));
                        break;
                    case 1:
                        var delete_idArg0 = args[0].u;
                        _delete_id?.Invoke(this, new DeleteIdEventArgs(delete_idArg0));
                        break;
                    default:
                        throw new WlClientException("Unknown event opcode");
                }

                return 0;
            }

            return dispatcher;
        }

        public WlCallback Sync()
        {
            CheckIfDisposed();
            var interfacePtr = WlInterface.WlCallback.ToBlittable();
            var arg0 = (_WlProxy*)null;
            var newId = WlProxyMarshalFlags(_proxyObject, 0, interfacePtr, WlProxyGetVersion(_proxyObject), 0, arg0);
            return new WlCallback(newId);
        }

        public WlRegistry GetRegistry()
        {
            CheckIfDisposed();
            var interfacePtr = WlInterface.WlRegistry.ToBlittable();
            var arg0 = (_WlProxy*)null;
            var newId = WlProxyMarshalFlags(_proxyObject, 1, interfacePtr, WlProxyGetVersion(_proxyObject), 0, arg0);
            return new WlRegistry(newId);
        }
    }

    public unsafe partial class WlRegistry : WlClientObject
    {
        internal WlRegistry(_WlProxy* proxyObject) : base(proxyObject)
        {
        }

        public class GlobalEventArgs : EventArgs
        {
            public uint Name { get; }

            public string Interface { get; }

            public uint Version { get; }

            public GlobalEventArgs(uint name, string @interface, uint version)
            {
                Name = name;
                Interface = @interface;
                Version = version;
            }
        }

        event EventHandler<GlobalEventArgs>? _global;
        public event EventHandler<GlobalEventArgs>? Global
        {
            add
            {
                CheckIfDisposed();
                _global += value;
                HookDispatcher();
            }

            remove => _global -= value;
        }

        public class GlobalRemoveEventArgs : EventArgs
        {
            public uint Name { get; }

            public GlobalRemoveEventArgs(uint name)
            {
                Name = name;
            }
        }

        event EventHandler<GlobalRemoveEventArgs>? _global_remove;
        public event EventHandler<GlobalRemoveEventArgs>? GlobalRemove
        {
            add
            {
                CheckIfDisposed();
                _global_remove += value;
                HookDispatcher();
            }

            remove => _global_remove -= value;
        }

        internal sealed override _WlDispatcherFuncT CreateDispatcher()
        {
            int dispatcher(void* data, void* target, uint callbackOpcode, _WlMessage* messageSignature, _WlArgument* args)
            {
                switch (callbackOpcode)
                {
                    case 0:
                        var globalArg0 = args[0].u;
                        var globalArg1 = Marshal.PtrToStringAnsi((IntPtr)args[1].s)!;
                        var globalArg2 = args[2].u;
                        _global?.Invoke(this, new GlobalEventArgs(globalArg0, globalArg1, globalArg2));
                        break;
                    case 1:
                        var global_removeArg0 = args[0].u;
                        _global_remove?.Invoke(this, new GlobalRemoveEventArgs(global_removeArg0));
                        break;
                    default:
                        throw new WlClientException("Unknown event opcode");
                }

                return 0;
            }

            return dispatcher;
        }
    }

    public unsafe partial class WlCallback : WlClientObject
    {
        internal WlCallback(_WlProxy* proxyObject) : base(proxyObject)
        {
        }

        public class DoneEventArgs : EventArgs
        {
            public uint CallbackData { get; }

            public DoneEventArgs(uint callback_data)
            {
                CallbackData = callback_data;
            }
        }

        event EventHandler<DoneEventArgs>? _done;
        public event EventHandler<DoneEventArgs>? Done
        {
            add
            {
                CheckIfDisposed();
                _done += value;
                HookDispatcher();
            }

            remove => _done -= value;
        }

        internal sealed override _WlDispatcherFuncT CreateDispatcher()
        {
            int dispatcher(void* data, void* target, uint callbackOpcode, _WlMessage* messageSignature, _WlArgument* args)
            {
                switch (callbackOpcode)
                {
                    case 0:
                        var doneArg0 = args[0].u;
                        _done?.Invoke(this, new DoneEventArgs(doneArg0));
                        break;
                    default:
                        throw new WlClientException("Unknown event opcode");
                }

                return 0;
            }

            return dispatcher;
        }
    }

    public unsafe partial class WlCompositor : WlClientObject
    {
        internal WlCompositor(_WlProxy* proxyObject) : base(proxyObject)
        {
        }

        public WlSurface CreateSurface()
        {
            CheckIfDisposed();
            var interfacePtr = WlInterface.WlSurface.ToBlittable();
            var arg0 = (_WlProxy*)null;
            var newId = WlProxyMarshalFlags(_proxyObject, 0, interfacePtr, WlProxyGetVersion(_proxyObject), 0, arg0);
            return new WlSurface(newId);
        }

        public WlRegion CreateRegion()
        {
            CheckIfDisposed();
            var interfacePtr = WlInterface.WlRegion.ToBlittable();
            var arg0 = (_WlProxy*)null;
            var newId = WlProxyMarshalFlags(_proxyObject, 1, interfacePtr, WlProxyGetVersion(_proxyObject), 0, arg0);
            return new WlRegion(newId);
        }
    }

    public unsafe partial class WlShmPool : WlClientObject
    {
        internal WlShmPool(_WlProxy* proxyObject) : base(proxyObject)
        {
        }

        public WlBuffer CreateBuffer(int offset, int width, int height, int stride, WlShmFormat format)
        {
            CheckIfDisposed();
            var interfacePtr = WlInterface.WlBuffer.ToBlittable();
            var arg0 = (_WlProxy*)null;
            var arg1 = offset;
            var arg2 = width;
            var arg3 = height;
            var arg4 = stride;
            var arg5 = (uint)format;
            var newId = WlProxyMarshalFlags(_proxyObject, 0, interfacePtr, WlProxyGetVersion(_proxyObject), 0, arg0, arg1, arg2, arg3, arg4, arg5);
            return new WlBuffer(newId);
        }

        public void Destroy()
        {
            CheckIfDisposed();
            WlProxyMarshalFlags(_proxyObject, 1, null, WlProxyGetVersion(_proxyObject), 0);
        }

        public void Resize(int size)
        {
            CheckIfDisposed();
            var arg0 = size;
            WlProxyMarshalFlags(_proxyObject, 2, null, WlProxyGetVersion(_proxyObject), 0, arg0);
        }
    }

    public enum WlShmError : uint
    {
        InvalidFormat = 0,
        InvalidStride = 1,
        InvalidFd = 2
    }

    public enum WlShmFormat : uint
    {
        Argb8888 = 0,
        Xrgb8888 = 1,
        C8 = 538982467,
        Rgb332 = 943867730,
        Bgr233 = 944916290,
        Xrgb4444 = 842093144,
        Xbgr4444 = 842089048,
        Rgbx4444 = 842094674,
        Bgrx4444 = 842094658,
        Argb4444 = 842093121,
        Abgr4444 = 842089025,
        Rgba4444 = 842088786,
        Bgra4444 = 842088770,
        Xrgb1555 = 892424792,
        Xbgr1555 = 892420696,
        Rgbx5551 = 892426322,
        Bgrx5551 = 892426306,
        Argb1555 = 892424769,
        Abgr1555 = 892420673,
        Rgba5551 = 892420434,
        Bgra5551 = 892420418,
        Rgb565 = 909199186,
        Bgr565 = 909199170,
        Rgb888 = 875710290,
        Bgr888 = 875710274,
        Xbgr8888 = 875709016,
        Rgbx8888 = 875714642,
        Bgrx8888 = 875714626,
        Abgr8888 = 875708993,
        Rgba8888 = 875708754,
        Bgra8888 = 875708738,
        Xrgb2101010 = 808669784,
        Xbgr2101010 = 808665688,
        Rgbx1010102 = 808671314,
        Bgrx1010102 = 808671298,
        Argb2101010 = 808669761,
        Abgr2101010 = 808665665,
        Rgba1010102 = 808665426,
        Bgra1010102 = 808665410,
        Yuyv = 1448695129,
        Yvyu = 1431918169,
        Uyvy = 1498831189,
        Vyuy = 1498765654,
        Ayuv = 1448433985,
        Nv12 = 842094158,
        Nv21 = 825382478,
        Nv16 = 909203022,
        Nv61 = 825644622,
        Yuv410 = 961959257,
        Yvu410 = 961893977,
        Yuv411 = 825316697,
        Yvu411 = 825316953,
        Yuv420 = 842093913,
        Yvu420 = 842094169,
        Yuv422 = 909202777,
        Yvu422 = 909203033,
        Yuv444 = 875713881,
        Yvu444 = 875714137,
        R8 = 538982482,
        R16 = 540422482,
        Rg88 = 943212370,
        Gr88 = 943215175,
        Rg1616 = 842221394,
        Gr1616 = 842224199,
        Xrgb16161616f = 1211388504,
        Xbgr16161616f = 1211384408,
        Argb16161616f = 1211388481,
        Abgr16161616f = 1211384385,
        Xyuv8888 = 1448434008,
        Vuy888 = 875713878,
        Vuy101010 = 808670550,
        Y210 = 808530521,
        Y212 = 842084953,
        Y216 = 909193817,
        Y410 = 808531033,
        Y412 = 842085465,
        Y416 = 909194329,
        Xvyu2101010 = 808670808,
        Xvyu1216161616 = 909334104,
        Xvyu16161616 = 942954072,
        Y0l0 = 810299481,
        X0l0 = 810299480,
        Y0l2 = 843853913,
        X0l2 = 843853912,
        Yuv4208bit = 942691673,
        Yuv42010bit = 808539481,
        Xrgb8888A8 = 943805016,
        Xbgr8888A8 = 943800920,
        Rgbx8888A8 = 943806546,
        Bgrx8888A8 = 943806530,
        Rgb888A8 = 943798354,
        Bgr888A8 = 943798338,
        Rgb565A8 = 943797586,
        Bgr565A8 = 943797570,
        Nv24 = 875714126,
        Nv42 = 842290766,
        P210 = 808530512,
        P010 = 808530000,
        P012 = 842084432,
        P016 = 909193296,
        Axbxgxrx106106106106 = 808534593,
        Nv15 = 892425806,
        Q410 = 808531025,
        Q401 = 825242705,
        Xrgb16161616 = 942953048,
        Xbgr16161616 = 942948952,
        Argb16161616 = 942953025,
        Abgr16161616 = 942948929
    }

    public unsafe partial class WlShm : WlClientObject
    {
        internal WlShm(_WlProxy* proxyObject) : base(proxyObject)
        {
        }

        public class FormatEventArgs : EventArgs
        {
            public WlShmFormat Format { get; }

            public FormatEventArgs(WlShmFormat format)
            {
                Format = format;
            }
        }

        event EventHandler<FormatEventArgs>? _format;
        public event EventHandler<FormatEventArgs>? Format
        {
            add
            {
                CheckIfDisposed();
                _format += value;
                HookDispatcher();
            }

            remove => _format -= value;
        }

        internal sealed override _WlDispatcherFuncT CreateDispatcher()
        {
            int dispatcher(void* data, void* target, uint callbackOpcode, _WlMessage* messageSignature, _WlArgument* args)
            {
                switch (callbackOpcode)
                {
                    case 0:
                        var formatArg0 = (WlShmFormat)args[0].u;
                        _format?.Invoke(this, new FormatEventArgs(formatArg0));
                        break;
                    default:
                        throw new WlClientException("Unknown event opcode");
                }

                return 0;
            }

            return dispatcher;
        }

        public WlShmPool CreatePool(int fd, int size)
        {
            CheckIfDisposed();
            var interfacePtr = WlInterface.WlShmPool.ToBlittable();
            var arg0 = (_WlProxy*)null;
            var arg1 = fd;
            var arg2 = size;
            var newId = WlProxyMarshalFlags(_proxyObject, 0, interfacePtr, WlProxyGetVersion(_proxyObject), 0, arg0, arg1, arg2);
            return new WlShmPool(newId);
        }
    }

    public unsafe partial class WlBuffer : WlClientObject
    {
        internal WlBuffer(_WlProxy* proxyObject) : base(proxyObject)
        {
        }

        public class ReleaseEventArgs : EventArgs
        {
            public ReleaseEventArgs()
            {
            }
        }

        event EventHandler<ReleaseEventArgs>? _release;
        public event EventHandler<ReleaseEventArgs>? Release
        {
            add
            {
                CheckIfDisposed();
                _release += value;
                HookDispatcher();
            }

            remove => _release -= value;
        }

        internal sealed override _WlDispatcherFuncT CreateDispatcher()
        {
            int dispatcher(void* data, void* target, uint callbackOpcode, _WlMessage* messageSignature, _WlArgument* args)
            {
                switch (callbackOpcode)
                {
                    case 0:
                        _release?.Invoke(this, new ReleaseEventArgs());
                        break;
                    default:
                        throw new WlClientException("Unknown event opcode");
                }

                return 0;
            }

            return dispatcher;
        }

        public void Destroy()
        {
            CheckIfDisposed();
            WlProxyMarshalFlags(_proxyObject, 0, null, WlProxyGetVersion(_proxyObject), 0);
        }
    }

    public enum WlDataOfferError : uint
    {
        InvalidFinish = 0,
        InvalidActionMask = 1,
        InvalidAction = 2,
        InvalidOffer = 3
    }

    public unsafe partial class WlDataOffer : WlClientObject
    {
        internal WlDataOffer(_WlProxy* proxyObject) : base(proxyObject)
        {
        }

        public class OfferEventArgs : EventArgs
        {
            public string MimeType { get; }

            public OfferEventArgs(string mime_type)
            {
                MimeType = mime_type;
            }
        }

        event EventHandler<OfferEventArgs>? _offer;
        public event EventHandler<OfferEventArgs>? Offer
        {
            add
            {
                CheckIfDisposed();
                _offer += value;
                HookDispatcher();
            }

            remove => _offer -= value;
        }

        public class SourceActionsEventArgs : EventArgs
        {
            public WlDataDeviceManagerDndAction SourceActions { get; }

            public SourceActionsEventArgs(WlDataDeviceManagerDndAction source_actions)
            {
                SourceActions = source_actions;
            }
        }

        event EventHandler<SourceActionsEventArgs>? _source_actions;
        public event EventHandler<SourceActionsEventArgs>? SourceActions
        {
            add
            {
                CheckIfDisposed();
                _source_actions += value;
                HookDispatcher();
            }

            remove => _source_actions -= value;
        }

        public class ActionEventArgs : EventArgs
        {
            public WlDataDeviceManagerDndAction DndAction { get; }

            public ActionEventArgs(WlDataDeviceManagerDndAction dnd_action)
            {
                DndAction = dnd_action;
            }
        }

        event EventHandler<ActionEventArgs>? _action;
        public event EventHandler<ActionEventArgs>? Action
        {
            add
            {
                CheckIfDisposed();
                _action += value;
                HookDispatcher();
            }

            remove => _action -= value;
        }

        internal sealed override _WlDispatcherFuncT CreateDispatcher()
        {
            int dispatcher(void* data, void* target, uint callbackOpcode, _WlMessage* messageSignature, _WlArgument* args)
            {
                switch (callbackOpcode)
                {
                    case 0:
                        var offerArg0 = Marshal.PtrToStringAnsi((IntPtr)args[0].s)!;
                        _offer?.Invoke(this, new OfferEventArgs(offerArg0));
                        break;
                    case 1:
                        var source_actionsArg0 = (WlDataDeviceManagerDndAction)args[0].u;
                        _source_actions?.Invoke(this, new SourceActionsEventArgs(source_actionsArg0));
                        break;
                    case 2:
                        var actionArg0 = (WlDataDeviceManagerDndAction)args[0].u;
                        _action?.Invoke(this, new ActionEventArgs(actionArg0));
                        break;
                    default:
                        throw new WlClientException("Unknown event opcode");
                }

                return 0;
            }

            return dispatcher;
        }

        public void Accept(uint serial, string mime_type)
        {
            CheckIfDisposed();
            var arg0 = serial;
            var arg1 = (char*)Marshal.StringToHGlobalAnsi(mime_type)!;
            WlProxyMarshalFlags(_proxyObject, 0, null, WlProxyGetVersion(_proxyObject), 0, arg0, arg1);
        }

        public void Receive(string mime_type, int fd)
        {
            CheckIfDisposed();
            var arg0 = (char*)Marshal.StringToHGlobalAnsi(mime_type)!;
            var arg1 = fd;
            WlProxyMarshalFlags(_proxyObject, 1, null, WlProxyGetVersion(_proxyObject), 0, arg0, arg1);
        }

        public void Destroy()
        {
            CheckIfDisposed();
            WlProxyMarshalFlags(_proxyObject, 2, null, WlProxyGetVersion(_proxyObject), 0);
        }

        public void Finish()
        {
            CheckIfDisposed();
            WlProxyMarshalFlags(_proxyObject, 3, null, WlProxyGetVersion(_proxyObject), 0);
        }

        public void SetActions(WlDataDeviceManagerDndAction dnd_actions, WlDataDeviceManagerDndAction preferred_action)
        {
            CheckIfDisposed();
            var arg0 = (uint)dnd_actions;
            var arg1 = (uint)preferred_action;
            WlProxyMarshalFlags(_proxyObject, 4, null, WlProxyGetVersion(_proxyObject), 0, arg0, arg1);
        }
    }

    public enum WlDataSourceError : uint
    {
        InvalidActionMask = 0,
        InvalidSource = 1
    }

    public unsafe partial class WlDataSource : WlClientObject
    {
        internal WlDataSource(_WlProxy* proxyObject) : base(proxyObject)
        {
        }

        public class TargetEventArgs : EventArgs
        {
            public string MimeType { get; }

            public TargetEventArgs(string mime_type)
            {
                MimeType = mime_type;
            }
        }

        event EventHandler<TargetEventArgs>? _target;
        public event EventHandler<TargetEventArgs>? Target
        {
            add
            {
                CheckIfDisposed();
                _target += value;
                HookDispatcher();
            }

            remove => _target -= value;
        }

        public class SendEventArgs : EventArgs
        {
            public string MimeType { get; }

            public int Fd { get; }

            public SendEventArgs(string mime_type, int fd)
            {
                MimeType = mime_type;
                Fd = fd;
            }
        }

        event EventHandler<SendEventArgs>? _send;
        public event EventHandler<SendEventArgs>? Send
        {
            add
            {
                CheckIfDisposed();
                _send += value;
                HookDispatcher();
            }

            remove => _send -= value;
        }

        public class CancelledEventArgs : EventArgs
        {
            public CancelledEventArgs()
            {
            }
        }

        event EventHandler<CancelledEventArgs>? _cancelled;
        public event EventHandler<CancelledEventArgs>? Cancelled
        {
            add
            {
                CheckIfDisposed();
                _cancelled += value;
                HookDispatcher();
            }

            remove => _cancelled -= value;
        }

        public class DndDropPerformedEventArgs : EventArgs
        {
            public DndDropPerformedEventArgs()
            {
            }
        }

        event EventHandler<DndDropPerformedEventArgs>? _dnd_drop_performed;
        public event EventHandler<DndDropPerformedEventArgs>? DndDropPerformed
        {
            add
            {
                CheckIfDisposed();
                _dnd_drop_performed += value;
                HookDispatcher();
            }

            remove => _dnd_drop_performed -= value;
        }

        public class DndFinishedEventArgs : EventArgs
        {
            public DndFinishedEventArgs()
            {
            }
        }

        event EventHandler<DndFinishedEventArgs>? _dnd_finished;
        public event EventHandler<DndFinishedEventArgs>? DndFinished
        {
            add
            {
                CheckIfDisposed();
                _dnd_finished += value;
                HookDispatcher();
            }

            remove => _dnd_finished -= value;
        }

        public class ActionEventArgs : EventArgs
        {
            public WlDataDeviceManagerDndAction DndAction { get; }

            public ActionEventArgs(WlDataDeviceManagerDndAction dnd_action)
            {
                DndAction = dnd_action;
            }
        }

        event EventHandler<ActionEventArgs>? _action;
        public event EventHandler<ActionEventArgs>? Action
        {
            add
            {
                CheckIfDisposed();
                _action += value;
                HookDispatcher();
            }

            remove => _action -= value;
        }

        internal sealed override _WlDispatcherFuncT CreateDispatcher()
        {
            int dispatcher(void* data, void* target, uint callbackOpcode, _WlMessage* messageSignature, _WlArgument* args)
            {
                switch (callbackOpcode)
                {
                    case 0:
                        var targetArg0 = Marshal.PtrToStringAnsi((IntPtr)args[0].s)!;
                        _target?.Invoke(this, new TargetEventArgs(targetArg0));
                        break;
                    case 1:
                        var sendArg0 = Marshal.PtrToStringAnsi((IntPtr)args[0].s)!;
                        var sendArg1 = args[1].h;
                        _send?.Invoke(this, new SendEventArgs(sendArg0, sendArg1));
                        break;
                    case 2:
                        _cancelled?.Invoke(this, new CancelledEventArgs());
                        break;
                    case 3:
                        _dnd_drop_performed?.Invoke(this, new DndDropPerformedEventArgs());
                        break;
                    case 4:
                        _dnd_finished?.Invoke(this, new DndFinishedEventArgs());
                        break;
                    case 5:
                        var actionArg0 = (WlDataDeviceManagerDndAction)args[0].u;
                        _action?.Invoke(this, new ActionEventArgs(actionArg0));
                        break;
                    default:
                        throw new WlClientException("Unknown event opcode");
                }

                return 0;
            }

            return dispatcher;
        }

        public void Offer(string mime_type)
        {
            CheckIfDisposed();
            var arg0 = (char*)Marshal.StringToHGlobalAnsi(mime_type)!;
            WlProxyMarshalFlags(_proxyObject, 0, null, WlProxyGetVersion(_proxyObject), 0, arg0);
        }

        public void Destroy()
        {
            CheckIfDisposed();
            WlProxyMarshalFlags(_proxyObject, 1, null, WlProxyGetVersion(_proxyObject), 0);
        }

        public void SetActions(WlDataDeviceManagerDndAction dnd_actions)
        {
            CheckIfDisposed();
            var arg0 = (uint)dnd_actions;
            WlProxyMarshalFlags(_proxyObject, 2, null, WlProxyGetVersion(_proxyObject), 0, arg0);
        }
    }

    public enum WlDataDeviceError : uint
    {
        Role = 0
    }

    public unsafe partial class WlDataDevice : WlClientObject
    {
        internal WlDataDevice(_WlProxy* proxyObject) : base(proxyObject)
        {
        }

        public class DataOfferEventArgs : EventArgs
        {
            public WlDataOffer Id { get; }

            public DataOfferEventArgs(WlDataOffer id)
            {
                Id = id;
            }
        }

        event EventHandler<DataOfferEventArgs>? _data_offer;
        public event EventHandler<DataOfferEventArgs>? DataOffer
        {
            add
            {
                CheckIfDisposed();
                _data_offer += value;
                HookDispatcher();
            }

            remove => _data_offer -= value;
        }

        public class EnterEventArgs : EventArgs
        {
            public uint Serial { get; }

            public WlSurface Surface { get; }

            public double X { get; }

            public double Y { get; }

            public WlDataOffer Id { get; }

            public EnterEventArgs(uint serial, WlSurface surface, double x, double y, WlDataOffer id)
            {
                Serial = serial;
                Surface = surface;
                X = x;
                Y = y;
                Id = id;
            }
        }

        event EventHandler<EnterEventArgs>? _enter;
        public event EventHandler<EnterEventArgs>? Enter
        {
            add
            {
                CheckIfDisposed();
                _enter += value;
                HookDispatcher();
            }

            remove => _enter -= value;
        }

        public class LeaveEventArgs : EventArgs
        {
            public LeaveEventArgs()
            {
            }
        }

        event EventHandler<LeaveEventArgs>? _leave;
        public event EventHandler<LeaveEventArgs>? Leave
        {
            add
            {
                CheckIfDisposed();
                _leave += value;
                HookDispatcher();
            }

            remove => _leave -= value;
        }

        public class MotionEventArgs : EventArgs
        {
            public uint Time { get; }

            public double X { get; }

            public double Y { get; }

            public MotionEventArgs(uint time, double x, double y)
            {
                Time = time;
                X = x;
                Y = y;
            }
        }

        event EventHandler<MotionEventArgs>? _motion;
        public event EventHandler<MotionEventArgs>? Motion
        {
            add
            {
                CheckIfDisposed();
                _motion += value;
                HookDispatcher();
            }

            remove => _motion -= value;
        }

        public class DropEventArgs : EventArgs
        {
            public DropEventArgs()
            {
            }
        }

        event EventHandler<DropEventArgs>? _drop;
        public event EventHandler<DropEventArgs>? Drop
        {
            add
            {
                CheckIfDisposed();
                _drop += value;
                HookDispatcher();
            }

            remove => _drop -= value;
        }

        public class SelectionEventArgs : EventArgs
        {
            public WlDataOffer Id { get; }

            public SelectionEventArgs(WlDataOffer id)
            {
                Id = id;
            }
        }

        event EventHandler<SelectionEventArgs>? _selection;
        public event EventHandler<SelectionEventArgs>? Selection
        {
            add
            {
                CheckIfDisposed();
                _selection += value;
                HookDispatcher();
            }

            remove => _selection -= value;
        }

        internal sealed override _WlDispatcherFuncT CreateDispatcher()
        {
            int dispatcher(void* data, void* target, uint callbackOpcode, _WlMessage* messageSignature, _WlArgument* args)
            {
                switch (callbackOpcode)
                {
                    case 0:
                        var data_offerArg0 = new WlDataOffer((_WlProxy*)args[0].n);
                        _data_offer?.Invoke(this, new DataOfferEventArgs(data_offerArg0));
                        break;
                    case 1:
                        var enterArg0 = args[0].u;
                        var enterArg1 = WlClientObject.GetObject<WlSurface>((_WlProxy*)args[1].o);
                        var enterArg2 = WlFixedToDouble(args[2].f);
                        var enterArg3 = WlFixedToDouble(args[3].f);
                        var enterArg4 = WlClientObject.GetObject<WlDataOffer>((_WlProxy*)args[4].o);
                        _enter?.Invoke(this, new EnterEventArgs(enterArg0, enterArg1, enterArg2, enterArg3, enterArg4));
                        break;
                    case 2:
                        _leave?.Invoke(this, new LeaveEventArgs());
                        break;
                    case 3:
                        var motionArg0 = args[0].u;
                        var motionArg1 = WlFixedToDouble(args[1].f);
                        var motionArg2 = WlFixedToDouble(args[2].f);
                        _motion?.Invoke(this, new MotionEventArgs(motionArg0, motionArg1, motionArg2));
                        break;
                    case 4:
                        _drop?.Invoke(this, new DropEventArgs());
                        break;
                    case 5:
                        var selectionArg0 = WlClientObject.GetObject<WlDataOffer>((_WlProxy*)args[0].o);
                        _selection?.Invoke(this, new SelectionEventArgs(selectionArg0));
                        break;
                    default:
                        throw new WlClientException("Unknown event opcode");
                }

                return 0;
            }

            return dispatcher;
        }

        public void StartDrag(WlDataSource source, WlSurface origin, WlSurface icon, uint serial)
        {
            CheckIfDisposed();
            var arg0 = source._proxyObject;
            var arg1 = origin._proxyObject;
            var arg2 = icon._proxyObject;
            var arg3 = serial;
            WlProxyMarshalFlags(_proxyObject, 0, null, WlProxyGetVersion(_proxyObject), 0, arg0, arg1, arg2, arg3);
        }

        public void SetSelection(WlDataSource source, uint serial)
        {
            CheckIfDisposed();
            var arg0 = source._proxyObject;
            var arg1 = serial;
            WlProxyMarshalFlags(_proxyObject, 1, null, WlProxyGetVersion(_proxyObject), 0, arg0, arg1);
        }

        public void Release()
        {
            CheckIfDisposed();
            WlProxyMarshalFlags(_proxyObject, 2, null, WlProxyGetVersion(_proxyObject), 0);
        }
    }

    public enum WlDataDeviceManagerDndAction : uint
    {
        None = 0,
        Copy = 1,
        Move = 2,
        Ask = 4
    }

    public unsafe partial class WlDataDeviceManager : WlClientObject
    {
        internal WlDataDeviceManager(_WlProxy* proxyObject) : base(proxyObject)
        {
        }

        public WlDataSource CreateDataSource()
        {
            CheckIfDisposed();
            var interfacePtr = WlInterface.WlDataSource.ToBlittable();
            var arg0 = (_WlProxy*)null;
            var newId = WlProxyMarshalFlags(_proxyObject, 0, interfacePtr, WlProxyGetVersion(_proxyObject), 0, arg0);
            return new WlDataSource(newId);
        }

        public WlDataDevice GetDataDevice(WlSeat seat)
        {
            CheckIfDisposed();
            var interfacePtr = WlInterface.WlDataDevice.ToBlittable();
            var arg0 = (_WlProxy*)null;
            var arg1 = seat._proxyObject;
            var newId = WlProxyMarshalFlags(_proxyObject, 1, interfacePtr, WlProxyGetVersion(_proxyObject), 0, arg0, arg1);
            return new WlDataDevice(newId);
        }
    }

    public enum WlShellError : uint
    {
        Role = 0
    }

    public unsafe partial class WlShell : WlClientObject
    {
        internal WlShell(_WlProxy* proxyObject) : base(proxyObject)
        {
        }

        public WlShellSurface GetShellSurface(WlSurface surface)
        {
            CheckIfDisposed();
            var interfacePtr = WlInterface.WlShellSurface.ToBlittable();
            var arg0 = (_WlProxy*)null;
            var arg1 = surface._proxyObject;
            var newId = WlProxyMarshalFlags(_proxyObject, 0, interfacePtr, WlProxyGetVersion(_proxyObject), 0, arg0, arg1);
            return new WlShellSurface(newId);
        }
    }

    public enum WlShellSurfaceResize : uint
    {
        None = 0,
        Top = 1,
        Bottom = 2,
        Left = 4,
        TopLeft = 5,
        BottomLeft = 6,
        Right = 8,
        TopRight = 9,
        BottomRight = 10
    }

    public enum WlShellSurfaceTransient : uint
    {
        Inactive = 1
    }

    public enum WlShellSurfaceFullscreenMethod : uint
    {
        Default = 0,
        Scale = 1,
        Driver = 2,
        Fill = 3
    }

    public unsafe partial class WlShellSurface : WlClientObject
    {
        internal WlShellSurface(_WlProxy* proxyObject) : base(proxyObject)
        {
        }

        public class PingEventArgs : EventArgs
        {
            public uint Serial { get; }

            public PingEventArgs(uint serial)
            {
                Serial = serial;
            }
        }

        event EventHandler<PingEventArgs>? _ping;
        public event EventHandler<PingEventArgs>? Ping
        {
            add
            {
                CheckIfDisposed();
                _ping += value;
                HookDispatcher();
            }

            remove => _ping -= value;
        }

        public class ConfigureEventArgs : EventArgs
        {
            public WlShellSurfaceResize Edges { get; }

            public int Width { get; }

            public int Height { get; }

            public ConfigureEventArgs(WlShellSurfaceResize edges, int width, int height)
            {
                Edges = edges;
                Width = width;
                Height = height;
            }
        }

        event EventHandler<ConfigureEventArgs>? _configure;
        public event EventHandler<ConfigureEventArgs>? Configure
        {
            add
            {
                CheckIfDisposed();
                _configure += value;
                HookDispatcher();
            }

            remove => _configure -= value;
        }

        public class PopupDoneEventArgs : EventArgs
        {
            public PopupDoneEventArgs()
            {
            }
        }

        event EventHandler<PopupDoneEventArgs>? _popup_done;
        public event EventHandler<PopupDoneEventArgs>? PopupDone
        {
            add
            {
                CheckIfDisposed();
                _popup_done += value;
                HookDispatcher();
            }

            remove => _popup_done -= value;
        }

        internal sealed override _WlDispatcherFuncT CreateDispatcher()
        {
            int dispatcher(void* data, void* target, uint callbackOpcode, _WlMessage* messageSignature, _WlArgument* args)
            {
                switch (callbackOpcode)
                {
                    case 0:
                        var pingArg0 = args[0].u;
                        _ping?.Invoke(this, new PingEventArgs(pingArg0));
                        break;
                    case 1:
                        var configureArg0 = (WlShellSurfaceResize)args[0].u;
                        var configureArg1 = args[1].i;
                        var configureArg2 = args[2].i;
                        _configure?.Invoke(this, new ConfigureEventArgs(configureArg0, configureArg1, configureArg2));
                        break;
                    case 2:
                        _popup_done?.Invoke(this, new PopupDoneEventArgs());
                        break;
                    default:
                        throw new WlClientException("Unknown event opcode");
                }

                return 0;
            }

            return dispatcher;
        }

        public void Pong(uint serial)
        {
            CheckIfDisposed();
            var arg0 = serial;
            WlProxyMarshalFlags(_proxyObject, 0, null, WlProxyGetVersion(_proxyObject), 0, arg0);
        }

        public void Move(WlSeat seat, uint serial)
        {
            CheckIfDisposed();
            var arg0 = seat._proxyObject;
            var arg1 = serial;
            WlProxyMarshalFlags(_proxyObject, 1, null, WlProxyGetVersion(_proxyObject), 0, arg0, arg1);
        }

        public void Resize(WlSeat seat, uint serial, WlShellSurfaceResize edges)
        {
            CheckIfDisposed();
            var arg0 = seat._proxyObject;
            var arg1 = serial;
            var arg2 = (uint)edges;
            WlProxyMarshalFlags(_proxyObject, 2, null, WlProxyGetVersion(_proxyObject), 0, arg0, arg1, arg2);
        }

        public void SetToplevel()
        {
            CheckIfDisposed();
            WlProxyMarshalFlags(_proxyObject, 3, null, WlProxyGetVersion(_proxyObject), 0);
        }

        public void SetTransient(WlSurface parent, int x, int y, WlShellSurfaceTransient flags)
        {
            CheckIfDisposed();
            var arg0 = parent._proxyObject;
            var arg1 = x;
            var arg2 = y;
            var arg3 = (uint)flags;
            WlProxyMarshalFlags(_proxyObject, 4, null, WlProxyGetVersion(_proxyObject), 0, arg0, arg1, arg2, arg3);
        }

        public void SetFullscreen(WlShellSurfaceFullscreenMethod method, uint framerate, WlOutput output)
        {
            CheckIfDisposed();
            var arg0 = (uint)method;
            var arg1 = framerate;
            var arg2 = output._proxyObject;
            WlProxyMarshalFlags(_proxyObject, 5, null, WlProxyGetVersion(_proxyObject), 0, arg0, arg1, arg2);
        }

        public void SetPopup(WlSeat seat, uint serial, WlSurface parent, int x, int y, WlShellSurfaceTransient flags)
        {
            CheckIfDisposed();
            var arg0 = seat._proxyObject;
            var arg1 = serial;
            var arg2 = parent._proxyObject;
            var arg3 = x;
            var arg4 = y;
            var arg5 = (uint)flags;
            WlProxyMarshalFlags(_proxyObject, 6, null, WlProxyGetVersion(_proxyObject), 0, arg0, arg1, arg2, arg3, arg4, arg5);
        }

        public void SetMaximized(WlOutput output)
        {
            CheckIfDisposed();
            var arg0 = output._proxyObject;
            WlProxyMarshalFlags(_proxyObject, 7, null, WlProxyGetVersion(_proxyObject), 0, arg0);
        }

        public void SetTitle(string title)
        {
            CheckIfDisposed();
            var arg0 = (char*)Marshal.StringToHGlobalAnsi(title)!;
            WlProxyMarshalFlags(_proxyObject, 8, null, WlProxyGetVersion(_proxyObject), 0, arg0);
        }

        public void SetClass(string class_)
        {
            CheckIfDisposed();
            var arg0 = (char*)Marshal.StringToHGlobalAnsi(class_)!;
            WlProxyMarshalFlags(_proxyObject, 9, null, WlProxyGetVersion(_proxyObject), 0, arg0);
        }
    }

    public enum WlSurfaceError : uint
    {
        InvalidScale = 0,
        InvalidTransform = 1,
        InvalidSize = 2,
        InvalidOffset = 3,
        DefunctRoleObject = 4
    }

    public unsafe partial class WlSurface : WlClientObject
    {
        internal WlSurface(_WlProxy* proxyObject) : base(proxyObject)
        {
        }

        public class EnterEventArgs : EventArgs
        {
            public WlOutput Output { get; }

            public EnterEventArgs(WlOutput output)
            {
                Output = output;
            }
        }

        event EventHandler<EnterEventArgs>? _enter;
        public event EventHandler<EnterEventArgs>? Enter
        {
            add
            {
                CheckIfDisposed();
                _enter += value;
                HookDispatcher();
            }

            remove => _enter -= value;
        }

        public class LeaveEventArgs : EventArgs
        {
            public WlOutput Output { get; }

            public LeaveEventArgs(WlOutput output)
            {
                Output = output;
            }
        }

        event EventHandler<LeaveEventArgs>? _leave;
        public event EventHandler<LeaveEventArgs>? Leave
        {
            add
            {
                CheckIfDisposed();
                _leave += value;
                HookDispatcher();
            }

            remove => _leave -= value;
        }

        public class PreferredBufferScaleEventArgs : EventArgs
        {
            public int Factor { get; }

            public PreferredBufferScaleEventArgs(int factor)
            {
                Factor = factor;
            }
        }

        event EventHandler<PreferredBufferScaleEventArgs>? _preferred_buffer_scale;
        public event EventHandler<PreferredBufferScaleEventArgs>? PreferredBufferScale
        {
            add
            {
                CheckIfDisposed();
                _preferred_buffer_scale += value;
                HookDispatcher();
            }

            remove => _preferred_buffer_scale -= value;
        }

        public class PreferredBufferTransformEventArgs : EventArgs
        {
            public WlOutputTransform Transform { get; }

            public PreferredBufferTransformEventArgs(WlOutputTransform transform)
            {
                Transform = transform;
            }
        }

        event EventHandler<PreferredBufferTransformEventArgs>? _preferred_buffer_transform;
        public event EventHandler<PreferredBufferTransformEventArgs>? PreferredBufferTransform
        {
            add
            {
                CheckIfDisposed();
                _preferred_buffer_transform += value;
                HookDispatcher();
            }

            remove => _preferred_buffer_transform -= value;
        }

        internal sealed override _WlDispatcherFuncT CreateDispatcher()
        {
            int dispatcher(void* data, void* target, uint callbackOpcode, _WlMessage* messageSignature, _WlArgument* args)
            {
                switch (callbackOpcode)
                {
                    case 0:
                        var enterArg0 = WlClientObject.GetObject<WlOutput>((_WlProxy*)args[0].o);
                        _enter?.Invoke(this, new EnterEventArgs(enterArg0));
                        break;
                    case 1:
                        var leaveArg0 = WlClientObject.GetObject<WlOutput>((_WlProxy*)args[0].o);
                        _leave?.Invoke(this, new LeaveEventArgs(leaveArg0));
                        break;
                    case 2:
                        var preferred_buffer_scaleArg0 = args[0].i;
                        _preferred_buffer_scale?.Invoke(this, new PreferredBufferScaleEventArgs(preferred_buffer_scaleArg0));
                        break;
                    case 3:
                        var preferred_buffer_transformArg0 = (WlOutputTransform)args[0].u;
                        _preferred_buffer_transform?.Invoke(this, new PreferredBufferTransformEventArgs(preferred_buffer_transformArg0));
                        break;
                    default:
                        throw new WlClientException("Unknown event opcode");
                }

                return 0;
            }

            return dispatcher;
        }

        public void Destroy()
        {
            CheckIfDisposed();
            WlProxyMarshalFlags(_proxyObject, 0, null, WlProxyGetVersion(_proxyObject), 0);
        }

        public void Attach(WlBuffer buffer, int x, int y)
        {
            CheckIfDisposed();
            var arg0 = buffer._proxyObject;
            var arg1 = x;
            var arg2 = y;
            WlProxyMarshalFlags(_proxyObject, 1, null, WlProxyGetVersion(_proxyObject), 0, arg0, arg1, arg2);
        }

        public void Damage(int x, int y, int width, int height)
        {
            CheckIfDisposed();
            var arg0 = x;
            var arg1 = y;
            var arg2 = width;
            var arg3 = height;
            WlProxyMarshalFlags(_proxyObject, 2, null, WlProxyGetVersion(_proxyObject), 0, arg0, arg1, arg2, arg3);
        }

        public WlCallback Frame()
        {
            CheckIfDisposed();
            var interfacePtr = WlInterface.WlCallback.ToBlittable();
            var arg0 = (_WlProxy*)null;
            var newId = WlProxyMarshalFlags(_proxyObject, 3, interfacePtr, WlProxyGetVersion(_proxyObject), 0, arg0);
            return new WlCallback(newId);
        }

        public void SetOpaqueRegion(WlRegion region)
        {
            CheckIfDisposed();
            var arg0 = region._proxyObject;
            WlProxyMarshalFlags(_proxyObject, 4, null, WlProxyGetVersion(_proxyObject), 0, arg0);
        }

        public void SetInputRegion(WlRegion region)
        {
            CheckIfDisposed();
            var arg0 = region._proxyObject;
            WlProxyMarshalFlags(_proxyObject, 5, null, WlProxyGetVersion(_proxyObject), 0, arg0);
        }

        public void Commit()
        {
            CheckIfDisposed();
            WlProxyMarshalFlags(_proxyObject, 6, null, WlProxyGetVersion(_proxyObject), 0);
        }

        public void SetBufferTransform(WlOutputTransform transform)
        {
            CheckIfDisposed();
            var arg0 = (int)transform;
            WlProxyMarshalFlags(_proxyObject, 7, null, WlProxyGetVersion(_proxyObject), 0, arg0);
        }

        public void SetBufferScale(int scale)
        {
            CheckIfDisposed();
            var arg0 = scale;
            WlProxyMarshalFlags(_proxyObject, 8, null, WlProxyGetVersion(_proxyObject), 0, arg0);
        }

        public void DamageBuffer(int x, int y, int width, int height)
        {
            CheckIfDisposed();
            var arg0 = x;
            var arg1 = y;
            var arg2 = width;
            var arg3 = height;
            WlProxyMarshalFlags(_proxyObject, 9, null, WlProxyGetVersion(_proxyObject), 0, arg0, arg1, arg2, arg3);
        }

        public void Offset(int x, int y)
        {
            CheckIfDisposed();
            var arg0 = x;
            var arg1 = y;
            WlProxyMarshalFlags(_proxyObject, 10, null, WlProxyGetVersion(_proxyObject), 0, arg0, arg1);
        }
    }

    public enum WlSeatCapability : uint
    {
        Pointer = 1,
        Keyboard = 2,
        Touch = 4
    }

    public enum WlSeatError : uint
    {
        MissingCapability = 0
    }

    public unsafe partial class WlSeat : WlClientObject
    {
        internal WlSeat(_WlProxy* proxyObject) : base(proxyObject)
        {
        }

        public class CapabilitiesEventArgs : EventArgs
        {
            public WlSeatCapability Capabilities { get; }

            public CapabilitiesEventArgs(WlSeatCapability capabilities)
            {
                Capabilities = capabilities;
            }
        }

        event EventHandler<CapabilitiesEventArgs>? _capabilities;
        public event EventHandler<CapabilitiesEventArgs>? Capabilities
        {
            add
            {
                CheckIfDisposed();
                _capabilities += value;
                HookDispatcher();
            }

            remove => _capabilities -= value;
        }

        public class NameEventArgs : EventArgs
        {
            public string Name { get; }

            public NameEventArgs(string name)
            {
                Name = name;
            }
        }

        event EventHandler<NameEventArgs>? _name;
        public event EventHandler<NameEventArgs>? Name
        {
            add
            {
                CheckIfDisposed();
                _name += value;
                HookDispatcher();
            }

            remove => _name -= value;
        }

        internal sealed override _WlDispatcherFuncT CreateDispatcher()
        {
            int dispatcher(void* data, void* target, uint callbackOpcode, _WlMessage* messageSignature, _WlArgument* args)
            {
                switch (callbackOpcode)
                {
                    case 0:
                        var capabilitiesArg0 = (WlSeatCapability)args[0].u;
                        _capabilities?.Invoke(this, new CapabilitiesEventArgs(capabilitiesArg0));
                        break;
                    case 1:
                        var nameArg0 = Marshal.PtrToStringAnsi((IntPtr)args[0].s)!;
                        _name?.Invoke(this, new NameEventArgs(nameArg0));
                        break;
                    default:
                        throw new WlClientException("Unknown event opcode");
                }

                return 0;
            }

            return dispatcher;
        }

        public WlPointer GetPointer()
        {
            CheckIfDisposed();
            var interfacePtr = WlInterface.WlPointer.ToBlittable();
            var arg0 = (_WlProxy*)null;
            var newId = WlProxyMarshalFlags(_proxyObject, 0, interfacePtr, WlProxyGetVersion(_proxyObject), 0, arg0);
            return new WlPointer(newId);
        }

        public WlKeyboard GetKeyboard()
        {
            CheckIfDisposed();
            var interfacePtr = WlInterface.WlKeyboard.ToBlittable();
            var arg0 = (_WlProxy*)null;
            var newId = WlProxyMarshalFlags(_proxyObject, 1, interfacePtr, WlProxyGetVersion(_proxyObject), 0, arg0);
            return new WlKeyboard(newId);
        }

        public WlTouch GetTouch()
        {
            CheckIfDisposed();
            var interfacePtr = WlInterface.WlTouch.ToBlittable();
            var arg0 = (_WlProxy*)null;
            var newId = WlProxyMarshalFlags(_proxyObject, 2, interfacePtr, WlProxyGetVersion(_proxyObject), 0, arg0);
            return new WlTouch(newId);
        }

        public void Release()
        {
            CheckIfDisposed();
            WlProxyMarshalFlags(_proxyObject, 3, null, WlProxyGetVersion(_proxyObject), 0);
        }
    }

    public enum WlPointerError : uint
    {
        Role = 0
    }

    public enum WlPointerButtonState : uint
    {
        Released = 0,
        Pressed = 1
    }

    public enum WlPointerAxis : uint
    {
        VerticalScroll = 0,
        HorizontalScroll = 1
    }

    public enum WlPointerAxisSource : uint
    {
        Wheel = 0,
        Finger = 1,
        Continuous = 2,
        WheelTilt = 3
    }

    public unsafe partial class WlPointer : WlClientObject
    {
        internal WlPointer(_WlProxy* proxyObject) : base(proxyObject)
        {
        }

        public class EnterEventArgs : EventArgs
        {
            public uint Serial { get; }

            public WlSurface Surface { get; }

            public double SurfaceX { get; }

            public double SurfaceY { get; }

            public EnterEventArgs(uint serial, WlSurface surface, double surface_x, double surface_y)
            {
                Serial = serial;
                Surface = surface;
                SurfaceX = surface_x;
                SurfaceY = surface_y;
            }
        }

        event EventHandler<EnterEventArgs>? _enter;
        public event EventHandler<EnterEventArgs>? Enter
        {
            add
            {
                CheckIfDisposed();
                _enter += value;
                HookDispatcher();
            }

            remove => _enter -= value;
        }

        public class LeaveEventArgs : EventArgs
        {
            public uint Serial { get; }

            public WlSurface Surface { get; }

            public LeaveEventArgs(uint serial, WlSurface surface)
            {
                Serial = serial;
                Surface = surface;
            }
        }

        event EventHandler<LeaveEventArgs>? _leave;
        public event EventHandler<LeaveEventArgs>? Leave
        {
            add
            {
                CheckIfDisposed();
                _leave += value;
                HookDispatcher();
            }

            remove => _leave -= value;
        }

        public class MotionEventArgs : EventArgs
        {
            public uint Time { get; }

            public double SurfaceX { get; }

            public double SurfaceY { get; }

            public MotionEventArgs(uint time, double surface_x, double surface_y)
            {
                Time = time;
                SurfaceX = surface_x;
                SurfaceY = surface_y;
            }
        }

        event EventHandler<MotionEventArgs>? _motion;
        public event EventHandler<MotionEventArgs>? Motion
        {
            add
            {
                CheckIfDisposed();
                _motion += value;
                HookDispatcher();
            }

            remove => _motion -= value;
        }

        public class ButtonEventArgs : EventArgs
        {
            public uint Serial { get; }

            public uint Time { get; }

            public uint Button { get; }

            public WlPointerButtonState State { get; }

            public ButtonEventArgs(uint serial, uint time, uint button, WlPointerButtonState state)
            {
                Serial = serial;
                Time = time;
                Button = button;
                State = state;
            }
        }

        event EventHandler<ButtonEventArgs>? _button;
        public event EventHandler<ButtonEventArgs>? Button
        {
            add
            {
                CheckIfDisposed();
                _button += value;
                HookDispatcher();
            }

            remove => _button -= value;
        }

        public class AxisEventArgs : EventArgs
        {
            public uint Time { get; }

            public WlPointerAxis Axis { get; }

            public double Value { get; }

            public AxisEventArgs(uint time, WlPointerAxis axis, double value)
            {
                Time = time;
                Axis = axis;
                Value = value;
            }
        }

        event EventHandler<AxisEventArgs>? _axis;
        public event EventHandler<AxisEventArgs>? Axis
        {
            add
            {
                CheckIfDisposed();
                _axis += value;
                HookDispatcher();
            }

            remove => _axis -= value;
        }

        public class FrameEventArgs : EventArgs
        {
            public FrameEventArgs()
            {
            }
        }

        event EventHandler<FrameEventArgs>? _frame;
        public event EventHandler<FrameEventArgs>? Frame
        {
            add
            {
                CheckIfDisposed();
                _frame += value;
                HookDispatcher();
            }

            remove => _frame -= value;
        }

        public class AxisSourceEventArgs : EventArgs
        {
            public WlPointerAxisSource AxisSource { get; }

            public AxisSourceEventArgs(WlPointerAxisSource axis_source)
            {
                AxisSource = axis_source;
            }
        }

        event EventHandler<AxisSourceEventArgs>? _axis_source;
        public event EventHandler<AxisSourceEventArgs>? AxisSource
        {
            add
            {
                CheckIfDisposed();
                _axis_source += value;
                HookDispatcher();
            }

            remove => _axis_source -= value;
        }

        public class AxisStopEventArgs : EventArgs
        {
            public uint Time { get; }

            public WlPointerAxis Axis { get; }

            public AxisStopEventArgs(uint time, WlPointerAxis axis)
            {
                Time = time;
                Axis = axis;
            }
        }

        event EventHandler<AxisStopEventArgs>? _axis_stop;
        public event EventHandler<AxisStopEventArgs>? AxisStop
        {
            add
            {
                CheckIfDisposed();
                _axis_stop += value;
                HookDispatcher();
            }

            remove => _axis_stop -= value;
        }

        public class AxisDiscreteEventArgs : EventArgs
        {
            public WlPointerAxis Axis { get; }

            public int Discrete { get; }

            public AxisDiscreteEventArgs(WlPointerAxis axis, int discrete)
            {
                Axis = axis;
                Discrete = discrete;
            }
        }

        event EventHandler<AxisDiscreteEventArgs>? _axis_discrete;
        public event EventHandler<AxisDiscreteEventArgs>? AxisDiscrete
        {
            add
            {
                CheckIfDisposed();
                _axis_discrete += value;
                HookDispatcher();
            }

            remove => _axis_discrete -= value;
        }

        public class AxisValue120EventArgs : EventArgs
        {
            public WlPointerAxis Axis { get; }

            public int Value120 { get; }

            public AxisValue120EventArgs(WlPointerAxis axis, int value120)
            {
                Axis = axis;
                Value120 = value120;
            }
        }

        event EventHandler<AxisValue120EventArgs>? _axis_value120;
        public event EventHandler<AxisValue120EventArgs>? AxisValue120
        {
            add
            {
                CheckIfDisposed();
                _axis_value120 += value;
                HookDispatcher();
            }

            remove => _axis_value120 -= value;
        }

        internal sealed override _WlDispatcherFuncT CreateDispatcher()
        {
            int dispatcher(void* data, void* target, uint callbackOpcode, _WlMessage* messageSignature, _WlArgument* args)
            {
                switch (callbackOpcode)
                {
                    case 0:
                        var enterArg0 = args[0].u;
                        var enterArg1 = WlClientObject.GetObject<WlSurface>((_WlProxy*)args[1].o);
                        var enterArg2 = WlFixedToDouble(args[2].f);
                        var enterArg3 = WlFixedToDouble(args[3].f);
                        _enter?.Invoke(this, new EnterEventArgs(enterArg0, enterArg1, enterArg2, enterArg3));
                        break;
                    case 1:
                        var leaveArg0 = args[0].u;
                        var leaveArg1 = WlClientObject.GetObject<WlSurface>((_WlProxy*)args[1].o);
                        _leave?.Invoke(this, new LeaveEventArgs(leaveArg0, leaveArg1));
                        break;
                    case 2:
                        var motionArg0 = args[0].u;
                        var motionArg1 = WlFixedToDouble(args[1].f);
                        var motionArg2 = WlFixedToDouble(args[2].f);
                        _motion?.Invoke(this, new MotionEventArgs(motionArg0, motionArg1, motionArg2));
                        break;
                    case 3:
                        var buttonArg0 = args[0].u;
                        var buttonArg1 = args[1].u;
                        var buttonArg2 = args[2].u;
                        var buttonArg3 = (WlPointerButtonState)args[3].u;
                        _button?.Invoke(this, new ButtonEventArgs(buttonArg0, buttonArg1, buttonArg2, buttonArg3));
                        break;
                    case 4:
                        var axisArg0 = args[0].u;
                        var axisArg1 = (WlPointerAxis)args[1].u;
                        var axisArg2 = WlFixedToDouble(args[2].f);
                        _axis?.Invoke(this, new AxisEventArgs(axisArg0, axisArg1, axisArg2));
                        break;
                    case 5:
                        _frame?.Invoke(this, new FrameEventArgs());
                        break;
                    case 6:
                        var axis_sourceArg0 = (WlPointerAxisSource)args[0].u;
                        _axis_source?.Invoke(this, new AxisSourceEventArgs(axis_sourceArg0));
                        break;
                    case 7:
                        var axis_stopArg0 = args[0].u;
                        var axis_stopArg1 = (WlPointerAxis)args[1].u;
                        _axis_stop?.Invoke(this, new AxisStopEventArgs(axis_stopArg0, axis_stopArg1));
                        break;
                    case 8:
                        var axis_discreteArg0 = (WlPointerAxis)args[0].u;
                        var axis_discreteArg1 = args[1].i;
                        _axis_discrete?.Invoke(this, new AxisDiscreteEventArgs(axis_discreteArg0, axis_discreteArg1));
                        break;
                    case 9:
                        var axis_value120Arg0 = (WlPointerAxis)args[0].u;
                        var axis_value120Arg1 = args[1].i;
                        _axis_value120?.Invoke(this, new AxisValue120EventArgs(axis_value120Arg0, axis_value120Arg1));
                        break;
                    default:
                        throw new WlClientException("Unknown event opcode");
                }

                return 0;
            }

            return dispatcher;
        }

        public void SetCursor(uint serial, WlSurface surface, int hotspot_x, int hotspot_y)
        {
            CheckIfDisposed();
            var arg0 = serial;
            var arg1 = surface._proxyObject;
            var arg2 = hotspot_x;
            var arg3 = hotspot_y;
            WlProxyMarshalFlags(_proxyObject, 0, null, WlProxyGetVersion(_proxyObject), 0, arg0, arg1, arg2, arg3);
        }

        public void Release()
        {
            CheckIfDisposed();
            WlProxyMarshalFlags(_proxyObject, 1, null, WlProxyGetVersion(_proxyObject), 0);
        }
    }

    public enum WlKeyboardKeymapFormat : uint
    {
        NoKeymap = 0,
        XkbV1 = 1
    }

    public enum WlKeyboardKeyState : uint
    {
        Released = 0,
        Pressed = 1
    }

    public unsafe partial class WlKeyboard : WlClientObject
    {
        internal WlKeyboard(_WlProxy* proxyObject) : base(proxyObject)
        {
        }

        public class KeymapEventArgs : EventArgs
        {
            public WlKeyboardKeymapFormat Format { get; }

            public int Fd { get; }

            public uint Size { get; }

            public KeymapEventArgs(WlKeyboardKeymapFormat format, int fd, uint size)
            {
                Format = format;
                Fd = fd;
                Size = size;
            }
        }

        event EventHandler<KeymapEventArgs>? _keymap;
        public event EventHandler<KeymapEventArgs>? Keymap
        {
            add
            {
                CheckIfDisposed();
                _keymap += value;
                HookDispatcher();
            }

            remove => _keymap -= value;
        }

        public class EnterEventArgs : EventArgs
        {
            public uint Serial { get; }

            public WlSurface Surface { get; }

            public WlArray Keys { get; }

            public EnterEventArgs(uint serial, WlSurface surface, WlArray keys)
            {
                Serial = serial;
                Surface = surface;
                Keys = keys;
            }
        }

        event EventHandler<EnterEventArgs>? _enter;
        public event EventHandler<EnterEventArgs>? Enter
        {
            add
            {
                CheckIfDisposed();
                _enter += value;
                HookDispatcher();
            }

            remove => _enter -= value;
        }

        public class LeaveEventArgs : EventArgs
        {
            public uint Serial { get; }

            public WlSurface Surface { get; }

            public LeaveEventArgs(uint serial, WlSurface surface)
            {
                Serial = serial;
                Surface = surface;
            }
        }

        event EventHandler<LeaveEventArgs>? _leave;
        public event EventHandler<LeaveEventArgs>? Leave
        {
            add
            {
                CheckIfDisposed();
                _leave += value;
                HookDispatcher();
            }

            remove => _leave -= value;
        }

        public class KeyEventArgs : EventArgs
        {
            public uint Serial { get; }

            public uint Time { get; }

            public uint Key { get; }

            public WlKeyboardKeyState State { get; }

            public KeyEventArgs(uint serial, uint time, uint key, WlKeyboardKeyState state)
            {
                Serial = serial;
                Time = time;
                Key = key;
                State = state;
            }
        }

        event EventHandler<KeyEventArgs>? _key;
        public event EventHandler<KeyEventArgs>? Key
        {
            add
            {
                CheckIfDisposed();
                _key += value;
                HookDispatcher();
            }

            remove => _key -= value;
        }

        public class ModifiersEventArgs : EventArgs
        {
            public uint Serial { get; }

            public uint ModsDepressed { get; }

            public uint ModsLatched { get; }

            public uint ModsLocked { get; }

            public uint Group { get; }

            public ModifiersEventArgs(uint serial, uint mods_depressed, uint mods_latched, uint mods_locked, uint group)
            {
                Serial = serial;
                ModsDepressed = mods_depressed;
                ModsLatched = mods_latched;
                ModsLocked = mods_locked;
                Group = group;
            }
        }

        event EventHandler<ModifiersEventArgs>? _modifiers;
        public event EventHandler<ModifiersEventArgs>? Modifiers
        {
            add
            {
                CheckIfDisposed();
                _modifiers += value;
                HookDispatcher();
            }

            remove => _modifiers -= value;
        }

        public class RepeatInfoEventArgs : EventArgs
        {
            public int Rate { get; }

            public int Delay { get; }

            public RepeatInfoEventArgs(int rate, int delay)
            {
                Rate = rate;
                Delay = delay;
            }
        }

        event EventHandler<RepeatInfoEventArgs>? _repeat_info;
        public event EventHandler<RepeatInfoEventArgs>? RepeatInfo
        {
            add
            {
                CheckIfDisposed();
                _repeat_info += value;
                HookDispatcher();
            }

            remove => _repeat_info -= value;
        }

        internal sealed override _WlDispatcherFuncT CreateDispatcher()
        {
            int dispatcher(void* data, void* target, uint callbackOpcode, _WlMessage* messageSignature, _WlArgument* args)
            {
                switch (callbackOpcode)
                {
                    case 0:
                        var keymapArg0 = (WlKeyboardKeymapFormat)args[0].u;
                        var keymapArg1 = args[1].h;
                        var keymapArg2 = args[2].u;
                        _keymap?.Invoke(this, new KeymapEventArgs(keymapArg0, keymapArg1, keymapArg2));
                        break;
                    case 1:
                        var enterArg0 = args[0].u;
                        var enterArg1 = WlClientObject.GetObject<WlSurface>((_WlProxy*)args[1].o);
                        var enterArg2 = new WlArray(args[2].a);
                        _enter?.Invoke(this, new EnterEventArgs(enterArg0, enterArg1, enterArg2));
                        break;
                    case 2:
                        var leaveArg0 = args[0].u;
                        var leaveArg1 = WlClientObject.GetObject<WlSurface>((_WlProxy*)args[1].o);
                        _leave?.Invoke(this, new LeaveEventArgs(leaveArg0, leaveArg1));
                        break;
                    case 3:
                        var keyArg0 = args[0].u;
                        var keyArg1 = args[1].u;
                        var keyArg2 = args[2].u;
                        var keyArg3 = (WlKeyboardKeyState)args[3].u;
                        _key?.Invoke(this, new KeyEventArgs(keyArg0, keyArg1, keyArg2, keyArg3));
                        break;
                    case 4:
                        var modifiersArg0 = args[0].u;
                        var modifiersArg1 = args[1].u;
                        var modifiersArg2 = args[2].u;
                        var modifiersArg3 = args[3].u;
                        var modifiersArg4 = args[4].u;
                        _modifiers?.Invoke(this, new ModifiersEventArgs(modifiersArg0, modifiersArg1, modifiersArg2, modifiersArg3, modifiersArg4));
                        break;
                    case 5:
                        var repeat_infoArg0 = args[0].i;
                        var repeat_infoArg1 = args[1].i;
                        _repeat_info?.Invoke(this, new RepeatInfoEventArgs(repeat_infoArg0, repeat_infoArg1));
                        break;
                    default:
                        throw new WlClientException("Unknown event opcode");
                }

                return 0;
            }

            return dispatcher;
        }

        public void Release()
        {
            CheckIfDisposed();
            WlProxyMarshalFlags(_proxyObject, 0, null, WlProxyGetVersion(_proxyObject), 0);
        }
    }

    public unsafe partial class WlTouch : WlClientObject
    {
        internal WlTouch(_WlProxy* proxyObject) : base(proxyObject)
        {
        }

        public class DownEventArgs : EventArgs
        {
            public uint Serial { get; }

            public uint Time { get; }

            public WlSurface Surface { get; }

            public int Id { get; }

            public double X { get; }

            public double Y { get; }

            public DownEventArgs(uint serial, uint time, WlSurface surface, int id, double x, double y)
            {
                Serial = serial;
                Time = time;
                Surface = surface;
                Id = id;
                X = x;
                Y = y;
            }
        }

        event EventHandler<DownEventArgs>? _down;
        public event EventHandler<DownEventArgs>? Down
        {
            add
            {
                CheckIfDisposed();
                _down += value;
                HookDispatcher();
            }

            remove => _down -= value;
        }

        public class UpEventArgs : EventArgs
        {
            public uint Serial { get; }

            public uint Time { get; }

            public int Id { get; }

            public UpEventArgs(uint serial, uint time, int id)
            {
                Serial = serial;
                Time = time;
                Id = id;
            }
        }

        event EventHandler<UpEventArgs>? _up;
        public event EventHandler<UpEventArgs>? Up
        {
            add
            {
                CheckIfDisposed();
                _up += value;
                HookDispatcher();
            }

            remove => _up -= value;
        }

        public class MotionEventArgs : EventArgs
        {
            public uint Time { get; }

            public int Id { get; }

            public double X { get; }

            public double Y { get; }

            public MotionEventArgs(uint time, int id, double x, double y)
            {
                Time = time;
                Id = id;
                X = x;
                Y = y;
            }
        }

        event EventHandler<MotionEventArgs>? _motion;
        public event EventHandler<MotionEventArgs>? Motion
        {
            add
            {
                CheckIfDisposed();
                _motion += value;
                HookDispatcher();
            }

            remove => _motion -= value;
        }

        public class FrameEventArgs : EventArgs
        {
            public FrameEventArgs()
            {
            }
        }

        event EventHandler<FrameEventArgs>? _frame;
        public event EventHandler<FrameEventArgs>? Frame
        {
            add
            {
                CheckIfDisposed();
                _frame += value;
                HookDispatcher();
            }

            remove => _frame -= value;
        }

        public class CancelEventArgs : EventArgs
        {
            public CancelEventArgs()
            {
            }
        }

        event EventHandler<CancelEventArgs>? _cancel;
        public event EventHandler<CancelEventArgs>? Cancel
        {
            add
            {
                CheckIfDisposed();
                _cancel += value;
                HookDispatcher();
            }

            remove => _cancel -= value;
        }

        public class ShapeEventArgs : EventArgs
        {
            public int Id { get; }

            public double Major { get; }

            public double Minor { get; }

            public ShapeEventArgs(int id, double major, double minor)
            {
                Id = id;
                Major = major;
                Minor = minor;
            }
        }

        event EventHandler<ShapeEventArgs>? _shape;
        public event EventHandler<ShapeEventArgs>? Shape
        {
            add
            {
                CheckIfDisposed();
                _shape += value;
                HookDispatcher();
            }

            remove => _shape -= value;
        }

        public class OrientationEventArgs : EventArgs
        {
            public int Id { get; }

            public double Orientation { get; }

            public OrientationEventArgs(int id, double orientation)
            {
                Id = id;
                Orientation = orientation;
            }
        }

        event EventHandler<OrientationEventArgs>? _orientation;
        public event EventHandler<OrientationEventArgs>? Orientation
        {
            add
            {
                CheckIfDisposed();
                _orientation += value;
                HookDispatcher();
            }

            remove => _orientation -= value;
        }

        internal sealed override _WlDispatcherFuncT CreateDispatcher()
        {
            int dispatcher(void* data, void* target, uint callbackOpcode, _WlMessage* messageSignature, _WlArgument* args)
            {
                switch (callbackOpcode)
                {
                    case 0:
                        var downArg0 = args[0].u;
                        var downArg1 = args[1].u;
                        var downArg2 = WlClientObject.GetObject<WlSurface>((_WlProxy*)args[2].o);
                        var downArg3 = args[3].i;
                        var downArg4 = WlFixedToDouble(args[4].f);
                        var downArg5 = WlFixedToDouble(args[5].f);
                        _down?.Invoke(this, new DownEventArgs(downArg0, downArg1, downArg2, downArg3, downArg4, downArg5));
                        break;
                    case 1:
                        var upArg0 = args[0].u;
                        var upArg1 = args[1].u;
                        var upArg2 = args[2].i;
                        _up?.Invoke(this, new UpEventArgs(upArg0, upArg1, upArg2));
                        break;
                    case 2:
                        var motionArg0 = args[0].u;
                        var motionArg1 = args[1].i;
                        var motionArg2 = WlFixedToDouble(args[2].f);
                        var motionArg3 = WlFixedToDouble(args[3].f);
                        _motion?.Invoke(this, new MotionEventArgs(motionArg0, motionArg1, motionArg2, motionArg3));
                        break;
                    case 3:
                        _frame?.Invoke(this, new FrameEventArgs());
                        break;
                    case 4:
                        _cancel?.Invoke(this, new CancelEventArgs());
                        break;
                    case 5:
                        var shapeArg0 = args[0].i;
                        var shapeArg1 = WlFixedToDouble(args[1].f);
                        var shapeArg2 = WlFixedToDouble(args[2].f);
                        _shape?.Invoke(this, new ShapeEventArgs(shapeArg0, shapeArg1, shapeArg2));
                        break;
                    case 6:
                        var orientationArg0 = args[0].i;
                        var orientationArg1 = WlFixedToDouble(args[1].f);
                        _orientation?.Invoke(this, new OrientationEventArgs(orientationArg0, orientationArg1));
                        break;
                    default:
                        throw new WlClientException("Unknown event opcode");
                }

                return 0;
            }

            return dispatcher;
        }

        public void Release()
        {
            CheckIfDisposed();
            WlProxyMarshalFlags(_proxyObject, 0, null, WlProxyGetVersion(_proxyObject), 0);
        }
    }

    public enum WlOutputSubpixel : uint
    {
        Unknown = 0,
        None = 1,
        HorizontalRgb = 2,
        HorizontalBgr = 3,
        VerticalRgb = 4,
        VerticalBgr = 5
    }

    public enum WlOutputTransform : uint
    {
        Normal = 0,
        _90 = 1,
        _180 = 2,
        _270 = 3,
        Flipped = 4,
        Flipped90 = 5,
        Flipped180 = 6,
        Flipped270 = 7
    }

    public enum WlOutputMode : uint
    {
        Current = 1,
        Preferred = 2
    }

    public unsafe partial class WlOutput : WlClientObject
    {
        internal WlOutput(_WlProxy* proxyObject) : base(proxyObject)
        {
        }

        public class GeometryEventArgs : EventArgs
        {
            public int X { get; }

            public int Y { get; }

            public int PhysicalWidth { get; }

            public int PhysicalHeight { get; }

            public WlOutputSubpixel Subpixel { get; }

            public string Make { get; }

            public string Model { get; }

            public WlOutputTransform Transform { get; }

            public GeometryEventArgs(int x, int y, int physical_width, int physical_height, WlOutputSubpixel subpixel, string make, string model, WlOutputTransform transform)
            {
                X = x;
                Y = y;
                PhysicalWidth = physical_width;
                PhysicalHeight = physical_height;
                Subpixel = subpixel;
                Make = make;
                Model = model;
                Transform = transform;
            }
        }

        event EventHandler<GeometryEventArgs>? _geometry;
        public event EventHandler<GeometryEventArgs>? Geometry
        {
            add
            {
                CheckIfDisposed();
                _geometry += value;
                HookDispatcher();
            }

            remove => _geometry -= value;
        }

        public class ModeEventArgs : EventArgs
        {
            public WlOutputMode Flags { get; }

            public int Width { get; }

            public int Height { get; }

            public int Refresh { get; }

            public ModeEventArgs(WlOutputMode flags, int width, int height, int refresh)
            {
                Flags = flags;
                Width = width;
                Height = height;
                Refresh = refresh;
            }
        }

        event EventHandler<ModeEventArgs>? _mode;
        public event EventHandler<ModeEventArgs>? Mode
        {
            add
            {
                CheckIfDisposed();
                _mode += value;
                HookDispatcher();
            }

            remove => _mode -= value;
        }

        public class DoneEventArgs : EventArgs
        {
            public DoneEventArgs()
            {
            }
        }

        event EventHandler<DoneEventArgs>? _done;
        public event EventHandler<DoneEventArgs>? Done
        {
            add
            {
                CheckIfDisposed();
                _done += value;
                HookDispatcher();
            }

            remove => _done -= value;
        }

        public class ScaleEventArgs : EventArgs
        {
            public int Factor { get; }

            public ScaleEventArgs(int factor)
            {
                Factor = factor;
            }
        }

        event EventHandler<ScaleEventArgs>? _scale;
        public event EventHandler<ScaleEventArgs>? Scale
        {
            add
            {
                CheckIfDisposed();
                _scale += value;
                HookDispatcher();
            }

            remove => _scale -= value;
        }

        public class NameEventArgs : EventArgs
        {
            public string Name { get; }

            public NameEventArgs(string name)
            {
                Name = name;
            }
        }

        event EventHandler<NameEventArgs>? _name;
        public event EventHandler<NameEventArgs>? Name
        {
            add
            {
                CheckIfDisposed();
                _name += value;
                HookDispatcher();
            }

            remove => _name -= value;
        }

        public class DescriptionEventArgs : EventArgs
        {
            public string Description { get; }

            public DescriptionEventArgs(string description)
            {
                Description = description;
            }
        }

        event EventHandler<DescriptionEventArgs>? _description;
        public event EventHandler<DescriptionEventArgs>? Description
        {
            add
            {
                CheckIfDisposed();
                _description += value;
                HookDispatcher();
            }

            remove => _description -= value;
        }

        internal sealed override _WlDispatcherFuncT CreateDispatcher()
        {
            int dispatcher(void* data, void* target, uint callbackOpcode, _WlMessage* messageSignature, _WlArgument* args)
            {
                switch (callbackOpcode)
                {
                    case 0:
                        var geometryArg0 = args[0].i;
                        var geometryArg1 = args[1].i;
                        var geometryArg2 = args[2].i;
                        var geometryArg3 = args[3].i;
                        var geometryArg4 = (WlOutputSubpixel)args[4].i;
                        var geometryArg5 = Marshal.PtrToStringAnsi((IntPtr)args[5].s)!;
                        var geometryArg6 = Marshal.PtrToStringAnsi((IntPtr)args[6].s)!;
                        var geometryArg7 = (WlOutputTransform)args[7].i;
                        _geometry?.Invoke(this, new GeometryEventArgs(geometryArg0, geometryArg1, geometryArg2, geometryArg3, geometryArg4, geometryArg5, geometryArg6, geometryArg7));
                        break;
                    case 1:
                        var modeArg0 = (WlOutputMode)args[0].u;
                        var modeArg1 = args[1].i;
                        var modeArg2 = args[2].i;
                        var modeArg3 = args[3].i;
                        _mode?.Invoke(this, new ModeEventArgs(modeArg0, modeArg1, modeArg2, modeArg3));
                        break;
                    case 2:
                        _done?.Invoke(this, new DoneEventArgs());
                        break;
                    case 3:
                        var scaleArg0 = args[0].i;
                        _scale?.Invoke(this, new ScaleEventArgs(scaleArg0));
                        break;
                    case 4:
                        var nameArg0 = Marshal.PtrToStringAnsi((IntPtr)args[0].s)!;
                        _name?.Invoke(this, new NameEventArgs(nameArg0));
                        break;
                    case 5:
                        var descriptionArg0 = Marshal.PtrToStringAnsi((IntPtr)args[0].s)!;
                        _description?.Invoke(this, new DescriptionEventArgs(descriptionArg0));
                        break;
                    default:
                        throw new WlClientException("Unknown event opcode");
                }

                return 0;
            }

            return dispatcher;
        }

        public void Release()
        {
            CheckIfDisposed();
            WlProxyMarshalFlags(_proxyObject, 0, null, WlProxyGetVersion(_proxyObject), 0);
        }
    }

    public unsafe partial class WlRegion : WlClientObject
    {
        internal WlRegion(_WlProxy* proxyObject) : base(proxyObject)
        {
        }

        public void Destroy()
        {
            CheckIfDisposed();
            WlProxyMarshalFlags(_proxyObject, 0, null, WlProxyGetVersion(_proxyObject), 0);
        }

        public void Add(int x, int y, int width, int height)
        {
            CheckIfDisposed();
            var arg0 = x;
            var arg1 = y;
            var arg2 = width;
            var arg3 = height;
            WlProxyMarshalFlags(_proxyObject, 1, null, WlProxyGetVersion(_proxyObject), 0, arg0, arg1, arg2, arg3);
        }

        public void Subtract(int x, int y, int width, int height)
        {
            CheckIfDisposed();
            var arg0 = x;
            var arg1 = y;
            var arg2 = width;
            var arg3 = height;
            WlProxyMarshalFlags(_proxyObject, 2, null, WlProxyGetVersion(_proxyObject), 0, arg0, arg1, arg2, arg3);
        }
    }

    public enum WlSubcompositorError : uint
    {
        BadSurface = 0,
        BadParent = 1
    }

    public unsafe partial class WlSubcompositor : WlClientObject
    {
        internal WlSubcompositor(_WlProxy* proxyObject) : base(proxyObject)
        {
        }

        public void Destroy()
        {
            CheckIfDisposed();
            WlProxyMarshalFlags(_proxyObject, 0, null, WlProxyGetVersion(_proxyObject), 0);
        }

        public WlSubsurface GetSubsurface(WlSurface surface, WlSurface parent)
        {
            CheckIfDisposed();
            var interfacePtr = WlInterface.WlSubsurface.ToBlittable();
            var arg0 = (_WlProxy*)null;
            var arg1 = surface._proxyObject;
            var arg2 = parent._proxyObject;
            var newId = WlProxyMarshalFlags(_proxyObject, 1, interfacePtr, WlProxyGetVersion(_proxyObject), 0, arg0, arg1, arg2);
            return new WlSubsurface(newId);
        }
    }

    public enum WlSubsurfaceError : uint
    {
        BadSurface = 0
    }

    public unsafe partial class WlSubsurface : WlClientObject
    {
        internal WlSubsurface(_WlProxy* proxyObject) : base(proxyObject)
        {
        }

        public void Destroy()
        {
            CheckIfDisposed();
            WlProxyMarshalFlags(_proxyObject, 0, null, WlProxyGetVersion(_proxyObject), 0);
        }

        public void SetPosition(int x, int y)
        {
            CheckIfDisposed();
            var arg0 = x;
            var arg1 = y;
            WlProxyMarshalFlags(_proxyObject, 1, null, WlProxyGetVersion(_proxyObject), 0, arg0, arg1);
        }

        public void PlaceAbove(WlSurface sibling)
        {
            CheckIfDisposed();
            var arg0 = sibling._proxyObject;
            WlProxyMarshalFlags(_proxyObject, 2, null, WlProxyGetVersion(_proxyObject), 0, arg0);
        }

        public void PlaceBelow(WlSurface sibling)
        {
            CheckIfDisposed();
            var arg0 = sibling._proxyObject;
            WlProxyMarshalFlags(_proxyObject, 3, null, WlProxyGetVersion(_proxyObject), 0, arg0);
        }

        public void SetSync()
        {
            CheckIfDisposed();
            WlProxyMarshalFlags(_proxyObject, 4, null, WlProxyGetVersion(_proxyObject), 0);
        }

        public void SetDesync()
        {
            CheckIfDisposed();
            WlProxyMarshalFlags(_proxyObject, 5, null, WlProxyGetVersion(_proxyObject), 0);
        }
    }

    public unsafe partial class ZwlrExportDmabufManagerV1 : WlClientObject
    {
        internal ZwlrExportDmabufManagerV1(_WlProxy* proxyObject) : base(proxyObject)
        {
        }

        public ZwlrExportDmabufFrameV1 CaptureOutput(int overlay_cursor, WlOutput output)
        {
            CheckIfDisposed();
            var interfacePtr = WlInterface.ZwlrExportDmabufFrameV1.ToBlittable();
            var arg0 = (_WlProxy*)null;
            var arg1 = overlay_cursor;
            var arg2 = output._proxyObject;
            var newId = WlProxyMarshalFlags(_proxyObject, 0, interfacePtr, WlProxyGetVersion(_proxyObject), 0, arg0, arg1, arg2);
            return new ZwlrExportDmabufFrameV1(newId);
        }

        public void Destroy()
        {
            CheckIfDisposed();
            WlProxyMarshalFlags(_proxyObject, 1, null, WlProxyGetVersion(_proxyObject), 0);
        }
    }

    public enum ZwlrExportDmabufFrameV1Flags : uint
    {
        Transient = 1
    }

    public enum ZwlrExportDmabufFrameV1CancelReason : uint
    {
        Temporary = 0,
        Permanent = 1,
        Resizing = 2
    }

    public unsafe partial class ZwlrExportDmabufFrameV1 : WlClientObject
    {
        internal ZwlrExportDmabufFrameV1(_WlProxy* proxyObject) : base(proxyObject)
        {
        }

        public class FrameEventArgs : EventArgs
        {
            public uint Width { get; }

            public uint Height { get; }

            public uint OffsetX { get; }

            public uint OffsetY { get; }

            public uint BufferFlags { get; }

            public ZwlrExportDmabufFrameV1Flags Flags { get; }

            public uint Format { get; }

            public uint ModHigh { get; }

            public uint ModLow { get; }

            public uint NumObjects { get; }

            public FrameEventArgs(uint width, uint height, uint offset_x, uint offset_y, uint buffer_flags, ZwlrExportDmabufFrameV1Flags flags, uint format, uint mod_high, uint mod_low, uint num_objects)
            {
                Width = width;
                Height = height;
                OffsetX = offset_x;
                OffsetY = offset_y;
                BufferFlags = buffer_flags;
                Flags = flags;
                Format = format;
                ModHigh = mod_high;
                ModLow = mod_low;
                NumObjects = num_objects;
            }
        }

        event EventHandler<FrameEventArgs>? _frame;
        public event EventHandler<FrameEventArgs>? Frame
        {
            add
            {
                CheckIfDisposed();
                _frame += value;
                HookDispatcher();
            }

            remove => _frame -= value;
        }

        public class ObjectEventArgs : EventArgs
        {
            public uint Index { get; }

            public int Fd { get; }

            public uint Size { get; }

            public uint Offset { get; }

            public uint Stride { get; }

            public uint PlaneIndex { get; }

            public ObjectEventArgs(uint index, int fd, uint size, uint offset, uint stride, uint plane_index)
            {
                Index = index;
                Fd = fd;
                Size = size;
                Offset = offset;
                Stride = stride;
                PlaneIndex = plane_index;
            }
        }

        event EventHandler<ObjectEventArgs>? _object;
        public event EventHandler<ObjectEventArgs>? Object
        {
            add
            {
                CheckIfDisposed();
                _object += value;
                HookDispatcher();
            }

            remove => _object -= value;
        }

        public class ReadyEventArgs : EventArgs
        {
            public uint TvSecHi { get; }

            public uint TvSecLo { get; }

            public uint TvNsec { get; }

            public ReadyEventArgs(uint tv_sec_hi, uint tv_sec_lo, uint tv_nsec)
            {
                TvSecHi = tv_sec_hi;
                TvSecLo = tv_sec_lo;
                TvNsec = tv_nsec;
            }
        }

        event EventHandler<ReadyEventArgs>? _ready;
        public event EventHandler<ReadyEventArgs>? Ready
        {
            add
            {
                CheckIfDisposed();
                _ready += value;
                HookDispatcher();
            }

            remove => _ready -= value;
        }

        public class CancelEventArgs : EventArgs
        {
            public ZwlrExportDmabufFrameV1CancelReason Reason { get; }

            public CancelEventArgs(ZwlrExportDmabufFrameV1CancelReason reason)
            {
                Reason = reason;
            }
        }

        event EventHandler<CancelEventArgs>? _cancel;
        public event EventHandler<CancelEventArgs>? Cancel
        {
            add
            {
                CheckIfDisposed();
                _cancel += value;
                HookDispatcher();
            }

            remove => _cancel -= value;
        }

        internal sealed override _WlDispatcherFuncT CreateDispatcher()
        {
            int dispatcher(void* data, void* target, uint callbackOpcode, _WlMessage* messageSignature, _WlArgument* args)
            {
                switch (callbackOpcode)
                {
                    case 0:
                        var frameArg0 = args[0].u;
                        var frameArg1 = args[1].u;
                        var frameArg2 = args[2].u;
                        var frameArg3 = args[3].u;
                        var frameArg4 = args[4].u;
                        var frameArg5 = (ZwlrExportDmabufFrameV1Flags)args[5].u;
                        var frameArg6 = args[6].u;
                        var frameArg7 = args[7].u;
                        var frameArg8 = args[8].u;
                        var frameArg9 = args[9].u;
                        _frame?.Invoke(this, new FrameEventArgs(frameArg0, frameArg1, frameArg2, frameArg3, frameArg4, frameArg5, frameArg6, frameArg7, frameArg8, frameArg9));
                        break;
                    case 1:
                        var objectArg0 = args[0].u;
                        var objectArg1 = args[1].h;
                        var objectArg2 = args[2].u;
                        var objectArg3 = args[3].u;
                        var objectArg4 = args[4].u;
                        var objectArg5 = args[5].u;
                        _object?.Invoke(this, new ObjectEventArgs(objectArg0, objectArg1, objectArg2, objectArg3, objectArg4, objectArg5));
                        break;
                    case 2:
                        var readyArg0 = args[0].u;
                        var readyArg1 = args[1].u;
                        var readyArg2 = args[2].u;
                        _ready?.Invoke(this, new ReadyEventArgs(readyArg0, readyArg1, readyArg2));
                        break;
                    case 3:
                        var cancelArg0 = (ZwlrExportDmabufFrameV1CancelReason)args[0].u;
                        _cancel?.Invoke(this, new CancelEventArgs(cancelArg0));
                        break;
                    default:
                        throw new WlClientException("Unknown event opcode");
                }

                return 0;
            }

            return dispatcher;
        }

        public void Destroy()
        {
            CheckIfDisposed();
            WlProxyMarshalFlags(_proxyObject, 0, null, WlProxyGetVersion(_proxyObject), 0);
        }
    }

    public unsafe partial class ZwlrScreencopyManagerV1 : WlClientObject
    {
        internal ZwlrScreencopyManagerV1(_WlProxy* proxyObject) : base(proxyObject)
        {
        }

        public ZwlrScreencopyFrameV1 CaptureOutput(int overlay_cursor, WlOutput output)
        {
            CheckIfDisposed();
            var interfacePtr = WlInterface.ZwlrScreencopyFrameV1.ToBlittable();
            var arg0 = (_WlProxy*)null;
            var arg1 = overlay_cursor;
            var arg2 = output._proxyObject;
            var newId = WlProxyMarshalFlags(_proxyObject, 0, interfacePtr, WlProxyGetVersion(_proxyObject), 0, arg0, arg1, arg2);
            return new ZwlrScreencopyFrameV1(newId);
        }

        public ZwlrScreencopyFrameV1 CaptureOutputRegion(int overlay_cursor, WlOutput output, int x, int y, int width, int height)
        {
            CheckIfDisposed();
            var interfacePtr = WlInterface.ZwlrScreencopyFrameV1.ToBlittable();
            var arg0 = (_WlProxy*)null;
            var arg1 = overlay_cursor;
            var arg2 = output._proxyObject;
            var arg3 = x;
            var arg4 = y;
            var arg5 = width;
            var arg6 = height;
            var newId = WlProxyMarshalFlags(_proxyObject, 1, interfacePtr, WlProxyGetVersion(_proxyObject), 0, arg0, arg1, arg2, arg3, arg4, arg5, arg6);
            return new ZwlrScreencopyFrameV1(newId);
        }

        public void Destroy()
        {
            CheckIfDisposed();
            WlProxyMarshalFlags(_proxyObject, 2, null, WlProxyGetVersion(_proxyObject), 0);
        }
    }

    public enum ZwlrScreencopyFrameV1Error : uint
    {
        AlreadyUsed = 0,
        InvalidBuffer = 1
    }

    public enum ZwlrScreencopyFrameV1Flags : uint
    {
        YInvert = 1
    }

    public unsafe partial class ZwlrScreencopyFrameV1 : WlClientObject
    {
        internal ZwlrScreencopyFrameV1(_WlProxy* proxyObject) : base(proxyObject)
        {
        }

        public class BufferEventArgs : EventArgs
        {
            public WlShmFormat Format { get; }

            public uint Width { get; }

            public uint Height { get; }

            public uint Stride { get; }

            public BufferEventArgs(WlShmFormat format, uint width, uint height, uint stride)
            {
                Format = format;
                Width = width;
                Height = height;
                Stride = stride;
            }
        }

        event EventHandler<BufferEventArgs>? _buffer;
        public event EventHandler<BufferEventArgs>? Buffer
        {
            add
            {
                CheckIfDisposed();
                _buffer += value;
                HookDispatcher();
            }

            remove => _buffer -= value;
        }

        public class FlagsEventArgs : EventArgs
        {
            public ZwlrScreencopyFrameV1Flags Flags { get; }

            public FlagsEventArgs(ZwlrScreencopyFrameV1Flags flags)
            {
                Flags = flags;
            }
        }

        event EventHandler<FlagsEventArgs>? _flags;
        public event EventHandler<FlagsEventArgs>? Flags
        {
            add
            {
                CheckIfDisposed();
                _flags += value;
                HookDispatcher();
            }

            remove => _flags -= value;
        }

        public class ReadyEventArgs : EventArgs
        {
            public uint TvSecHi { get; }

            public uint TvSecLo { get; }

            public uint TvNsec { get; }

            public ReadyEventArgs(uint tv_sec_hi, uint tv_sec_lo, uint tv_nsec)
            {
                TvSecHi = tv_sec_hi;
                TvSecLo = tv_sec_lo;
                TvNsec = tv_nsec;
            }
        }

        event EventHandler<ReadyEventArgs>? _ready;
        public event EventHandler<ReadyEventArgs>? Ready
        {
            add
            {
                CheckIfDisposed();
                _ready += value;
                HookDispatcher();
            }

            remove => _ready -= value;
        }

        public class FailedEventArgs : EventArgs
        {
            public FailedEventArgs()
            {
            }
        }

        event EventHandler<FailedEventArgs>? _failed;
        public event EventHandler<FailedEventArgs>? Failed
        {
            add
            {
                CheckIfDisposed();
                _failed += value;
                HookDispatcher();
            }

            remove => _failed -= value;
        }

        public class DamageEventArgs : EventArgs
        {
            public uint X { get; }

            public uint Y { get; }

            public uint Width { get; }

            public uint Height { get; }

            public DamageEventArgs(uint x, uint y, uint width, uint height)
            {
                X = x;
                Y = y;
                Width = width;
                Height = height;
            }
        }

        event EventHandler<DamageEventArgs>? _damage;
        public event EventHandler<DamageEventArgs>? Damage
        {
            add
            {
                CheckIfDisposed();
                _damage += value;
                HookDispatcher();
            }

            remove => _damage -= value;
        }

        public class LinuxDmabufEventArgs : EventArgs
        {
            public uint Format { get; }

            public uint Width { get; }

            public uint Height { get; }

            public LinuxDmabufEventArgs(uint format, uint width, uint height)
            {
                Format = format;
                Width = width;
                Height = height;
            }
        }

        event EventHandler<LinuxDmabufEventArgs>? _linux_dmabuf;
        public event EventHandler<LinuxDmabufEventArgs>? LinuxDmabuf
        {
            add
            {
                CheckIfDisposed();
                _linux_dmabuf += value;
                HookDispatcher();
            }

            remove => _linux_dmabuf -= value;
        }

        public class BufferDoneEventArgs : EventArgs
        {
            public BufferDoneEventArgs()
            {
            }
        }

        event EventHandler<BufferDoneEventArgs>? _buffer_done;
        public event EventHandler<BufferDoneEventArgs>? BufferDone
        {
            add
            {
                CheckIfDisposed();
                _buffer_done += value;
                HookDispatcher();
            }

            remove => _buffer_done -= value;
        }

        internal sealed override _WlDispatcherFuncT CreateDispatcher()
        {
            int dispatcher(void* data, void* target, uint callbackOpcode, _WlMessage* messageSignature, _WlArgument* args)
            {
                switch (callbackOpcode)
                {
                    case 0:
                        var bufferArg0 = (WlShmFormat)args[0].u;
                        var bufferArg1 = args[1].u;
                        var bufferArg2 = args[2].u;
                        var bufferArg3 = args[3].u;
                        _buffer?.Invoke(this, new BufferEventArgs(bufferArg0, bufferArg1, bufferArg2, bufferArg3));
                        break;
                    case 1:
                        var flagsArg0 = (ZwlrScreencopyFrameV1Flags)args[0].u;
                        _flags?.Invoke(this, new FlagsEventArgs(flagsArg0));
                        break;
                    case 2:
                        var readyArg0 = args[0].u;
                        var readyArg1 = args[1].u;
                        var readyArg2 = args[2].u;
                        _ready?.Invoke(this, new ReadyEventArgs(readyArg0, readyArg1, readyArg2));
                        break;
                    case 3:
                        _failed?.Invoke(this, new FailedEventArgs());
                        break;
                    case 4:
                        var damageArg0 = args[0].u;
                        var damageArg1 = args[1].u;
                        var damageArg2 = args[2].u;
                        var damageArg3 = args[3].u;
                        _damage?.Invoke(this, new DamageEventArgs(damageArg0, damageArg1, damageArg2, damageArg3));
                        break;
                    case 5:
                        var linux_dmabufArg0 = args[0].u;
                        var linux_dmabufArg1 = args[1].u;
                        var linux_dmabufArg2 = args[2].u;
                        _linux_dmabuf?.Invoke(this, new LinuxDmabufEventArgs(linux_dmabufArg0, linux_dmabufArg1, linux_dmabufArg2));
                        break;
                    case 6:
                        _buffer_done?.Invoke(this, new BufferDoneEventArgs());
                        break;
                    default:
                        throw new WlClientException("Unknown event opcode");
                }

                return 0;
            }

            return dispatcher;
        }

        public void Copy(WlBuffer buffer)
        {
            CheckIfDisposed();
            var arg0 = buffer._proxyObject;
            WlProxyMarshalFlags(_proxyObject, 0, null, WlProxyGetVersion(_proxyObject), 0, arg0);
        }

        public void Destroy()
        {
            CheckIfDisposed();
            WlProxyMarshalFlags(_proxyObject, 1, null, WlProxyGetVersion(_proxyObject), 0);
        }

        public void CopyWithDamage(WlBuffer buffer)
        {
            CheckIfDisposed();
            var arg0 = buffer._proxyObject;
            WlProxyMarshalFlags(_proxyObject, 2, null, WlProxyGetVersion(_proxyObject), 0, arg0);
        }
    }

    public enum ZkdeScreencastUnstableV1Pointer : uint
    {
        Hidden = 1,
        Embedded = 2,
        Metadata = 4
    }

    public unsafe partial class ZkdeScreencastUnstableV1 : WlClientObject
    {
        internal ZkdeScreencastUnstableV1(_WlProxy* proxyObject) : base(proxyObject)
        {
        }

        public ZkdeScreencastStreamUnstableV1 StreamOutput(WlOutput output, uint pointer)
        {
            CheckIfDisposed();
            var interfacePtr = WlInterface.ZkdeScreencastStreamUnstableV1.ToBlittable();
            var arg0 = (_WlProxy*)null;
            var arg1 = output._proxyObject;
            var arg2 = pointer;
            var newId = WlProxyMarshalFlags(_proxyObject, 0, interfacePtr, WlProxyGetVersion(_proxyObject), 0, arg0, arg1, arg2);
            return new ZkdeScreencastStreamUnstableV1(newId);
        }

        public ZkdeScreencastStreamUnstableV1 StreamWindow(string window_uuid, uint pointer)
        {
            CheckIfDisposed();
            var interfacePtr = WlInterface.ZkdeScreencastStreamUnstableV1.ToBlittable();
            var arg0 = (_WlProxy*)null;
            var arg1 = (char*)Marshal.StringToHGlobalAnsi(window_uuid)!;
            var arg2 = pointer;
            var newId = WlProxyMarshalFlags(_proxyObject, 1, interfacePtr, WlProxyGetVersion(_proxyObject), 0, arg0, arg1, arg2);
            return new ZkdeScreencastStreamUnstableV1(newId);
        }

        public void Destroy()
        {
            CheckIfDisposed();
            WlProxyMarshalFlags(_proxyObject, 2, null, WlProxyGetVersion(_proxyObject), 0);
        }
    }

    public unsafe partial class ZkdeScreencastStreamUnstableV1 : WlClientObject
    {
        internal ZkdeScreencastStreamUnstableV1(_WlProxy* proxyObject) : base(proxyObject)
        {
        }

        public class ClosedEventArgs : EventArgs
        {
            public ClosedEventArgs()
            {
            }
        }

        event EventHandler<ClosedEventArgs>? _closed;
        public event EventHandler<ClosedEventArgs>? Closed
        {
            add
            {
                CheckIfDisposed();
                _closed += value;
                HookDispatcher();
            }

            remove => _closed -= value;
        }

        public class CreatedEventArgs : EventArgs
        {
            public uint Node { get; }

            public CreatedEventArgs(uint node)
            {
                Node = node;
            }
        }

        event EventHandler<CreatedEventArgs>? _created;
        public event EventHandler<CreatedEventArgs>? Created
        {
            add
            {
                CheckIfDisposed();
                _created += value;
                HookDispatcher();
            }

            remove => _created -= value;
        }

        public class FailedEventArgs : EventArgs
        {
            public string Error { get; }

            public FailedEventArgs(string error)
            {
                Error = error;
            }
        }

        event EventHandler<FailedEventArgs>? _failed;
        public event EventHandler<FailedEventArgs>? Failed
        {
            add
            {
                CheckIfDisposed();
                _failed += value;
                HookDispatcher();
            }

            remove => _failed -= value;
        }

        internal sealed override _WlDispatcherFuncT CreateDispatcher()
        {
            int dispatcher(void* data, void* target, uint callbackOpcode, _WlMessage* messageSignature, _WlArgument* args)
            {
                switch (callbackOpcode)
                {
                    case 0:
                        _closed?.Invoke(this, new ClosedEventArgs());
                        break;
                    case 1:
                        var createdArg0 = args[0].u;
                        _created?.Invoke(this, new CreatedEventArgs(createdArg0));
                        break;
                    case 2:
                        var failedArg0 = Marshal.PtrToStringAnsi((IntPtr)args[0].s)!;
                        _failed?.Invoke(this, new FailedEventArgs(failedArg0));
                        break;
                    default:
                        throw new WlClientException("Unknown event opcode");
                }

                return 0;
            }

            return dispatcher;
        }

        public void Close()
        {
            CheckIfDisposed();
            WlProxyMarshalFlags(_proxyObject, 0, null, WlProxyGetVersion(_proxyObject), 0);
        }
    }

    public unsafe partial class ZxdgOutputManagerV1 : WlClientObject
    {
        internal ZxdgOutputManagerV1(_WlProxy* proxyObject) : base(proxyObject)
        {
        }

        public void Destroy()
        {
            CheckIfDisposed();
            WlProxyMarshalFlags(_proxyObject, 0, null, WlProxyGetVersion(_proxyObject), 0);
        }

        public ZxdgOutputV1 GetXdgOutput(WlOutput output)
        {
            CheckIfDisposed();
            var interfacePtr = WlInterface.ZxdgOutputV1.ToBlittable();
            var arg0 = (_WlProxy*)null;
            var arg1 = output._proxyObject;
            var newId = WlProxyMarshalFlags(_proxyObject, 1, interfacePtr, WlProxyGetVersion(_proxyObject), 0, arg0, arg1);
            return new ZxdgOutputV1(newId);
        }
    }

    public unsafe partial class ZxdgOutputV1 : WlClientObject
    {
        internal ZxdgOutputV1(_WlProxy* proxyObject) : base(proxyObject)
        {
        }

        public class LogicalPositionEventArgs : EventArgs
        {
            public int X { get; }

            public int Y { get; }

            public LogicalPositionEventArgs(int x, int y)
            {
                X = x;
                Y = y;
            }
        }

        event EventHandler<LogicalPositionEventArgs>? _logical_position;
        public event EventHandler<LogicalPositionEventArgs>? LogicalPosition
        {
            add
            {
                CheckIfDisposed();
                _logical_position += value;
                HookDispatcher();
            }

            remove => _logical_position -= value;
        }

        public class LogicalSizeEventArgs : EventArgs
        {
            public int Width { get; }

            public int Height { get; }

            public LogicalSizeEventArgs(int width, int height)
            {
                Width = width;
                Height = height;
            }
        }

        event EventHandler<LogicalSizeEventArgs>? _logical_size;
        public event EventHandler<LogicalSizeEventArgs>? LogicalSize
        {
            add
            {
                CheckIfDisposed();
                _logical_size += value;
                HookDispatcher();
            }

            remove => _logical_size -= value;
        }

        public class DoneEventArgs : EventArgs
        {
            public DoneEventArgs()
            {
            }
        }

        event EventHandler<DoneEventArgs>? _done;
        public event EventHandler<DoneEventArgs>? Done
        {
            add
            {
                CheckIfDisposed();
                _done += value;
                HookDispatcher();
            }

            remove => _done -= value;
        }

        public class NameEventArgs : EventArgs
        {
            public string Name { get; }

            public NameEventArgs(string name)
            {
                Name = name;
            }
        }

        event EventHandler<NameEventArgs>? _name;
        public event EventHandler<NameEventArgs>? Name
        {
            add
            {
                CheckIfDisposed();
                _name += value;
                HookDispatcher();
            }

            remove => _name -= value;
        }

        public class DescriptionEventArgs : EventArgs
        {
            public string Description { get; }

            public DescriptionEventArgs(string description)
            {
                Description = description;
            }
        }

        event EventHandler<DescriptionEventArgs>? _description;
        public event EventHandler<DescriptionEventArgs>? Description
        {
            add
            {
                CheckIfDisposed();
                _description += value;
                HookDispatcher();
            }

            remove => _description -= value;
        }

        internal sealed override _WlDispatcherFuncT CreateDispatcher()
        {
            int dispatcher(void* data, void* target, uint callbackOpcode, _WlMessage* messageSignature, _WlArgument* args)
            {
                switch (callbackOpcode)
                {
                    case 0:
                        var logical_positionArg0 = args[0].i;
                        var logical_positionArg1 = args[1].i;
                        _logical_position?.Invoke(this, new LogicalPositionEventArgs(logical_positionArg0, logical_positionArg1));
                        break;
                    case 1:
                        var logical_sizeArg0 = args[0].i;
                        var logical_sizeArg1 = args[1].i;
                        _logical_size?.Invoke(this, new LogicalSizeEventArgs(logical_sizeArg0, logical_sizeArg1));
                        break;
                    case 2:
                        _done?.Invoke(this, new DoneEventArgs());
                        break;
                    case 3:
                        var nameArg0 = Marshal.PtrToStringAnsi((IntPtr)args[0].s)!;
                        _name?.Invoke(this, new NameEventArgs(nameArg0));
                        break;
                    case 4:
                        var descriptionArg0 = Marshal.PtrToStringAnsi((IntPtr)args[0].s)!;
                        _description?.Invoke(this, new DescriptionEventArgs(descriptionArg0));
                        break;
                    default:
                        throw new WlClientException("Unknown event opcode");
                }

                return 0;
            }

            return dispatcher;
        }

        public void Destroy()
        {
            CheckIfDisposed();
            WlProxyMarshalFlags(_proxyObject, 0, null, WlProxyGetVersion(_proxyObject), 0);
        }
    }

    public unsafe partial class WlRegistry : WlClientObject
    {
        public T Bind<T>(uint name, string interfaceName, uint version = 0)
            where T : WlClientObject
        {
            return (T)Bind(name, interfaceName, version);
        }

        public WlClientObject Bind(uint name, string interfaceName, uint version = 0)
        {
            var interfacePtr = WlInterface.FromInterfaceName(interfaceName).ToBlittable();
            version = version == 0 ? (uint)interfacePtr->Version : version;
            var proxy = WlProxyMarshalFlags(_proxyObject, 0, interfacePtr, version, 0, name, interfacePtr->Name, version);
            return interfaceName switch
            {
                "wl_display" => new WlDisplay(proxy),
                "wl_registry" => new WlRegistry(proxy),
                "wl_callback" => new WlCallback(proxy),
                "wl_compositor" => new WlCompositor(proxy),
                "wl_shm_pool" => new WlShmPool(proxy),
                "wl_shm" => new WlShm(proxy),
                "wl_buffer" => new WlBuffer(proxy),
                "wl_data_offer" => new WlDataOffer(proxy),
                "wl_data_source" => new WlDataSource(proxy),
                "wl_data_device" => new WlDataDevice(proxy),
                "wl_data_device_manager" => new WlDataDeviceManager(proxy),
                "wl_shell" => new WlShell(proxy),
                "wl_shell_surface" => new WlShellSurface(proxy),
                "wl_surface" => new WlSurface(proxy),
                "wl_seat" => new WlSeat(proxy),
                "wl_pointer" => new WlPointer(proxy),
                "wl_keyboard" => new WlKeyboard(proxy),
                "wl_touch" => new WlTouch(proxy),
                "wl_output" => new WlOutput(proxy),
                "wl_region" => new WlRegion(proxy),
                "wl_subcompositor" => new WlSubcompositor(proxy),
                "wl_subsurface" => new WlSubsurface(proxy),
                "zwlr_export_dmabuf_manager_v1" => new ZwlrExportDmabufManagerV1(proxy),
                "zwlr_export_dmabuf_frame_v1" => new ZwlrExportDmabufFrameV1(proxy),
                "zwlr_screencopy_manager_v1" => new ZwlrScreencopyManagerV1(proxy),
                "zwlr_screencopy_frame_v1" => new ZwlrScreencopyFrameV1(proxy),
                "zkde_screencast_unstable_v1" => new ZkdeScreencastUnstableV1(proxy),
                "zkde_screencast_stream_unstable_v1" => new ZkdeScreencastStreamUnstableV1(proxy),
                "zxdg_output_manager_v1" => new ZxdgOutputManagerV1(proxy),
                "zxdg_output_v1" => new ZxdgOutputV1(proxy),
                _ => throw new WlClientException("Unknown interface")
            };
        }
    }

#pragma warning disable CA5392
    internal static unsafe class Client
    {
        private const string LibWaylandClient = "libwayland-client.so.0";
        [DllImport(LibWaylandClient, EntryPoint = "wl_array_init", ExactSpelling = true)]
        public static extern void WlArrayInit(_WlArray* array);
        [DllImport(LibWaylandClient, EntryPoint = "wl_array_release", ExactSpelling = true)]
        public static extern void WlArrayRelease(_WlArray* array);
        [DllImport(LibWaylandClient, EntryPoint = "wl_array_add", ExactSpelling = true)]
        public static extern void* WlArrayAdd(_WlArray* array, int size);
        [DllImport(LibWaylandClient, EntryPoint = "wl_array_copy", ExactSpelling = true)]
        public static extern int WlArrayCopy(_WlArray* array, _WlArray* source);
        [DllImport(LibWaylandClient, EntryPoint = "wl_display_create_queue", ExactSpelling = true)]
        public static extern _WlEventQueue* WlDisplayCreateQueue(_WlDisplay* display);
        [DllImport(LibWaylandClient, EntryPoint = "wl_display_connect_to_fd", ExactSpelling = true)]
        public static extern _WlDisplay* WlDisplayConnectToFd(int fd);
        [DllImport(LibWaylandClient, EntryPoint = "wl_display_connect", ExactSpelling = true)]
        public static extern _WlDisplay* WlDisplayConnect(char* name);
        [DllImport(LibWaylandClient, EntryPoint = "wl_display_disconnect", ExactSpelling = true)]
        public static extern void WlDisplayDisconnect(_WlDisplay* display);
        [DllImport(LibWaylandClient, EntryPoint = "wl_display_get_fd", ExactSpelling = true)]
        public static extern int WlDisplayGetFd(_WlDisplay* display);
        [DllImport(LibWaylandClient, EntryPoint = "wl_display_roundtrip_queue", ExactSpelling = true)]
        public static extern int WlDisplayRoundtripQueue(_WlDisplay* display, _WlEventQueue* queue);
        [DllImport(LibWaylandClient, EntryPoint = "wl_display_roundtrip", ExactSpelling = true)]
        public static extern int WlDisplayRoundtrip(_WlDisplay* display);
        [DllImport(LibWaylandClient, EntryPoint = "wl_display_read_events", ExactSpelling = true)]
        public static extern int WlDisplayReadEvents(_WlDisplay* display);
        [DllImport(LibWaylandClient, EntryPoint = "wl_display_prepare_read_queue", ExactSpelling = true)]
        public static extern int WlDisplayPrepareReadQueue(_WlDisplay* display, _WlEventQueue* queue);
        [DllImport(LibWaylandClient, EntryPoint = "wl_display_prepare_read", ExactSpelling = true)]
        public static extern int WlDisplayPrepareRead(_WlDisplay* display);
        [DllImport(LibWaylandClient, EntryPoint = "wl_display_cancel_read", ExactSpelling = true)]
        public static extern void WlDisplayCancelRead(_WlDisplay* display);
        [DllImport(LibWaylandClient, EntryPoint = "wl_display_dispatch_queue", ExactSpelling = true)]
        public static extern int WlDisplayDispatchQueue(_WlDisplay* display, _WlEventQueue* queue);
        [DllImport(LibWaylandClient, EntryPoint = "wl_display_dispatch_queue_pending", ExactSpelling = true)]
        public static extern int WlDisplayDispatchQueuePending(_WlDisplay* display, _WlEventQueue* queue);
        [DllImport(LibWaylandClient, EntryPoint = "wl_display_dispatch", ExactSpelling = true)]
        public static extern int WlDisplayDispatch(_WlDisplay* display);
        [DllImport(LibWaylandClient, EntryPoint = "wl_display_dispatch_pending", ExactSpelling = true)]
        public static extern int WlDisplayDispatchPending(_WlDisplay* display);
        [DllImport(LibWaylandClient, EntryPoint = "wl_display_get_error", ExactSpelling = true)]
        public static extern int WlDisplayGetError(_WlDisplay* display);
        [DllImport(LibWaylandClient, EntryPoint = "wl_display_get_protocol_error", ExactSpelling = true)]
        public static extern uint WlDisplayGetProtocolError(_WlDisplay* display, _WlInterface** @interface, uint* id);
        [DllImport(LibWaylandClient, EntryPoint = "wl_display_flush", ExactSpelling = true)]
        public static extern int WlDisplayFlush(_WlDisplay* display);
        [DllImport(LibWaylandClient, EntryPoint = "wl_event_queue_destroy", ExactSpelling = true)]
        public static extern void WlEventQueueDestroy(_WlEventQueue* queue);
        [DllImport(LibWaylandClient, EntryPoint = "wl_list_init", ExactSpelling = true)]
        public static extern void WlListInit(_WlList* list);
        [DllImport(LibWaylandClient, EntryPoint = "wl_list_insert", ExactSpelling = true)]
        public static extern void WlListInsert(_WlList* list, _WlList* elm);
        [DllImport(LibWaylandClient, EntryPoint = "wl_list_remove", ExactSpelling = true)]
        public static extern void WlListRemove(_WlList* elm);
        [DllImport(LibWaylandClient, EntryPoint = "wl_list_length", ExactSpelling = true)]
        public static extern int WlListLength(_WlList* list);
        [DllImport(LibWaylandClient, EntryPoint = "wl_list_empty", ExactSpelling = true)]
        public static extern int WlListEmpty(_WlList* list);
        [DllImport(LibWaylandClient, EntryPoint = "wl_list_insert_list", ExactSpelling = true)]
        public static extern void WlListInsertList(_WlList* list, _WlList* other);
        [DllImport(LibWaylandClient, EntryPoint = "wl_proxy_create", ExactSpelling = true)]
        public static extern _WlProxy* WlProxyCreate(_WlProxy* factory, _WlInterface* @interface);
        [DllImport(LibWaylandClient, EntryPoint = "wl_proxy_destroy", ExactSpelling = true)]
        public static extern void WlProxyDestroy(_WlProxy* proxy);
        [DllImport(LibWaylandClient, EntryPoint = "wl_proxy_add_listener", ExactSpelling = true)]
        public static extern int WlProxyAddListener(_WlProxy* proxy, void* implementation, void* data);
        [DllImport(LibWaylandClient, EntryPoint = "wl_proxy_get_listener", ExactSpelling = true)]
        public static extern void* WlProxyGetListener(_WlProxy* proxy);
        [DllImport(LibWaylandClient, EntryPoint = "wl_proxy_add_dispatcher", ExactSpelling = true)]
        public static extern int WlProxyAddDispatcher(_WlProxy* proxy, _WlDispatcherFuncT dispatcher, void* implementation, void* data);
        [DllImport(LibWaylandClient, EntryPoint = "wl_proxy_marshal_flags", ExactSpelling = true)]
        public static extern _WlProxy* WlProxyMarshalFlags(_WlProxy* proxy, uint opcode, _WlInterface* @interface, uint version, uint flags, uint param0, char* param1, uint param2);
        [DllImport(LibWaylandClient, EntryPoint = "wl_proxy_marshal_array_constructor", ExactSpelling = true)]
        public static extern _WlProxy* WlProxyMarshalArrayConstructor(_WlProxy* proxy, uint opcode, _WlArgument* args, _WlInterface* @interface);
        [DllImport(LibWaylandClient, EntryPoint = "wl_proxy_marshal_array_constructor_versioned", ExactSpelling = true)]
        public static extern _WlProxy* WlProxyMarshalArrayConstructorVersioned(_WlProxy* proxy, uint opcode, _WlArgument* args, _WlInterface* @interface, uint version);
        [DllImport(LibWaylandClient, EntryPoint = "wl_proxy_marshal_array_flags", ExactSpelling = true)]
        public static extern _WlProxy* WlProxyMarshalArrayFlags(_WlProxy* proxy, uint opcode, _WlInterface* @interface, uint version, uint flags, _WlArgument* args);
        [DllImport(LibWaylandClient, EntryPoint = "wl_proxy_marshal_constructor", ExactSpelling = true)]
        public static extern _WlProxy* WlProxyMarshalConstructor(_WlProxy* proxy, uint opcode, _WlInterface* @interface, __arglist);
        [DllImport(LibWaylandClient, EntryPoint = "wl_proxy_marshal_constructor_versioned", ExactSpelling = true)]
        public static extern _WlProxy* WlProxyMarshalConstructorVersioned(_WlProxy* proxy, uint opcode, _WlInterface* @interface, uint version, __arglist);
        [DllImport(LibWaylandClient, EntryPoint = "wl_proxy_marshal_array", ExactSpelling = true)]
        public static extern void WlProxyMarshalArray(_WlProxy* proxy, uint opcode, _WlArgument* args);
        [DllImport(LibWaylandClient, EntryPoint = "wl_proxy_set_user_data", ExactSpelling = true)]
        public static extern void WlProxySetUserData(_WlProxy* proxy, void* user_data);
        [DllImport(LibWaylandClient, EntryPoint = "wl_proxy_get_user_data", ExactSpelling = true)]
        public static extern void* WlProxyGetUserData(_WlProxy* proxy);
        [DllImport(LibWaylandClient, EntryPoint = "wl_proxy_get_version", ExactSpelling = true)]
        public static extern uint WlProxyGetVersion(_WlProxy* proxy);
        [DllImport(LibWaylandClient, EntryPoint = "wl_proxy_get_id", ExactSpelling = true)]
        public static extern uint WlProxyGetId(_WlProxy* proxy);
        [DllImport(LibWaylandClient, EntryPoint = "wl_proxy_set_tag", ExactSpelling = true)]
        public static extern void WlProxySetTag(_WlProxy* proxy, char* tag);
        [DllImport(LibWaylandClient, EntryPoint = "wl_proxy_get_tag", ExactSpelling = true)]
        public static extern char* WlProxyGetTag(_WlProxy* proxy);
        [DllImport(LibWaylandClient, EntryPoint = "wl_proxy_get_class", ExactSpelling = true)]
        public static extern char* WlProxyGetClass(_WlProxy* proxy);
        [DllImport(LibWaylandClient, EntryPoint = "wl_proxy_set_queue", ExactSpelling = true)]
        public static extern void WlProxySetQueue(_WlProxy* proxy, _WlEventQueue* queue);
        [DllImport(LibWaylandClient, EntryPoint = "wl_proxy_create_wrapper", ExactSpelling = true)]
        public static extern void* WlProxyCreateWrapper(void* proxy);
        [DllImport(LibWaylandClient, EntryPoint = "wl_fixed_to_double", ExactSpelling = true)]
        public static extern double WlFixedToDouble(_WlFixedT f);
        [DllImport(LibWaylandClient, EntryPoint = "wl_fixed_from_double", ExactSpelling = true)]
        public static extern _WlFixedT WlFixedFromDouble(double d);
        [DllImport(LibWaylandClient, EntryPoint = "wl_proxy_marshal_flags", ExactSpelling = true)]
        public static extern _WlProxy* WlProxyMarshalFlags(_WlProxy* proxy, uint opcode, _WlInterface* @interface, uint version, uint flags, _WlProxy* param0);
        [DllImport(LibWaylandClient, EntryPoint = "wl_proxy_marshal_flags", ExactSpelling = true)]
        public static extern _WlProxy* WlProxyMarshalFlags(_WlProxy* proxy, uint opcode, _WlInterface* @interface, uint version, uint flags, _WlProxy* param0, int param1, int param2, int param3, int param4, uint param5);
        [DllImport(LibWaylandClient, EntryPoint = "wl_proxy_marshal_flags", ExactSpelling = true)]
        public static extern _WlProxy* WlProxyMarshalFlags(_WlProxy* proxy, uint opcode, _WlInterface* @interface, uint version, uint flags);
        [DllImport(LibWaylandClient, EntryPoint = "wl_proxy_marshal_flags", ExactSpelling = true)]
        public static extern _WlProxy* WlProxyMarshalFlags(_WlProxy* proxy, uint opcode, _WlInterface* @interface, uint version, uint flags, int param0);
        [DllImport(LibWaylandClient, EntryPoint = "wl_proxy_marshal_flags", ExactSpelling = true)]
        public static extern _WlProxy* WlProxyMarshalFlags(_WlProxy* proxy, uint opcode, _WlInterface* @interface, uint version, uint flags, _WlProxy* param0, int param1, int param2);
        [DllImport(LibWaylandClient, EntryPoint = "wl_proxy_marshal_flags", ExactSpelling = true)]
        public static extern _WlProxy* WlProxyMarshalFlags(_WlProxy* proxy, uint opcode, _WlInterface* @interface, uint version, uint flags, uint param0, char* param1);
        [DllImport(LibWaylandClient, EntryPoint = "wl_proxy_marshal_flags", ExactSpelling = true)]
        public static extern _WlProxy* WlProxyMarshalFlags(_WlProxy* proxy, uint opcode, _WlInterface* @interface, uint version, uint flags, char* param0, int param1);
        [DllImport(LibWaylandClient, EntryPoint = "wl_proxy_marshal_flags", ExactSpelling = true)]
        public static extern _WlProxy* WlProxyMarshalFlags(_WlProxy* proxy, uint opcode, _WlInterface* @interface, uint version, uint flags, uint param0, uint param1);
        [DllImport(LibWaylandClient, EntryPoint = "wl_proxy_marshal_flags", ExactSpelling = true)]
        public static extern _WlProxy* WlProxyMarshalFlags(_WlProxy* proxy, uint opcode, _WlInterface* @interface, uint version, uint flags, char* param0);
        [DllImport(LibWaylandClient, EntryPoint = "wl_proxy_marshal_flags", ExactSpelling = true)]
        public static extern _WlProxy* WlProxyMarshalFlags(_WlProxy* proxy, uint opcode, _WlInterface* @interface, uint version, uint flags, uint param0);
        [DllImport(LibWaylandClient, EntryPoint = "wl_proxy_marshal_flags", ExactSpelling = true)]
        public static extern _WlProxy* WlProxyMarshalFlags(_WlProxy* proxy, uint opcode, _WlInterface* @interface, uint version, uint flags, _WlProxy* param0, _WlProxy* param1, _WlProxy* param2, uint param3);
        [DllImport(LibWaylandClient, EntryPoint = "wl_proxy_marshal_flags", ExactSpelling = true)]
        public static extern _WlProxy* WlProxyMarshalFlags(_WlProxy* proxy, uint opcode, _WlInterface* @interface, uint version, uint flags, _WlProxy* param0, uint param1);
        [DllImport(LibWaylandClient, EntryPoint = "wl_proxy_marshal_flags", ExactSpelling = true)]
        public static extern _WlProxy* WlProxyMarshalFlags(_WlProxy* proxy, uint opcode, _WlInterface* @interface, uint version, uint flags, _WlProxy* param0, _WlProxy* param1);
        [DllImport(LibWaylandClient, EntryPoint = "wl_proxy_marshal_flags", ExactSpelling = true)]
        public static extern _WlProxy* WlProxyMarshalFlags(_WlProxy* proxy, uint opcode, _WlInterface* @interface, uint version, uint flags, _WlProxy* param0, uint param1, uint param2);
        [DllImport(LibWaylandClient, EntryPoint = "wl_proxy_marshal_flags", ExactSpelling = true)]
        public static extern _WlProxy* WlProxyMarshalFlags(_WlProxy* proxy, uint opcode, _WlInterface* @interface, uint version, uint flags, _WlProxy* param0, int param1, int param2, uint param3);
        [DllImport(LibWaylandClient, EntryPoint = "wl_proxy_marshal_flags", ExactSpelling = true)]
        public static extern _WlProxy* WlProxyMarshalFlags(_WlProxy* proxy, uint opcode, _WlInterface* @interface, uint version, uint flags, uint param0, uint param1, _WlProxy* param2);
        [DllImport(LibWaylandClient, EntryPoint = "wl_proxy_marshal_flags", ExactSpelling = true)]
        public static extern _WlProxy* WlProxyMarshalFlags(_WlProxy* proxy, uint opcode, _WlInterface* @interface, uint version, uint flags, _WlProxy* param0, uint param1, _WlProxy* param2, int param3, int param4, uint param5);
        [DllImport(LibWaylandClient, EntryPoint = "wl_proxy_marshal_flags", ExactSpelling = true)]
        public static extern _WlProxy* WlProxyMarshalFlags(_WlProxy* proxy, uint opcode, _WlInterface* @interface, uint version, uint flags, int param0, int param1, int param2, int param3);
        [DllImport(LibWaylandClient, EntryPoint = "wl_proxy_marshal_flags", ExactSpelling = true)]
        public static extern _WlProxy* WlProxyMarshalFlags(_WlProxy* proxy, uint opcode, _WlInterface* @interface, uint version, uint flags, int param0, int param1);
        [DllImport(LibWaylandClient, EntryPoint = "wl_proxy_marshal_flags", ExactSpelling = true)]
        public static extern _WlProxy* WlProxyMarshalFlags(_WlProxy* proxy, uint opcode, _WlInterface* @interface, uint version, uint flags, uint param0, _WlProxy* param1, int param2, int param3);
        [DllImport(LibWaylandClient, EntryPoint = "wl_proxy_marshal_flags", ExactSpelling = true)]
        public static extern _WlProxy* WlProxyMarshalFlags(_WlProxy* proxy, uint opcode, _WlInterface* @interface, uint version, uint flags, _WlProxy* param0, _WlProxy* param1, _WlProxy* param2);
        [DllImport(LibWaylandClient, EntryPoint = "wl_proxy_marshal_flags", ExactSpelling = true)]
        public static extern _WlProxy* WlProxyMarshalFlags(_WlProxy* proxy, uint opcode, _WlInterface* @interface, uint version, uint flags, _WlProxy* param0, int param1, _WlProxy* param2);
        [DllImport(LibWaylandClient, EntryPoint = "wl_proxy_marshal_flags", ExactSpelling = true)]
        public static extern _WlProxy* WlProxyMarshalFlags(_WlProxy* proxy, uint opcode, _WlInterface* @interface, uint version, uint flags, _WlProxy* param0, int param1, _WlProxy* param2, int param3, int param4, int param5, int param6);
        [DllImport(LibWaylandClient, EntryPoint = "wl_proxy_marshal_flags", ExactSpelling = true)]
        public static extern _WlProxy* WlProxyMarshalFlags(_WlProxy* proxy, uint opcode, _WlInterface* @interface, uint version, uint flags, _WlProxy* param0, _WlProxy* param1, uint param2);
        [DllImport(LibWaylandClient, EntryPoint = "wl_proxy_marshal_flags", ExactSpelling = true)]
        public static extern _WlProxy* WlProxyMarshalFlags(_WlProxy* proxy, uint opcode, _WlInterface* @interface, uint version, uint flags, _WlProxy* param0, char* param1, uint param2);
        [DllImport(LibWaylandClient, EntryPoint = "wl_proxy_marshal_flags", ExactSpelling = true)]
        public static extern _WlProxy* WlProxyMarshalFlags(_WlProxy* proxy, uint opcode, _WlInterface* @interface, uint version, uint flags, _WlProxy* param0, char* param1, int param2, int param3, int param4, uint param5);
        [DllImport(LibWaylandClient, EntryPoint = "wl_proxy_marshal_flags", ExactSpelling = true)]
        public static extern _WlProxy* WlProxyMarshalFlags(_WlProxy* proxy, uint opcode, _WlInterface* @interface, uint version, uint flags, _WlProxy* param0, int param1, int param2, uint param3, uint param4, int param5, uint param6);
    }

    public partial class WlInterface
    {
        public static readonly WlInterface WlDisplay;
        public static readonly WlInterface WlCallback;
        public static readonly WlInterface WlRegistry;
        public static readonly WlInterface WlCompositor;
        public static readonly WlInterface WlSurface;
        public static readonly WlInterface WlOutput;
        public static readonly WlInterface WlBuffer;
        public static readonly WlInterface WlRegion;
        public static readonly WlInterface WlShmPool;
        public static readonly WlInterface WlShm;
        public static readonly WlInterface WlDataOffer;
        public static readonly WlInterface WlDataSource;
        public static readonly WlInterface WlDataDevice;
        public static readonly WlInterface WlDataDeviceManager;
        public static readonly WlInterface WlSeat;
        public static readonly WlInterface WlPointer;
        public static readonly WlInterface WlKeyboard;
        public static readonly WlInterface WlTouch;
        public static readonly WlInterface WlShell;
        public static readonly WlInterface WlShellSurface;
        public static readonly WlInterface WlSubcompositor;
        public static readonly WlInterface WlSubsurface;
        public static readonly WlInterface ZwlrExportDmabufManagerV1;
        public static readonly WlInterface ZwlrExportDmabufFrameV1;
        public static readonly WlInterface ZwlrScreencopyManagerV1;
        public static readonly WlInterface ZwlrScreencopyFrameV1;
        public static readonly WlInterface ZkdeScreencastUnstableV1;
        public static readonly WlInterface ZkdeScreencastStreamUnstableV1;
        public static readonly WlInterface ZxdgOutputManagerV1;
        public static readonly WlInterface ZxdgOutputV1;
        static WlInterface()
        {
            WlCallback = new WlInterface.Builder("wl_callback", 1).Event("done", "u", new WlInterface?[] { null });
            WlRegistry = new WlInterface.Builder("wl_registry", 1).Event("global", "usu", new WlInterface?[] { null, null, null }).Event("global_remove", "u", new WlInterface?[] { null }).Method("bind", "usun", new WlInterface?[] { null, null, null, null });
            WlDisplay = new WlInterface.Builder("wl_display", 1).Event("error", "ous", new WlInterface?[] { null, null, null }).Event("delete_id", "u", new WlInterface?[] { null }).Method("sync", "n", new WlInterface?[] { WlCallback }).Method("get_registry", "n", new WlInterface?[] { WlRegistry });
            WlOutput = new WlInterface.Builder("wl_output", 4).Event("geometry", "iiiiissi", new WlInterface?[] { null, null, null, null, null, null, null, null }).Event("mode", "uiii", new WlInterface?[] { null, null, null, null }).Event("done", "2", new WlInterface?[] { }).Event("scale", "2i", new WlInterface?[] { null }).Event("name", "4s", new WlInterface?[] { null }).Event("description", "4s", new WlInterface?[] { null }).Method("release", "3", new WlInterface?[] { });
            WlBuffer = new WlInterface.Builder("wl_buffer", 1).Event("release", "", new WlInterface?[] { }).Method("destroy", "", new WlInterface?[] { });
            WlRegion = new WlInterface.Builder("wl_region", 1).Method("destroy", "", new WlInterface?[] { }).Method("add", "iiii", new WlInterface?[] { null, null, null, null }).Method("subtract", "iiii", new WlInterface?[] { null, null, null, null });
            WlSurface = new WlInterface.Builder("wl_surface", 6).Event("enter", "o", new WlInterface?[] { WlOutput }).Event("leave", "o", new WlInterface?[] { WlOutput }).Event("preferred_buffer_scale", "6i", new WlInterface?[] { null }).Event("preferred_buffer_transform", "6u", new WlInterface?[] { null }).Method("destroy", "", new WlInterface?[] { }).Method("attach", "oii", new WlInterface?[] { WlBuffer, null, null }).Method("damage", "iiii", new WlInterface?[] { null, null, null, null }).Method("frame", "n", new WlInterface?[] { WlCallback }).Method("set_opaque_region", "o", new WlInterface?[] { WlRegion }).Method("set_input_region", "o", new WlInterface?[] { WlRegion }).Method("commit", "", new WlInterface?[] { }).Method("set_buffer_transform", "2i", new WlInterface?[] { null }).Method("set_buffer_scale", "3i", new WlInterface?[] { null }).Method("damage_buffer", "4iiii", new WlInterface?[] { null, null, null, null }).Method("offset", "5ii", new WlInterface?[] { null, null });
            WlCompositor = new WlInterface.Builder("wl_compositor", 6).Method("create_surface", "n", new WlInterface?[] { WlSurface }).Method("create_region", "n", new WlInterface?[] { WlRegion });
            WlShmPool = new WlInterface.Builder("wl_shm_pool", 1).Method("create_buffer", "niiiiu", new WlInterface?[] { WlBuffer, null, null, null, null, null }).Method("destroy", "", new WlInterface?[] { }).Method("resize", "i", new WlInterface?[] { null });
            WlShm = new WlInterface.Builder("wl_shm", 1).Event("format", "u", new WlInterface?[] { null }).Method("create_pool", "nhi", new WlInterface?[] { WlShmPool, null, null });
            WlDataOffer = new WlInterface.Builder("wl_data_offer", 3).Event("offer", "s", new WlInterface?[] { null }).Event("source_actions", "3u", new WlInterface?[] { null }).Event("action", "3u", new WlInterface?[] { null }).Method("accept", "us", new WlInterface?[] { null, null }).Method("receive", "sh", new WlInterface?[] { null, null }).Method("destroy", "", new WlInterface?[] { }).Method("finish", "3", new WlInterface?[] { }).Method("set_actions", "3uu", new WlInterface?[] { null, null });
            WlDataSource = new WlInterface.Builder("wl_data_source", 3).Event("target", "s", new WlInterface?[] { null }).Event("send", "sh", new WlInterface?[] { null, null }).Event("cancelled", "", new WlInterface?[] { }).Event("dnd_drop_performed", "3", new WlInterface?[] { }).Event("dnd_finished", "3", new WlInterface?[] { }).Event("action", "3u", new WlInterface?[] { null }).Method("offer", "s", new WlInterface?[] { null }).Method("destroy", "", new WlInterface?[] { }).Method("set_actions", "3u", new WlInterface?[] { null });
            WlDataDevice = new WlInterface.Builder("wl_data_device", 3).Event("data_offer", "n", new WlInterface?[] { WlDataOffer }).Event("enter", "uoffo", new WlInterface?[] { null, WlSurface, null, null, WlDataOffer }).Event("leave", "", new WlInterface?[] { }).Event("motion", "uff", new WlInterface?[] { null, null, null }).Event("drop", "", new WlInterface?[] { }).Event("selection", "o", new WlInterface?[] { WlDataOffer }).Method("start_drag", "ooou", new WlInterface?[] { WlDataSource, WlSurface, WlSurface, null }).Method("set_selection", "ou", new WlInterface?[] { WlDataSource, null }).Method("release", "2", new WlInterface?[] { });
            WlPointer = new WlInterface.Builder("wl_pointer", 8).Event("enter", "uoff", new WlInterface?[] { null, WlSurface, null, null }).Event("leave", "uo", new WlInterface?[] { null, WlSurface }).Event("motion", "uff", new WlInterface?[] { null, null, null }).Event("button", "uuuu", new WlInterface?[] { null, null, null, null }).Event("axis", "uuf", new WlInterface?[] { null, null, null }).Event("frame", "5", new WlInterface?[] { }).Event("axis_source", "5u", new WlInterface?[] { null }).Event("axis_stop", "5uu", new WlInterface?[] { null, null }).Event("axis_discrete", "5ui", new WlInterface?[] { null, null }).Event("axis_value120", "8ui", new WlInterface?[] { null, null }).Method("set_cursor", "uoii", new WlInterface?[] { null, WlSurface, null, null }).Method("release", "3", new WlInterface?[] { });
            WlKeyboard = new WlInterface.Builder("wl_keyboard", 8).Event("keymap", "uhu", new WlInterface?[] { null, null, null }).Event("enter", "uoa", new WlInterface?[] { null, WlSurface, null }).Event("leave", "uo", new WlInterface?[] { null, WlSurface }).Event("key", "uuuu", new WlInterface?[] { null, null, null, null }).Event("modifiers", "uuuuu", new WlInterface?[] { null, null, null, null, null }).Event("repeat_info", "4ii", new WlInterface?[] { null, null }).Method("release", "3", new WlInterface?[] { });
            WlTouch = new WlInterface.Builder("wl_touch", 8).Event("down", "uuoiff", new WlInterface?[] { null, null, WlSurface, null, null, null }).Event("up", "uui", new WlInterface?[] { null, null, null }).Event("motion", "uiff", new WlInterface?[] { null, null, null, null }).Event("frame", "", new WlInterface?[] { }).Event("cancel", "", new WlInterface?[] { }).Event("shape", "6iff", new WlInterface?[] { null, null, null }).Event("orientation", "6if", new WlInterface?[] { null, null }).Method("release", "3", new WlInterface?[] { });
            WlSeat = new WlInterface.Builder("wl_seat", 8).Event("capabilities", "u", new WlInterface?[] { null }).Event("name", "2s", new WlInterface?[] { null }).Method("get_pointer", "n", new WlInterface?[] { WlPointer }).Method("get_keyboard", "n", new WlInterface?[] { WlKeyboard }).Method("get_touch", "n", new WlInterface?[] { WlTouch }).Method("release", "5", new WlInterface?[] { });
            WlDataDeviceManager = new WlInterface.Builder("wl_data_device_manager", 3).Method("create_data_source", "n", new WlInterface?[] { WlDataSource }).Method("get_data_device", "no", new WlInterface?[] { WlDataDevice, WlSeat });
            WlShellSurface = new WlInterface.Builder("wl_shell_surface", 1).Event("ping", "u", new WlInterface?[] { null }).Event("configure", "uii", new WlInterface?[] { null, null, null }).Event("popup_done", "", new WlInterface?[] { }).Method("pong", "u", new WlInterface?[] { null }).Method("move", "ou", new WlInterface?[] { WlSeat, null }).Method("resize", "ouu", new WlInterface?[] { WlSeat, null, null }).Method("set_toplevel", "", new WlInterface?[] { }).Method("set_transient", "oiiu", new WlInterface?[] { WlSurface, null, null, null }).Method("set_fullscreen", "uuo", new WlInterface?[] { null, null, WlOutput }).Method("set_popup", "ouoiiu", new WlInterface?[] { WlSeat, null, WlSurface, null, null, null }).Method("set_maximized", "o", new WlInterface?[] { WlOutput }).Method("set_title", "s", new WlInterface?[] { null }).Method("set_class", "s", new WlInterface?[] { null });
            WlShell = new WlInterface.Builder("wl_shell", 1).Method("get_shell_surface", "no", new WlInterface?[] { WlShellSurface, WlSurface });
            WlSubsurface = new WlInterface.Builder("wl_subsurface", 1).Method("destroy", "", new WlInterface?[] { }).Method("set_position", "ii", new WlInterface?[] { null, null }).Method("place_above", "o", new WlInterface?[] { WlSurface }).Method("place_below", "o", new WlInterface?[] { WlSurface }).Method("set_sync", "", new WlInterface?[] { }).Method("set_desync", "", new WlInterface?[] { });
            WlSubcompositor = new WlInterface.Builder("wl_subcompositor", 1).Method("destroy", "", new WlInterface?[] { }).Method("get_subsurface", "noo", new WlInterface?[] { WlSubsurface, WlSurface, WlSurface });
            ZwlrExportDmabufFrameV1 = new WlInterface.Builder("zwlr_export_dmabuf_frame_v1", 1).Event("frame", "uuuuuuuuuu", new WlInterface?[] { null, null, null, null, null, null, null, null, null, null }).Event("object", "uhuuuu", new WlInterface?[] { null, null, null, null, null, null }).Event("ready", "uuu", new WlInterface?[] { null, null, null }).Event("cancel", "u", new WlInterface?[] { null }).Method("destroy", "", new WlInterface?[] { });
            ZwlrExportDmabufManagerV1 = new WlInterface.Builder("zwlr_export_dmabuf_manager_v1", 1).Method("capture_output", "nio", new WlInterface?[] { ZwlrExportDmabufFrameV1, null, WlOutput }).Method("destroy", "", new WlInterface?[] { });
            ZwlrScreencopyFrameV1 = new WlInterface.Builder("zwlr_screencopy_frame_v1", 3).Event("buffer", "uuuu", new WlInterface?[] { null, null, null, null }).Event("flags", "u", new WlInterface?[] { null }).Event("ready", "uuu", new WlInterface?[] { null, null, null }).Event("failed", "", new WlInterface?[] { }).Event("damage", "2uuuu", new WlInterface?[] { null, null, null, null }).Event("linux_dmabuf", "3uuu", new WlInterface?[] { null, null, null }).Event("buffer_done", "3", new WlInterface?[] { }).Method("copy", "o", new WlInterface?[] { WlBuffer }).Method("destroy", "", new WlInterface?[] { }).Method("copy_with_damage", "2o", new WlInterface?[] { WlBuffer });
            ZwlrScreencopyManagerV1 = new WlInterface.Builder("zwlr_screencopy_manager_v1", 3).Method("capture_output", "nio", new WlInterface?[] { ZwlrScreencopyFrameV1, null, WlOutput }).Method("capture_output_region", "nioiiii", new WlInterface?[] { ZwlrScreencopyFrameV1, null, WlOutput, null, null, null, null }).Method("destroy", "", new WlInterface?[] { });
            ZkdeScreencastStreamUnstableV1 = new WlInterface.Builder("zkde_screencast_stream_unstable_v1", 3).Event("closed", "", new WlInterface?[] { }).Event("created", "u", new WlInterface?[] { null }).Event("failed", "s", new WlInterface?[] { null }).Method("close", "", new WlInterface?[] { });
            ZkdeScreencastUnstableV1 = new WlInterface.Builder("zkde_screencast_unstable_v1", 3).Method("stream_output", "nou", new WlInterface?[] { ZkdeScreencastStreamUnstableV1, WlOutput, null }).Method("stream_window", "nsu", new WlInterface?[] { ZkdeScreencastStreamUnstableV1, null, null }).Method("destroy", "", new WlInterface?[] { }).Method("stream_virtual_output", "2nsiifu", new WlInterface?[] { ZkdeScreencastStreamUnstableV1, null, null, null, null, null }).Method("stream_region", "3niiuufu", new WlInterface?[] { ZkdeScreencastStreamUnstableV1, null, null, null, null, null, null });
            ZxdgOutputV1 = new WlInterface.Builder("zxdg_output_v1", 3).Event("logical_position", "ii", new WlInterface?[] { null, null }).Event("logical_size", "ii", new WlInterface?[] { null, null }).Event("done", "", new WlInterface?[] { }).Event("name", "2s", new WlInterface?[] { null }).Event("description", "2s", new WlInterface?[] { null }).Method("destroy", "", new WlInterface?[] { });
            ZxdgOutputManagerV1 = new WlInterface.Builder("zxdg_output_manager_v1", 3).Method("destroy", "", new WlInterface?[] { }).Method("get_xdg_output", "no", new WlInterface?[] { ZxdgOutputV1, WlOutput });
        }

        public static WlInterface FromInterfaceName(string name)
        {
            return name switch
            {
                "wl_callback" => WlCallback,
                "wl_registry" => WlRegistry,
                "wl_display" => WlDisplay,
                "wl_output" => WlOutput,
                "wl_buffer" => WlBuffer,
                "wl_region" => WlRegion,
                "wl_surface" => WlSurface,
                "wl_compositor" => WlCompositor,
                "wl_shm_pool" => WlShmPool,
                "wl_shm" => WlShm,
                "wl_data_offer" => WlDataOffer,
                "wl_data_source" => WlDataSource,
                "wl_data_device" => WlDataDevice,
                "wl_pointer" => WlPointer,
                "wl_keyboard" => WlKeyboard,
                "wl_touch" => WlTouch,
                "wl_seat" => WlSeat,
                "wl_data_device_manager" => WlDataDeviceManager,
                "wl_shell_surface" => WlShellSurface,
                "wl_shell" => WlShell,
                "wl_subsurface" => WlSubsurface,
                "wl_subcompositor" => WlSubcompositor,
                "zwlr_export_dmabuf_frame_v1" => ZwlrExportDmabufFrameV1,
                "zwlr_export_dmabuf_manager_v1" => ZwlrExportDmabufManagerV1,
                "zwlr_screencopy_frame_v1" => ZwlrScreencopyFrameV1,
                "zwlr_screencopy_manager_v1" => ZwlrScreencopyManagerV1,
                "zkde_screencast_stream_unstable_v1" => ZkdeScreencastStreamUnstableV1,
                "zkde_screencast_unstable_v1" => ZkdeScreencastUnstableV1,
                "zxdg_output_v1" => ZxdgOutputV1,
                "zxdg_output_manager_v1" => ZxdgOutputManagerV1,
                _ => throw new ArgumentException($"Unknown interface name: {name}")
            };
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    internal readonly unsafe struct _WlMessage
    {
        public readonly char* Name;
        public readonly char* Signature;
        public readonly _WlInterface** Types;
        public _WlMessage(char* name, char* signature, _WlInterface** types)
        {
            Name = name;
            Signature = signature;
            Types = types;
        }
    }

    public class WlMessage
    {
        public string Name { get; }

        public string Signature { get; }

        public ImmutableArray<WlInterface?> Types { get; }

        internal WlMessage(string name, string signature, ImmutableArray<WlInterface?> types)
        {
            Name = name;
            Signature = signature;
            Types = types;
        }

        internal unsafe _WlMessage ToBlittable()
        {
            var rawName = (char*)Marshal.StringToHGlobalAnsi(Name);
            var rawSignature = (char*)Marshal.StringToHGlobalAnsi(Signature);
            Span<IntPtr> rawTypesSpan = GC.AllocateArray<IntPtr>(Types.Length);
            var rawTypes = rawTypesSpan.Length > 0 ? (_WlInterface**)Unsafe.AsPointer(ref rawTypesSpan[0]) : null;
            for (var i = 0; i < Types.Length; i++)
            {
                rawTypes[i] = Types[i] is { } type ? type.ToBlittable() : (_WlInterface*)null;
            }

            return new _WlMessage(rawName, rawSignature, rawTypes);
        }

        internal class Builder
        {
            private readonly string _name;
            private readonly string _signature;
            private readonly ImmutableArray<WlInterface?> _types;
            public Builder(string name, string signature, WlInterface?[] types)
            {
                _name = name;
                _signature = signature;
                _types = types.ToImmutableArray();
            }

            public WlMessage Build()
            {
                return new WlMessage(_name, _signature, _types);
            }

            public static implicit operator WlMessage(Builder builder) => builder.Build();
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    internal readonly unsafe struct _WlInterface
    {
        public readonly char* Name;
        public readonly int Version;
        public readonly int MethodCount;
        public readonly _WlMessage* Methods;
        public readonly int EventCount;
        public readonly _WlMessage* Events;
        public _WlInterface(char* name, int version, int methodCount, _WlMessage* methods, int eventCount, _WlMessage* events)
        {
            Name = name;
            Version = version;
            MethodCount = methodCount;
            Methods = methods;
            EventCount = eventCount;
            Events = events;
        }
    }

    public unsafe partial class WlInterface
    {
        private readonly _WlInterface* _blittable;
        public string Name { get; }

        public int Version { get; }

        public ImmutableArray<WlMessage> Methods { get; }

        public ImmutableArray<WlMessage> Events { get; }

        internal unsafe WlInterface(string name, int version, ImmutableArray<WlMessage> methods, ImmutableArray<WlMessage> events)
        {
            Name = name;
            Version = version;
            Methods = methods;
            Events = events;
            var rawName = (char*)Marshal.StringToHGlobalAnsi(Name);
            Span<_WlMessage> rawMethodSpan = GC.AllocateArray<_WlMessage>(Methods.Length, true);
            var rawMethods = rawMethodSpan.Length > 0 ? (_WlMessage*)Unsafe.AsPointer(ref rawMethodSpan[0]) : null;
            Span<_WlMessage> rawEventSpan = GC.AllocateArray<_WlMessage>(Events.Length, true);
            var rawEvents = rawEventSpan.Length > 0 ? (_WlMessage*)Unsafe.AsPointer(ref rawEventSpan[0]) : null;
            for (var i = 0; i < Methods.Length; ++i)
            {
                var method = Methods[i];
                rawMethods[i] = method.ToBlittable();
            }

            for (var i = 0; i < Events.Length; ++i)
            {
                var event_ = Events[i];
                rawEvents[i] = event_.ToBlittable();
            }

            _blittable = (_WlInterface*)Marshal.AllocHGlobal(sizeof(_WlInterface));
            *_blittable = new _WlInterface(rawName, Version, Methods.Length, rawMethods, Events.Length, rawEvents);
        }

        internal _WlInterface* ToBlittable()
        {
            return _blittable;
        }

        internal class Builder
        {
            private readonly string _name;
            private readonly int _version;
            private readonly ImmutableArray<WlMessage>.Builder _methods;
            private readonly ImmutableArray<WlMessage>.Builder _events;
            public Builder(string name, int version)
            {
                _name = name;
                _version = version;
                _methods = ImmutableArray.CreateBuilder<WlMessage>();
                _events = ImmutableArray.CreateBuilder<WlMessage>();
            }

            public Builder Method(string name, string signature, WlInterface?[] types)
            {
                _methods.Add(new WlMessage.Builder(name, signature, types));
                return this;
            }

            public Builder Event(string name, string signature, WlInterface?[] types)
            {
                _events.Add(new WlMessage.Builder(name, signature, types));
                return this;
            }

            public WlInterface Build()
            {
                return new WlInterface(_name, _version, _methods.ToImmutable(), _events.ToImmutable());
            }

            public static implicit operator WlInterface(Builder builder) => builder.Build();
        }
    }

#pragma warning disable CS0649
    internal readonly unsafe struct _WlList
    {
        public readonly _WlList* Prev;
        public readonly _WlList* Next;
    }

    internal readonly unsafe struct _WlArray
    {
        public readonly int Size;
        public readonly int Alloc;
        public readonly void* Data;
    }

    internal readonly struct _WlFixedT : IEquatable<_WlFixedT>
    {
        private readonly uint _value;
        public bool Equals(_WlFixedT other)
        {
            return _value == other._value;
        }

        public override bool Equals(object? obj)
        {
            return obj is _WlFixedT t && Equals(t);
        }

        public override int GetHashCode()
        {
            return _value.GetHashCode();
        }

        public static bool operator ==(_WlFixedT left, _WlFixedT right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(_WlFixedT left, _WlFixedT right)
        {
            return !(left == right);
        }
    }

#pragma warning restore CS0649
    [StructLayout(LayoutKind.Explicit)]
    internal readonly unsafe struct _WlArgument
    {
        [FieldOffset(0)]
        public readonly int i;
        [FieldOffset(0)]
        public readonly uint u;
        [FieldOffset(0)]
        public readonly _WlFixedT f;
        [FieldOffset(0)]
        public readonly char* s;
        [FieldOffset(0)]
        public readonly void* o;
        [FieldOffset(0)]
        public readonly void* n;
        [FieldOffset(0)]
        public readonly _WlArray* a;
        [FieldOffset(0)]
        public readonly int h;
    }

    internal unsafe delegate int _WlDispatcherFuncT(void* data, void* target, uint callbackOpcode, _WlMessage* messageSignature, _WlArgument* args);
}