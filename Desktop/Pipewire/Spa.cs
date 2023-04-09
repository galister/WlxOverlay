// ReSharper disable InconsistentNaming
// ReSharper disable MemberCanBePrivate.Global
// ReSharper disable FieldCanBeMadeReadOnly.Global
// ReSharper disable IdentifierTypo

namespace WlxOverlay.Desktop.Pipewire;

[StructLayout(LayoutKind.Sequential)]
public struct spa_rectangle
{
    public uint width;
    public uint height;
}

[StructLayout(LayoutKind.Sequential)]
public struct spa_video_info
{
    public uint media_type;
    public uint media_subtype;
    public spa_video_info_raw raw;
}

[StructLayout(LayoutKind.Sequential)]
public struct spa_video_info_raw
{
    public int format;
    public uint flags;
    public ulong modifier;
    public spa_rectangle size;
}

[StructLayout(LayoutKind.Sequential)]
public struct spa_list
{
    public IntPtr next;
    public IntPtr prev;
}

[StructLayout(LayoutKind.Sequential)]
public struct spa_callbacks
{
    public IntPtr funcs;
    public IntPtr data;
}

[StructLayout(LayoutKind.Sequential)]
public struct spa_hook
{
    public spa_list link;
    public spa_callbacks cb;
    public IntPtr removed;
    public IntPtr priv;
}

[StructLayout(LayoutKind.Sequential)]
public struct spa_pod
{
    public uint size;
    public uint type;
}

[StructLayout(LayoutKind.Sequential)]
public struct spa_meta
{
    public uint type;
    public uint size;
    public IntPtr data;
}

public enum spa_data_type : uint
{
    SPA_DATA_Invalid,
    SPA_DATA_MemPtr,
    SPA_DATA_MemFd,
    SPA_DATA_DmaBuf,
    SPA_DATA_MemId,
}

[StructLayout(LayoutKind.Sequential)]
public unsafe struct spa_data
{
    public spa_data_type type;
    public uint flags;
    public long fd;
    public uint mapoffset;
    public uint maxsize;
    public nint data;
    public spa_chunk* chunk;
}

[StructLayout(LayoutKind.Sequential)]
public struct spa_chunk
{
    public uint offset;
    public uint size;
    public int stride;
    public int flags;
}

[StructLayout(LayoutKind.Sequential)]
public unsafe struct spa_buffer
{
    public uint n_metas;
    public uint n_datas;
    public spa_meta* metas;
    public spa_data* datas;
}

[StructLayout(LayoutKind.Sequential)]
public unsafe struct pw_buffer
{
    public spa_buffer* buffer;
    public IntPtr user_data;
    public ulong size;
    public ulong requested;
}