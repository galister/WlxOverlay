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

    public static Config Instance;
    
    public static void Load()
    {
        var yaml = File.ReadAllText("Resources/config.yaml");
        Instance = YamlDeserializer.Deserialize<Config>(yaml);
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

}