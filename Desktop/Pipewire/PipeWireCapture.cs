using WlxOverlay.GFX;
using WlxOverlay.GFX.OpenGL;
using WlxOverlay.Types;

namespace WlxOverlay.Desktop.Pipewire;

public class PipeWireCapture : IDisposable
{
    private readonly uint _nodeId;
    private readonly string _name;
    private readonly uint _width;
    private readonly uint _height;
    
    private nint _loop;
    private nint _context;
    private nint _stream;

    private nint _eglImage;
    private nint _lastEglImage;

    private readonly object _attribsLock = new();
    private readonly nint[] _attribs = new nint[47];

    public PipeWireCapture(uint nodeId, string name, uint width, uint height)
    {
        _nodeId = nodeId;
        _name = name;
        _width = width;
        _height = height;
    }


    /// <summary>
    /// Call this from BaseOverlay.Render()
    /// </summary>
    public void ApplyToTexture(ITexture texture)
    {
        if (texture is not GlTexture glTexture)
            return;
        
        if (_lastEglImage != IntPtr.Zero) 
            EGL.DestroyImage(EGL.Display, _lastEglImage);

        lock (_attribsLock)
        {
            if (_attribs[0] == (nint)EglEnum.Width)
            {
                _lastEglImage = _eglImage;
                _eglImage = EGL.CreateImage(EGL.Display, IntPtr.Zero, EglEnum.LinuxDmaBufExt, IntPtr.Zero, _attribs);
                var error = EGL.GetError();
                if (error != EglEnum.Success)
                    throw new ApplicationException($"{error} on eglCreateImage!");

                glTexture.LoadEglImage(_eglImage, _width, _height);
            }
            else if (_attribs[0] != 0)
            {
                var fmt = Config.Instance.ScreencopyColorSwap
                    ? GraphicsFormat.RGBA8
                    : GraphicsFormat.BGRA8;

                texture.LoadRawImage(_attribs[0], fmt, _width, _height);
            }

            _attribs[0] = IntPtr.Zero;
        }
    }
    
    public void Initialize()
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
            
            _stream = pw_stream_new(core, $"WlxOverlay-{_name}", props);
            if (_stream == IntPtr.Zero)
                throw new Exception("Failed to create PipeWire stream");
            
            var events = new PwStreamEvents
            {
                state_changed = 
                    Marshal.GetFunctionPointerForDelegate(new PwStreamStateChangedFunc(OnStreamStateChanged)),
                process = 
                    Marshal.GetFunctionPointerForDelegate(new PwStreamProcessFunc(OnStreamProcess))
            };
            var spaHook = new SpaHook();
            pw_stream_add_listener(_stream, spaHook.Ptr, ref events, IntPtr.Zero);
            pw_stream_connect(_stream, PwDirection.Input, _nodeId, PwStreamFlags.AutoConnect | PwStreamFlags.MapBuffers, IntPtr.Zero, 0);
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
                _attribs[i++] = (nint)EglEnum.Width;
                _attribs[i++] = (nint)_width;
                _attribs[i++] = (nint)EglEnum.Height;
                _attribs[i++] = (nint)_height;
                _attribs[i++] = (nint)EglEnum.LinuxDrmFourccExt;
                _attribs[i++] = (nint)DrmFormat.DRM_FORMAT_ARGB8888;

                for (var p = 0U; p < planes; p++)
                {
                    _attribs[i++] = (nint)EGL.DmaBufAttribs[p, 0];
                    _attribs[i++] = (nint)pb->buffer->datas[p].fd;
                    _attribs[i++] = (nint)EGL.DmaBufAttribs[p, 1];
                    _attribs[i++] = (nint)pb->buffer->datas[p].chunk->offset;
                    _attribs[i++] = (nint)EGL.DmaBufAttribs[p, 2];
                    _attribs[i++] = (nint)pb->buffer->datas[p].chunk->stride;
                    _attribs[i++] = (nint)EGL.DmaBufAttribs[p, 3];
                    _attribs[i++] = (nint)0;
                    _attribs[i++] = (nint)EGL.DmaBufAttribs[p, 4];
                    _attribs[i++] = (nint)0;
                }

                _attribs[i] = (nint)EglEnum.None;
                break;
            }
            case spa_data_type.SPA_DATA_MemPtr:
            {
                _attribs[0] = pb->buffer->datas[0].data;
                break;
            }
        }
        pw_stream_queue_buffer(_stream, buf);
    }

    private void OnStreamStateChanged (nint stream, PwStreamState oldState, PwStreamState newState, string error)
    {
        Console.WriteLine($"Stream state changed: {oldState} -> {newState}");  
    }

    public void Dispose()
    {
        if (_stream != IntPtr.Zero) 
            pw_stream_destroy(_stream);

        if (_context != IntPtr.Zero) 
            pw_context_destroy(_context);

        if (_loop != IntPtr.Zero) 
            pw_thread_loop_destroy(_loop);

        if (_eglImage != IntPtr.Zero) 
            EGL.DestroyImage(EGL.Display, _eglImage);

        if (_lastEglImage != IntPtr.Zero) 
            EGL.DestroyImage(EGL.Display, _lastEglImage);
    }
    
    
    const string Library = "libpipewire-0.3.so";
    
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    private static extern nint pw_thread_loop_new(string name, nint properties);
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    private static extern void pw_thread_loop_destroy(nint context);
    
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    private static extern nint pw_thread_loop_get_loop(nint threadLoop);
    
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    private static extern int pw_thread_loop_start(nint threadLoop);
    
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    private static extern nint pw_context_connect(nint context, nint properties, nuint userDataSize);
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    private static extern void pw_context_destroy(nint context);
    
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    private static extern void pw_thread_loop_lock(nint threadLoop);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    private static extern void pw_thread_loop_unlock(nint threadLoop);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    private static extern nint pw_stream_new(nint core, string name, nint props);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    private static extern void pw_stream_destroy(nint stream);
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    private static extern int pw_stream_connect(nint stream, PwDirection direction, uint targetId, PwStreamFlags flags, nint param, uint nParams);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    private static extern void pw_stream_add_listener(nint stream, nint listener, ref PwStreamEvents events, nint data);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    private static extern nint pw_init(int argc, nint argv);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    private static extern nint pw_context_new(nint mainLoop, nint properties, nuint size);
    
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    private static extern nint pw_properties_new_string([MarshalAs(UnmanagedType.LPStr)]string str);
    
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    private static extern nint pw_stream_dequeue_buffer(nint stream);
    
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    private static extern int pw_stream_queue_buffer(nint stream, nint buffer);

    private enum PwDirection
    {
        Input = 0,
        Output = 1,
    }

    [Flags]
    private enum PwStreamFlags
    {
        AutoConnect = 1 << 0,
        MapBuffers = 1 << 2,
    }
    
    [StructLayout(LayoutKind.Sequential)]
    private struct PwStreamEvents
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

    private delegate void PwStreamStateChangedFunc(
        nint stream,
        PwStreamState oldState,
        PwStreamState newState,
        string error
    );

    private delegate void PwStreamProcessFunc(nint data);

    private enum PwStreamState
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