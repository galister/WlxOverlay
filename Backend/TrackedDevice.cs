namespace WlxOverlay.Backend;

public struct TrackedDevice
{
    public bool Valid;
    public uint Index;
    public float SoC;
    public bool Charging;
    public TrackedDeviceRole Role;
}

public enum TrackedDeviceRole
{
    None,
    Hmd,
    LeftHand,
    RightHand,
    Tracker
}