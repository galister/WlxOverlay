using System.Dynamic;
using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Valve.VR;
using WlxOverlay.Types;

namespace WlxOverlay.Core;

public static class ManifestInstaller
{
    private const string AppKey = "galister.wlxoverlay";
    private static readonly string ManifestPath = Path.Combine(Config.UserConfigFolder, "manifest.vrmanifest");

    public static void EnsureInstalled()
    {
        var executablePath = GetExecutablePath();

        if (OpenVR.Applications.IsApplicationInstalled(AppKey))
        {
            try
            {
                dynamic manifest = JsonConvert.DeserializeObject<ExpandoObject>(File.ReadAllText(ManifestPath))!;
                if (manifest.applications[0].binary_path_linux == executablePath)
                    return;
                Console.WriteLine("Executable path changed, reinstalling manifest...");
            }
            catch
            {
                Console.WriteLine("Could not validate manifest, reinstalling...");
            }
        }

        if (!Directory.Exists(Config.UserConfigFolder))
            Directory.CreateDirectory(Config.UserConfigFolder);

        OpenVR.Applications.RemoveApplicationManifest(ManifestPath);

        CreateManifest(executablePath);

        var err = OpenVR.Applications.AddApplicationManifest(ManifestPath, false);
        if (err != EVRApplicationError.None)
        {
            Console.WriteLine($"ERR: Could not install manifest: {err}");
            return;
        }

        err = OpenVR.Applications.SetApplicationAutoLaunch(AppKey, true);
        if (err != EVRApplicationError.None)
        {
            Console.WriteLine($"ERR: Could not install manifest: {err}");
            return;
        }

        if (OpenVR.Applications.IsApplicationInstalled(AppKey))
            Console.WriteLine($"Manifest installed to {ManifestPath}");

    }

    private static string GetExecutablePath()
    {
        var appImage = Environment.GetEnvironmentVariable("APPIMAGE");
        if (appImage != null)
            return appImage;

        var folder = Path.GetDirectoryName(Assembly.GetEntryAssembly()!.Location)!;

        return Path.Combine(folder, "WlxOverlay");
    }

    private static void CreateManifest(string executablePath)
    {
        var manifest = new JObject
        {
            ["source"] = "builtin",
            ["applications"] = new JArray
            {
                new JObject
                {
                    ["app_key"] = AppKey,
                    ["launch_type"] = "binary",
                    ["binary_path_linux"] = executablePath,
                    ["is_dashboard_overlay"] = true,
                    ["strings"] = new JObject
                    {
                        ["en_us"] = new JObject
                        {
                            ["name"] = "WlxOverlay",
                            ["description"] = "A lightweight Wayland/X11 desktop overlay for OpenVR."
                        }
                    }
                }
            }
        };

        File.WriteAllText(ManifestPath, manifest.ToString());
    }
}