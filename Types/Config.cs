using System.Reflection;
using WlxOverlay.Core.Interactions;
using WlxOverlay.Overlays;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

#pragma warning disable CS8618
// ReSharper disable ClassNeverInstantiated.Global
// ReSharper disable UnassignedField.Global

namespace WlxOverlay.Types;

public class Config
{
    public static readonly IDeserializer YamlDeserializer = new DeserializerBuilder()
        .WithNamingConvention(UnderscoredNamingConvention.Instance)
        .IgnoreUnmatchedProperties()
        .Build();

    public static readonly string AppDir = Environment.GetEnvironmentVariable("APPDIR") != null
                                            ? Path.Combine(Environment.GetEnvironmentVariable("APPDIR")!, "usr", "bin")
                                            : Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!;

    private static readonly string? XdgConfigHomeFolder = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME");

    public static readonly string UserConfigFolder = (
        string.IsNullOrWhiteSpace(XdgConfigHomeFolder)
        ? Path.Combine(Environment.GetEnvironmentVariable("HOME")!, ".config", "wlxoverlay")
        : Path.Combine(XdgConfigHomeFolder, "wlxoverlay")
    );

    public static readonly string ResourcesFolder = Path.Combine(AppDir, "Resources");

    public static readonly string[] ConfigFolders =
    {
        UserConfigFolder,
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "x11overlay"), // old branding
        ResourcesFolder
    };

    public static Config Instance;

    public static bool TryGetFile(string fName, out string fPath, bool msgIfNotFound = false)
    {
        foreach (var folder in ConfigFolders)
        {
            fPath = Path.Combine(folder, fName);
            if (File.Exists(fPath))
                return true;
        }

        if (msgIfNotFound)
            Console.WriteLine($"ERR: Could not find {fName}!\nLooked in: {string.Join(", ", ConfigFolders)}");

        fPath = null!;
        return false;
    }

    public static bool Load()
    {
        if (!TryGetFile("config.yaml", out var path, msgIfNotFound: true))
            return false;
        try
        {
            var yaml = File.ReadAllText(path);
            Instance = YamlDeserializer.Deserialize<Config>(yaml);
            return true;
        }
        catch
        {
            Console.WriteLine($"FATAL: Could not load {path}!");
            throw;
        }
    }

    public bool NoAutoStart;

    public string WaylandCapture;
    public bool WaylandColorSwap;

    public string[]? VolumeUpCmd;
    public string[]? VolumeDnCmd;

    // work around case conversion issue
    public bool use_12h_time;
    public bool Use12hTime => use_12h_time;

    public string? AltTimezone1;
    public string? AltTimezone2;

    public bool LeftUsePtt;
    public string[]? LeftPttDnCmd;
    public string[]? LeftPttUpCmd;

    public bool RightUsePtt;
    public string[]? RightPttDnCmd;
    public string[]? RightPttUpCmd;

    public LeftRight PrimaryHand;
    public LeftRight WatchHand;

    public bool RightClickOrientation;
    public bool MiddleClickOrientation;

    public bool ExperimentalFeatures;

    public string NotificationsEndpoint;
    public float NotificationsFadeTime;
    public bool DbusNotifications;

    public string? NotificationsSound;
    public float? NotificationsVolume;

    public string? KeyboardSound;
    public float? KeyboardVolume;

    public float? KeyboardHaptics;

    public float ClickFreezeTime;

    public string DefaultScreen;

    public const string DefaultPrimaryColor = "#006080";
    public const string DefaultShiftColor = "#B03000";
    public const string DefaultAltColor = "#600080";

    public string? PrimaryColor;
    public string? ShiftColor;
    public string? AltColor;

    public bool FallbackCursors;

    public Dictionary<string, string> ExportInputs;

    public Dictionary<string, string>? OverrideEnv;
}
