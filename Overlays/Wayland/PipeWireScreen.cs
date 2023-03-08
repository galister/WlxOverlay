using WlxOverlay.Desktop;
using WlxOverlay.Desktop.Pipewire;
using WlxOverlay.Desktop.Wayland;
using WlxOverlay.Overlays.Simple;

namespace WlxOverlay.Overlays.Wayland;

public class PipeWireScreen : BaseScreen<PipeWireScreenData>
{
    private nint _loop;
    private nint _context;
    private nint _stream;

    private object attribsLock = new();
    private nint[] attribs = new nint[47];
    
    
    public PipeWireScreen(PipeWireScreenData screen) : base(screen)
    {
        var _ = PwThread();
    }

    protected override void Initialize()
    {
        base.Initialize();
    }
    
    protected override bool MoveMouse(PointerHit hitData)
    {
        return false;
    }
    
    private async Task PwThread()
    {
        pw_init(0, IntPtr.Zero);
        
        _loop = pw_thread_loop_new("PipeWire thread loop", IntPtr.Zero);
        _context = pw_context_new(pw_thread_loop_get_loop(_loop), IntPtr.Zero, UIntPtr.Zero);
        if (pw_thread_loop_start(_loop) < 0)
            throw new Exception("Failed to start PipeWire thread loop");
        
        pw_thread_loop_lock(_loop);
        try
        {
            var core = pw_context_connect(_context, IntPtr.Zero, UIntPtr.Zero);
            if (core == IntPtr.Zero)
                throw new Exception("Failed to connect to PipeWire context");

            var props = pw_properties_new_string("media.type=Video media.category=Capture media.role=Screen");
            
            _stream = pw_stream_new(core, $"WlxOverlay-{Screen.Name}", props);
            if (_stream == IntPtr.Zero)
                throw new Exception("Failed to create PipeWire stream");
            
            var events = new pw_stream_events
            {
                state_changed = 
                    Marshal.GetFunctionPointerForDelegate(new pw_stream_state_changed_func(OnStreamStateChanged)),
                process = 
                    Marshal.GetFunctionPointerForDelegate(new pw_stream_process_func(OnStreamProcess))
            };
            var spaHook = new SpaHook();
            pw_stream_add_listener(_stream, spaHook.Ptr, ref events, IntPtr.Zero);
            pw_stream_connect(_stream, pw_direction.PW_DIRECTION_INPUT, Screen.NodeId, pw_stream_flags.AutoConnect | pw_stream_flags.MapBuffers, IntPtr.Zero, 0);
        }
        finally
        {
            pw_thread_loop_unlock(_loop);
        }
    }
    
    private unsafe void OnStreamProcess (nint data)
    {
        var buf = IntPtr.Zero;
        while (true)
        {
            var aux = pw_stream_dequeue_buffer(_stream);
            if (aux == IntPtr.Zero)
                break;
            if (buf != IntPtr.Zero)
                pw_stream_queue_buffer(_stream, buf);
            buf = aux;
        }

        if (buf == IntPtr.Zero)
            return;

        var pb = (pw_buffer*)buf.ToPointer();
        if (pb->buffer->datas[0].chunk->size == 0)
            return;

        switch (pb->buffer->datas[0].type)
        {
            case spa_data_type.SPA_DATA_DmaBuf:
            {
                var planes = pb->buffer->n_datas;
                
                var i = 0;
                attribs[i++] = (nint)EglEnum.Width;
                attribs[i++] = (nint)Screen.Size.X;
                attribs[i++] = (nint)EglEnum.Height;
                attribs[i++] = (nint)Screen.Size.Y;
                attribs[i++] = (nint)EglEnum.LinuxDrmFourccExt;
                attribs[i++] = (nint)DrmFormat.DRM_FORMAT_ARGB8888;

                for (var p = 0U; p < planes; p++)
                {
                    attribs[i++] = (nint)EGL.DmaBufAttribs[p, 0];
                    attribs[i++] = (nint)pb->buffer->datas[p].fd;
                    attribs[i++] = (nint)EGL.DmaBufAttribs[p, 1];
                    attribs[i++] = (nint)pb->buffer->datas[p].chunk->offset;
                    attribs[i++] = (nint)EGL.DmaBufAttribs[p, 2];
                    attribs[i++] = (nint)pb->buffer->datas[p].chunk->stride;
                    attribs[i++] = (nint)EGL.DmaBufAttribs[p, 3];
                    attribs[i++] = (nint)0;
                    attribs[i++] = (nint)EGL.DmaBufAttribs[p, 4];
                    attribs[i++] = (nint)0;
                }

                attribs[i] = (nint)EglEnum.None;
                break;
            }
            case spa_data_type.SPA_DATA_MemPtr:
            {
                attribs[0] = pb->buffer->datas[0].data;
                break;
            }
        }
        pw_stream_queue_buffer(_stream, buf);
    }

