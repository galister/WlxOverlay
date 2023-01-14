using System.Diagnostics;
using System.Text.RegularExpressions;

#pragma warning disable CS8618
// ReSharper disable ClassNeverInstantiated.Global
// ReSharper disable FieldCanBeMadeReadOnly.Global
// ReSharper disable CollectionNeverUpdated.Global
// ReSharper disable MemberCanBePrivate.Global
// ReSharper disable UnassignedField.Global

namespace X11Overlay.Types;

public class KeyboardLayout
{
    public static KeyboardLayout Instance;
    
    public static bool Load()
    {
        if (!Config.TryGetFile("keyboard.yaml", out var path, msgIfNotFound: true))
            return false;
        
        try
        {
            var yaml = File.ReadAllText(path);
            Instance = Config.YamlDeserializer.Deserialize<KeyboardLayout>(yaml);
            return Instance.LoadAndCheckConfig();
        }
        catch
        {
            Console.WriteLine($"FATAL: Could not load {path}!");
            throw;
        }
    }
    
    public string Name;
    public int RowSize;
    public float[][] KeySizes;
    public string?[][] MainLayout;
    public string?[][] AltLayout;
    public string[] ShiftKeys;
    
    public Dictionary<string, string[]> ExecCommands;
    public Dictionary<string, string[]> Macros;
    public Dictionary<string, string?[]> Labels;
    
    
    public Dictionary<string, int> Keycodes = new();
    public int[] ShiftKeyCodes;
    
    public HashSet<int> Modifiers = new();

    public string[] LabelForKey(string key, bool shift = false)
    {
        if (Labels.TryGetValue(key, out var label))
            return label!;

        if (key.Length == 1)
            return new [] { shift ? key.ToUpperInvariant() : key.ToLowerInvariant() };

        if (key.StartsWith("KP_"))
            key = key[3..];

        if (key.Contains("_"))
            key = key.Split('_').First();

        return new [] { Char.ToUpperInvariant(key[0]) + key[1..].ToLowerInvariant() };
    }

    private static readonly Regex MacroRx = new(@"^([A-Za-z0-1_-]+)(?: +(UP|DOWN))?$", RegexOptions.Compiled);

    public List<(int key, bool down)> KeyEventsFromMacro(string[] macroVerbs)
    {
        var l = new List<(int, bool)>();

        foreach (var verb in macroVerbs)
        {
            var m = MacroRx.Match(verb);
            
            if (m.Success)
            {
                if (!Keycodes.TryGetValue(m.Groups[1].Value, out var keycode))
                {
                    Console.WriteLine($"Unknown keycode in macro: '{m.Groups[1].Value}'");
                    return new List<(int, bool)>();
                }

                if (!m.Groups[2].Success)
                {
                    l.Add((keycode, true));
                    l.Add((keycode, false));
                }
                else if (m.Groups[2].Value == "DOWN")
                    l.Add((keycode, true));
                else if (m.Groups[2].Value == "UP")
                    l.Add((keycode, false));
                else
                {
                    Console.WriteLine($"Unknown key state in macro: '{m.Groups[2].Value}', looking for UP or DOWN.");
                    return new List<(int, bool)>();
                }
            }
        }

        return l;
    }

    private bool LoadAndCheckConfig()
    {
        var regex = new Regex(@"^keycode +(\d+) = (.+)$", RegexOptions.Compiled | RegexOptions.Multiline);
        var output = Process.Start(
            new ProcessStartInfo("xmodmap", "-pke") { RedirectStandardOutput = true, UseShellExecute = false }
        )!.StandardOutput.ReadToEnd();

        foreach (Match match in regex.Matches(output))
            if (match.Success)
                if (Int32.TryParse(match.Groups[1].Value, out var keyCode))
                    foreach (var exp in match.Groups[2].Value.Split(' '))
                        Keycodes.TryAdd(exp, keyCode);


        for (var i = 0; i < KeySizes.Length; i++)
        {
            var row = KeySizes[i];
            var rowWidth = row.Sum();
            if (rowWidth - RowSize > Single.Epsilon)
            {
                Console.WriteLine($"FATAL keyboard.yaml: Sizes, row {i}: Want {RowSize} units of width, got {rowWidth}!");
                return false;
            }
        }

        foreach (var layout in new[] { MainLayout, AltLayout })
        {
            var layoutName = layout == MainLayout ? "main" : "alt";

            for (var i = 0; i < KeySizes.Length; i++)
            {
                if (KeySizes[i].Length != layout[i].Length)
                {
                    Console.WriteLine(
                        $"FATAL keyboard.yaml: {layoutName} layout, row {i}: Want {KeySizes[i].Length} buttons, got {layout[i].Length}!");
                    return false;
                }

                for (var j = 0; j < layout[i].Length; j++)
                {
                    var s = layout[i][j];
                    if (s == null)
                        continue;
                    if (!Keycodes.TryGetValue(s, out _)
                        && !ExecCommands.TryGetValue(s, out _)
                        && !Macros.TryGetValue(s, out _))
                    {
                        Console.WriteLine(
                            $"WARN keyboard.yaml: {layoutName} layout, row {i}: Keycode/Exec/Macro is not known for {s}! ** This key will not function! **");
                        Console.WriteLine(
                            $"WARN If {s} is a dead key or your system keyboard, edit keyboard.yaml and replace {s} with your actual key!");
                        layout[i][j] = null;
                    }
                }
            }
        }

        Modifiers.Clear();
        foreach (var (key, code) in Keycodes)
        {
            if (key.StartsWith("Control_") || key.StartsWith("Shift_") || key.StartsWith("Alt_") ||
                key.StartsWith("Meta_") || key.StartsWith("Super_"))
                Modifiers.Add(code);
        }

        ShiftKeyCodes = new int[ShiftKeys.Length];
        for (var i = 0; i < ShiftKeys.Length; i++) 
            ShiftKeyCodes[i] = Keycodes[ShiftKeys[i]];

        return true;
    }
}