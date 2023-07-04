// Most of the logic comes from https://github.com/bendahl/uinput licensed under MIT
// Thanks bendahl!

using Tmds.Linux;
using static Tmds.Linux.LibC;

namespace WlxOverlay.Input.Impl;

public unsafe class UInput : IDisposable, IKeyboardProvider, IMouseProvider
{
    private const string UInputPath = "/dev/uinput";
    private const string DeviceName = "WlxOverlay Keyboard-Mouse Hybrid Thing";

    private const int AbsX = 0;
    private const int AbsY = 1;
    private const int relWheel = 0x8;

    private const int UiDevCreate = 0x5501;
    private const int UiDevDestroy = 0x5502;

    private const int UiSetEvBit = 0x40045564;
    private const int UiSetKeyBit = 0x40045565;

    private const int uiSetRelBit = 0x40045566;
    private const int UiSetAbsBit = 0x40045567;
    private const int BusUsb = 0x03;


    private readonly ArrayPool<byte> _bytePool = ArrayPool<byte>.Shared;
    private readonly int _fd;

    public const int Extent = 32768;

    public UInput()
    {
        InitEventObjects();

        var buf = _bytePool.Rent(256);
        var len = Encoding.UTF8.GetBytes(UInputPath, buf);
        buf[len] = 0;

        fixed (byte* ptr = buf)
        {
            _fd = open(ptr, O_WRONLY | O_NONBLOCK, 0x1B0); // 0660
        }

        _bytePool.Return(buf);

        RegisterDevice((int)Ev.Key);
        foreach (var btn in Enum.GetValues<EvBtn>())
            Ioctl(UiSetKeyBit, (int)btn);
        foreach (var key in Enum.GetValues<VirtualKey>())
            Ioctl(UiSetKeyBit, (int)key - 8);

        RegisterDevice((int)Ev.Abs);
        Ioctl(UiSetAbsBit, AbsX);
        Ioctl(UiSetAbsBit, AbsY);

        RegisterDevice((int)Ev.Rel);
        Ioctl(uiSetRelBit, relWheel);

        var dev = new UiUserDev();

        buf = _bytePool.Rent(256);
        len = Encoding.UTF8.GetBytes(DeviceName, buf);

        buf[len++] = 0;
        Marshal.Copy(buf, 0, new IntPtr(dev.Name), len);
        _bytePool.Return(buf);

        dev.Id.BusType = BusUsb;
        dev.Id.Vendor = 0x4711;
        dev.Id.Product = 0x0819;
        dev.Id.Version = 5;
        dev.AbsMin[0] = 0;
        dev.AbsMin[1] = 0;
        dev.AbsMax[0] = Extent;
        dev.AbsMax[1] = Extent;

        WriteObject(dev);
        Ioctl(UiDevCreate, 0);
        Thread.Sleep(200);
    }

    private void RegisterDevice(int evType)
    {
        Ioctl(UiSetEvBit, evType);
    }

    private void CloseDevice()
    {
        try
        {
            ReleaseDevice();
        }
        catch (Exception x)
        {
            Console.WriteLine(x.Message);
        }
        if (_fd != 0)
            close(_fd);
    }

    private void ReleaseDevice()
    {
        Ioctl(UiDevDestroy, 0);
    }

    private UiEvent _keyEvent;
    private readonly UiEvent[] _moveEvents = new UiEvent[3];

    private void InitEventObjects()
    {
        _keyEvent.Type = Ev.Key;
        _moveEvents[0].Type = Ev.Abs;
        _moveEvents[0].Code = AbsX;
        _moveEvents[1].Type = Ev.Abs;
        _moveEvents[1].Code = AbsY;
        _moveEvents[2].Type = Ev.Rel;
        _moveEvents[2].Code = relWheel;
    }

    public void MouseMove(int x, int y)
    {
        if (x == 0 && y == 0)
            y--;

        _moveEvents[0].Value = x;
        _moveEvents[1].Value = y;

        WriteObject(_moveEvents[0]);
        WriteObject(_moveEvents[1]);
        SyncEvents();
    }

    public void Wheel(int delta)
    {
        _moveEvents[2].Value = delta;
        WriteObject(_moveEvents[2]);
        SyncEvents();
    }

    private KeyModifier _curModifiers;
    public void SetModifiers(KeyModifier newModifiers)
    {
        var changed = _curModifiers ^ newModifiers;
        foreach (var mod in Enum.GetValues<KeyModifier>())
            if ((changed & mod) != 0)
                SendKey(KeyboardLayout.ModifiersToKeys[mod][0], (newModifiers & mod) != 0);

        _curModifiers = newModifiers;
    }

    public void SendKey(VirtualKey key, bool pressed)
    {
        _keyEvent.Code = (ushort)(key - 8);
        _keyEvent.Value = pressed ? 1 : 0;
        WriteObject(_keyEvent);
        SyncEvents();
    }

    public void SendButton(EvBtn button, bool pressed)
    {
        _keyEvent.Code = (ushort)button;
        _keyEvent.Value = pressed ? 1 : 0;
        WriteObject(_keyEvent);
        SyncEvents();
    }

    private void SyncEvents()
    {
        var iev = new UiEvent
        {
            Time = new TimeVal(),
            Type = Ev.Syn,
            Code = 0,
            Value = 0
        };
        WriteObject(iev);
    }

    private void WriteObject(object obj)
    {
        var buf = _bytePool.Rent(2048);
        var len = StructToBytes(obj, buf);

        fixed (byte* ptr = buf)
        {
            write(_fd, ptr, len);
        }
        _bytePool.Return(buf);
    }

    private int StructToBytes(object obj, byte[] buf)
    {
        IntPtr ptr = IntPtr.Zero;
        try
        {
            var size = Marshal.SizeOf(obj);
            ptr = Marshal.AllocHGlobal(size);
            Marshal.StructureToPtr(obj, ptr, false);
            Marshal.Copy(ptr, buf, 0, size);
            return size;
        }
        finally
        {
            Marshal.FreeHGlobal(ptr);
        }
    }

    private void Ioctl(int cmd, int arg)
    {
        var err = ioctl(_fd, cmd, arg);
        if (err != 0)
            throw new ApplicationException($"ioctl returned {err}");
    }

    public void Dispose()
    {
        CloseDevice();
    }
}

// ReSharper disable UnusedMember.Global
// ReSharper disable UnassignedField.Global
// ReSharper disable NotAccessedField.Global
public unsafe struct UiUserDev
{
    public fixed byte Name[80];
    public InputId Id;
    public uint EffectsMax;
    public fixed int AbsMax[64];
    public fixed int AbsMin[64];
    public fixed int AbsFuzz[64];
    public fixed int AbsFlat[64];
}

public struct UiEvent
{
    public TimeVal Time;
    public Ev Type;
    public ushort Code;
    public int Value;
}

public struct InputId
{
    public ushort BusType;
    public ushort Vendor;
    public ushort Product;
    public ushort Version;
}

public struct TimeVal
{
    public time_t TvSec;
    public time_t TvUSec;
}

public enum Ev : ushort
{
    Syn = 0x0,
    Key = 0x1,
    Rel = 0x2,
    Abs = 0x3
}

public enum EvBtn
{
    Left = 0x110,
    Right = 0x111,
    Middle = 0x112,
    Touch = 0x14a
}