    protected internal override void Render()
    {
        lock (attribsLock)
        {
            if (attribs[0] == (nint)EglEnum.Width)
            {
                var image = EGL.CreateImage(EGL.Display, IntPtr.Zero, EglEnum.LinuxDmaBufExt, IntPtr.Zero, attribs);
            }
            else if (attribs[0] != 0)
            {
                
            }

            attribs[0] = IntPtr.Zero;
        }
        
        base.Render();
    }

    private void OnStreamStateChanged (nint stream, pw_stream_state oldState, pw_stream_state newState, string error)
    {
        Console.WriteLine($"Stream state changed: {oldState} -> {newState}");  
    }

    public override void Dispose()
    {
        base.Dispose();
    }
    
    const string Library = "libpipewire-0.3.so";
    
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    public static extern nint pw_thread_loop_new(string name, nint properties);
    
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    public static extern nint pw_thread_loop_get_loop(nint threadLoop);
    
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    public static extern int pw_thread_loop_start(nint threadLoop);
    
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    public static extern nint pw_context_connect(nint context, nint properties, nuint userDataSize);
    
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    public static extern void pw_thread_loop_lock(nint threadLoop);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    public static extern void pw_thread_loop_unlock(nint threadLoop);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    public static extern nint pw_stream_new(nint core, string name, nint props);
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    public static extern int pw_stream_connect(nint stream, pw_direction direction, uint target_id, pw_stream_flags flags, nint param, uint n_params);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    public static extern void pw_stream_add_listener(nint stream, nint listener, ref pw_stream_events events, nint data);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    public static extern nint pw_init(int argc, nint argv);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    public static extern nint pw_context_new(nint main_loop, nint properties, nuint size);
    
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    public static extern nint pw_properties_new_string([MarshalAs(UnmanagedType.LPStr)]string str);
    
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    public static extern nint pw_stream_dequeue_buffer(nint stream);
    
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    public static extern int pw_stream_queue_buffer(nint stream, nint buffer);
    
    public enum pw_direction
    {
        PW_DIRECTION_INPUT = 0,
        PW_DIRECTION_OUTPUT = 1,
    }

    [Flags]
    public enum pw_stream_flags
    {
        AutoConnect = 1 << 0,
        MapBuffers = 1 << 2,
    }
    
    [StructLayout(LayoutKind.Sequential)]
    public struct pw_stream_events
    {
        public uint version;
        public nint destroy;
        public nint state_changed;
        public nint control_info;
        public nint io_changed;
        public nint param_changed;
        public nint add_buffer;
        public nint remove_buffer;
        public nint process;
        public nint drained;
        public nint command;
        public nint trigger_done;
    }
    
    public delegate void pw_stream_state_changed_func(
        nint stream,
        pw_stream_state oldState,
        pw_stream_state newState,
        string error
    );
    
    public delegate void pw_stream_process_func(nint data);
    
    public enum pw_stream_state
    {
        Error = -1,
        Uninitialized = 0,
        Configured = 1,
        Ready = 2,
        Paused = 3,
        Streaming = 4,
        Done = 5
    }

}