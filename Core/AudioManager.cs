
namespace WlxOverlay.Core;

public class AudioManager
{
    public static AudioManager Instance { get; private set; } = null!;
    public static void Initialize() => Instance = new AudioManager();

    private readonly string[] _playerNames = 
    {
        "pw-play", "ffplay", "mpv", "aplay"
    };

    private readonly Type[] _playerTypes =
    {
        typeof(PwPlayPlayer), typeof(FfPlayPlayer), typeof(MpvPlayer), typeof(APlayPlayer)
    };

    private readonly AudioPlayer? _player;

    private AudioManager()
    {
        var values = Environment.GetEnvironmentVariable("PATH");
        foreach (var path in values.Split(Path.PathSeparator))
        {
            if (!Directory.Exists(path))
                continue;

            for (var p = 0; 0 < _playerNames.Length; p++)
                foreach (var fPath in Directory.EnumerateFiles(path))
                {
                    var fName = Path.GetFileName(fPath);
                    if (fName != _playerNames[p])
                        continue;

                    _player = (AudioPlayer)Activator.CreateInstance(_playerTypes[p])!;
                    Console.WriteLine($"Using {_playerNames[p]} for audio output.");
                    return;
                }
        }
        Console.WriteLine("WARN: No audio player found! Install either of the following if you need audio output: \n");
        Console.WriteLine("WARN: " + string.Join(", ", _playerNames));
    }
    
    public async Task PlayAsync(string file, float volume)
    {
        if (_player != null)
            await _player.PlayAsync(file, volume);
    }
}

public abstract class AudioPlayer
{
    protected abstract string Name { get; }
    protected abstract IEnumerable<string> GetArgs(string path, float volume);

    public async Task PlayAsync(string path, float volume)
    {
        var psi = new ProcessStartInfo(Name)
        {
            UseShellExecute = false,
            CreateNoWindow = true
        };
        foreach (var arg in GetArgs(path, volume))
            psi.ArgumentList.Add(arg);
        var p = Process.Start(psi);
        if (p != null)
        {
            await p.WaitForExitAsync();
            p.Dispose();
        }
    }
    
}

public class PwPlayPlayer : AudioPlayer
{
    protected override string Name => "pw-play";
    protected override IEnumerable<string> GetArgs(string path, float volume)
    {
        yield return "--volume";
        yield return volume.ToString("F");
        yield return path;
    }
}

public class FfPlayPlayer : AudioPlayer
{
    protected override string Name => "ffplay";
    protected override IEnumerable<string> GetArgs(string path, float volume)
    {
        yield return "-loglevel";
        yield return "quiet";
        yield return "-nodisp";
        yield return "-autoexit";
        yield return "-volume";
        yield return ((int)(volume * 100)).ToString();
        yield return "-i";
        yield return path;
    }
}

public class MpvPlayer : AudioPlayer
{
    protected override string Name => "mpv";
    protected override IEnumerable<string> GetArgs(string path, float volume)
    {
        yield return "--really-quiet";
        yield return "--volume=" + (int)(volume * 100);
        yield return path;
    }
}

public class APlayPlayer : AudioPlayer
{
    protected override string Name => "aplay";
    protected override IEnumerable<string> GetArgs(string path, float volume)
    {
        yield return "--quiet";
        yield return path;
    }
}