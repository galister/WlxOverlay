using Valve.VR;
using WlxOverlay.Types;

namespace WlxOverlay.Core;

public class ManifestInstaller
{
    public static void EnsureInstalled(string appKey)
    {
        if (!OpenVR.Applications.IsApplicationInstalled(appKey)
            && Config.TryGetFile("manifest.vrmanifest", out var manifestPath))
        {
            var err = OpenVR.Applications.AddApplicationManifest(manifestPath, false);
            if (err == EVRApplicationError.None)
                OpenVR.Applications.SetApplicationAutoLaunch(appKey, true);
        }
    }
}