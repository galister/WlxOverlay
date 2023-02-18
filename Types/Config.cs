using System.Reflection;
using X11Overlay.Overlays;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

#pragma warning disable CS8618
// ReSharper disable ClassNeverInstantiated.Global
// ReSharper disable UnassignedField.Global

namespace X11Overlay.Types;

public class Config
{
    public static readonly IDeserializer YamlDeserializer = new DeserializerBuilder().WithNamingConvention(UnderscoredNamingConvention.Instance).Build();

    public static string AppFolder = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!;

    public static readonly string[] ConfigFolders =
    {
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "x11overlay"),
        "Resources"
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

    public string[]? VolumeUpCmd;
    public string[]? VolumeDnCmd;

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

    public float ClickFreezeTime;

    public int DefaultScreen;

    public bool FallbackCursors;

    public Dictionary<string, string> ExportInputs;
}
