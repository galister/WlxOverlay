using Valve.VR;
using X11Overlay.Types;

namespace X11Overlay;

public static class Extensions
{
    public static void CopyTo(this Vector2 v, ref HmdVector2_t h)
    {
        h.v0 = v.x;
        h.v1 = v.y;
    }
    
    public static void CopyTo(this Vector3 v, ref HmdVector3_t h)
    {
        h.v0 = v.x;
        h.v1 = v.y;
        h.v2 = v.z;
    }

    public static void CopyTo(this Transform3D t, ref HmdMatrix34_t h)
    {
        h.m0 = t[0, 0];
        h.m1 = t[1, 0];
        h.m2 = t[2, 0];
        h.m3 = t[3, 0];

        h.m4 = t[0, 1];
        h.m5 = t[1, 1];
        h.m6 = t[2, 1];
        h.m7 = t[3, 1];

        h.m8 = t[0, 2];
        h.m9 = t[1, 2];
        h.m10 = t[2, 2];
        h.m11 = t[3, 2];
    }

    public static Vector3 ToVector3(this HmdVector3_t v)
    {
        return new Vector3(v.v0, v.v1, v.v2);
    }

    public static Transform3D ToTransform3D(this HmdMatrix34_t pose)
    {
        return new Transform3D
        {
            basis = new Basis(
                new Vector3(pose.m0, pose.m4, pose.m8),
                new Vector3(pose.m1, pose.m5, pose.m9),
                new Vector3(pose.m2, pose.m6, pose.m10)
                ),
            origin = new Vector3(pose.m3, pose.m7, pose.m11)
        };
    }
}