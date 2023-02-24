using System.Buffers;
using System.Runtime.InteropServices;
using WaylandSharp;
using WlxOverlay.GFX;
using WlxOverlay.GFX.OpenGL;

namespace WlxOverlay.Desktop.Wayland.Frame;

public class DmaBufFrame : IWaylandFrame
{
    private static readonly ArrayPool<uint> _uintPool = ArrayPool<uint>.Shared;
    private static readonly ArrayPool<int> _intPool = ArrayPool<int>.Shared;

    private readonly ZwlrExportDmabufFrameV1 _frame;

    private uint _width;
    private uint _height;
    private uint _format;
    private uint _modLo;
    private uint _modHi;
    private uint _numObjects;

    private int[]? _fds;
    private uint[]? _offsets;
    private uint[]? _pitches;

    private CaptureStatus _status;
    private IntPtr _eglImage;

    private static readonly EglEnum[,] ObjectAttributes = {
        { EglEnum.DmaBufPlane0FdExt, EglEnum.DmaBufPlane0OffsetExt, EglEnum.DmaBufPlane0PitchExt, EglEnum.DmaBufPlane0ModifierLoExt, EglEnum.DmaBufPlane0ModifierHiExt },
        { EglEnum.DmaBufPlane1FdExt, EglEnum.DmaBufPlane1OffsetExt, EglEnum.DmaBufPlane1PitchExt, EglEnum.DmaBufPlane1ModifierLoExt, EglEnum.DmaBufPlane1ModifierHiExt },
        { EglEnum.DmaBufPlane2FdExt, EglEnum.DmaBufPlane2OffsetExt, EglEnum.DmaBufPlane2PitchExt, EglEnum.DmaBufPlane2ModifierLoExt, EglEnum.DmaBufPlane2ModifierHiExt },
        { EglEnum.DmaBufPlane3FdExt, EglEnum.DmaBufPlane3OffsetExt, EglEnum.DmaBufPlane3PitchExt, EglEnum.DmaBufPlane3ModifierLoExt, EglEnum.DmaBufPlane3ModifierHiExt },
    };

    public DmaBufFrame(WlOutput output, ZwlrExportDmabufManagerV1 dmabufManager)
    {
        _frame = dmabufManager.CaptureOutput(1, output);
        _frame.Frame += OnFrame;
        _frame.Object += OnObject;
        _frame.Cancel += OnCancel;
        _frame.Ready += OnReady;
    }

    public CaptureStatus GetStatus() => _status;

    public void ApplyToTexture(ITexture texture)
    {
        if (texture is not GlTexture glTexture) return;

        var pool = ArrayPool<IntPtr>.Shared;
        var attribs = pool.Rent(7 + (int)_numObjects * 10);
        var i = 0;

        attribs[i++] = (IntPtr)EglEnum.Width;
        attribs[i++] = (IntPtr)_width;
        attribs[i++] = (IntPtr)EglEnum.Height;
        attribs[i++] = (IntPtr)_height;
        attribs[i++] = (IntPtr)EglEnum.LinuxDrmFourccExt;
        attribs[i++] = (IntPtr)_format;

        for (var p = 0; p < _numObjects; p++)
        {
            attribs[i++] = (IntPtr)ObjectAttributes[p, 0];
            attribs[i++] = (IntPtr)_fds![p];
            attribs[i++] = (IntPtr)ObjectAttributes[p, 1];
            attribs[i++] = (IntPtr)_offsets![p];
            attribs[i++] = (IntPtr)ObjectAttributes[p, 2];
            attribs[i++] = (IntPtr)_pitches![p];
            attribs[i++] = (IntPtr)ObjectAttributes[p, 3];
            attribs[i++] = (IntPtr)_modLo;
            attribs[i++] = (IntPtr)ObjectAttributes[p, 4];
            attribs[i++] = (IntPtr)_modHi;
        }

        attribs[i] = (IntPtr)EglEnum.None;

        _eglImage = EGL.CreateImage(EGL.Display, IntPtr.Zero, EglEnum.LinuxDmaBufExt, IntPtr.Zero, attribs);
        var error = EGL.GetError();
        if (error != EglEnum.Success)
            throw new ApplicationException($"{error} on eglCreateImage!");

        pool.Return(attribs);

        glTexture.LoadEglImage(_eglImage, _width, _height);
    }

    private void OnReady(object? _, ZwlrExportDmabufFrameV1.ReadyEventArgs e)
    {
        _status = _eglImage != IntPtr.Zero
            ? CaptureStatus.FrameReady
            : CaptureStatus.Fatal;
    }

    private void OnCancel(object? _, ZwlrExportDmabufFrameV1.CancelEventArgs e)
    {
        _status = e.Reason == ZwlrExportDmabufFrameV1CancelReason.Permanent
            ? CaptureStatus.Fatal
            : CaptureStatus.FrameSkipped;
    }

    private void OnObject(object? _, ZwlrExportDmabufFrameV1.ObjectEventArgs e)
    {
        _fds![e.Index] = e.Fd;
        _offsets![e.Index] = e.Offset;
        _pitches![e.Index] = e.Stride;
    }

    private void OnFrame(object? _, ZwlrExportDmabufFrameV1.FrameEventArgs e)
    {
        _width = e.Width;
        _height = e.Height;
        _format = e.Format;
        _modLo = e.ModLow;
        _modHi = e.ModHigh;
        _numObjects = e.NumObjects;

        _fds = _intPool.Rent((int)e.NumObjects);
        _pitches = _uintPool.Rent((int)e.NumObjects);
        _offsets = _uintPool.Rent((int)e.NumObjects);
    }

    public void Dispose()
    {
        if (_fds != null)
        {
            for (var i = 0; i < _numObjects; i++)
                if (_fds[i] != 0)
                    close(_fds[i]);

            _intPool.Return(_fds);
        }

        if (_pitches != null)
            _uintPool.Return(_pitches);

        if (_offsets != null)
            _uintPool.Return(_offsets);

        if (_eglImage != IntPtr.Zero)
            EGL.DestroyImage(EGL.Display, _eglImage);
        _frame.Dispose();
    }

    [DllImport("libc", SetLastError = true)]
    private static extern int close(int fileDescriptor);
}
