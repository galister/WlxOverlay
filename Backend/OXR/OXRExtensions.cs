using Silk.NET.OpenXR;
using WlxOverlay.Numerics;

namespace WlxOverlay.Backend.OXR;

public static class OXRExtensions
{
    public static void EnsureSuccess(this Result result)
    {
        if (result != Result.Success)
            throw new ApplicationException($"[Err] OpenXR: {result}");
    }

    public static void LogOnFail(this Result result)
    {
        if (result != Result.Success)
            Console.WriteLine($"[Warn] OpenXR: {result}");
    }

    public static Quaternionf ToOxr(this Quaternion q)
    {
        return new Quaternionf(q.x, q.y, q.z, q.w);
    }

    public static Vector3f ToOxr(this Vector3 v)
    {
        return new Vector3f(v.x, v.y, v.z);
    }

    public static Posef ToOxr(this Transform3D t)
    {
        return new Posef(t.basis.GetQuaternion().ToOxr(), t.origin.ToOxr());
    }

    public static Quaternion ToWlx(this Quaternionf q)
    {
        return new Quaternion(q.X, q.Y, q.Z, q.W);
    }

    public static Vector3 ToWlx(this Vector3f v)
    {
        return new Vector3(v.X, v.Y, v.Z);
    }

    public static Transform3D ToWlx(this Posef p)
    {
        return new Transform3D(p.Orientation.ToWlx(), p.Position.ToWlx());
    }
}