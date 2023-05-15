using WlxOverlay.Numerics;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

#pragma warning disable CS8618
// ReSharper disable ClassNeverInstantiated.Global
// ReSharper disable UnassignedField.Global

namespace WlxOverlay.Types;

public class Session 
{
    public static Session Instance;

    private static string _path = null!;
    private static ISerializer _serializer = null!;

    public static void Initialize()
    {
        _path = Path.Combine(Config.UserConfigFolder, "session.yaml");
        _serializer = new SerializerBuilder()
          .WithNamingConvention(UnderscoredNamingConvention.Instance)
          .Build();
        
        if (File.Exists(_path))
            try
            {
                var yaml = File.ReadAllText(_path);
                Instance = Config.YamlDeserializer.Deserialize<Session>(yaml);
            }
            catch { }

        Instance ??= new();
    }

    public void Persist()
    {
        if (!Directory.Exists(Config.UserConfigFolder))
          Directory.CreateDirectory(Config.UserConfigFolder);

        var yaml = _serializer.Serialize(this);
        File.WriteAllText(_path, yaml);
    }

    public Vector3 PlaySpaceOffset;
    public bool NotificationsMuteAudio;
    public bool NotificationsDnd;
}
