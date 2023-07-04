using System.Text.RegularExpressions;
using WlxOverlay.Types;

#pragma warning disable CS8618
// ReSharper disable ClassNeverInstantiated.Global
// ReSharper disable FieldCanBeMadeReadOnly.Global
// ReSharper disable CollectionNeverUpdated.Global
// ReSharper disable MemberCanBePrivate.Global
// ReSharper disable UnassignedField.Global

namespace WlxOverlay.Input;

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

    public AltLayoutMode AltLayoutMode;
    public string?[][] AltLayout;
    public string[] ShiftKeys;

    public Dictionary<string, string[]> ExecCommands;
    public Dictionary<string, string[]> Macros;
    public Dictionary<string, string?[]> Labels;

    public static Dictionary<KeyModifier, VirtualKey[]> ModifiersToKeys = new()
    {
        [KeyModifier.Shift] = new[] { VirtualKey.RShift, VirtualKey.LShift },
        [KeyModifier.CapsLock] = new[] { VirtualKey.Caps },
        [KeyModifier.Ctrl] = new[] { VirtualKey.RCtrl, VirtualKey.LCtrl },
        [KeyModifier.Alt] = new[] { VirtualKey.LAlt },
        [KeyModifier.NumLock] = new[] { VirtualKey.NumLock },
        [KeyModifier.Super] = new[] { VirtualKey.LSuper, VirtualKey.RSuper },
        [KeyModifier.Meta] = new[] { VirtualKey.Meta }
    };

    public static Dictionary<VirtualKey, KeyModifier> KeysToModifiers = new()
    {
        [VirtualKey.LShift] = KeyModifier.Shift,
        [VirtualKey.RShift] = KeyModifier.Shift,
        [VirtualKey.Caps] = KeyModifier.CapsLock,
        [VirtualKey.LCtrl] = KeyModifier.Ctrl,
        [VirtualKey.RCtrl] = KeyModifier.Ctrl,
        [VirtualKey.LAlt] = KeyModifier.Alt,
        [VirtualKey.NumLock] = KeyModifier.NumLock,
        [VirtualKey.LSuper] = KeyModifier.Super,
        [VirtualKey.RSuper] = KeyModifier.Super,
        [VirtualKey.Meta] = KeyModifier.Meta,
    };

    public string[] LabelForKey(string key, bool shift = false)
    {
        if (Labels.TryGetValue(key, out var label))
            return label!;

        if (key.Length == 1)
            return new[] { shift ? key.ToUpperInvariant() : key.ToLowerInvariant() };

        if (key.StartsWith("KP_"))
            key = key[3..];

        if (key.Contains("_"))
            key = key.Split('_').First();

        return new[] { Char.ToUpperInvariant(key[0]) + key[1..].ToLowerInvariant() };
    }

    private static readonly Regex MacroRx = new(@"^([A-Za-z0-1_-]+)(?: +(UP|DOWN))?$", RegexOptions.Compiled);

    public List<(VirtualKey key, bool down)> KeyEventsFromMacro(string[] macroVerbs)
    {
        var l = new List<(VirtualKey, bool)>();

        foreach (var verb in macroVerbs)
        {
            var m = MacroRx.Match(verb);

            if (m.Success)
            {
                if (!VirtualKey.TryParse(m.Groups[1].Value, out VirtualKey virtualKey))
                {
                    Console.WriteLine($"Unknown keycode in macro: '{m.Groups[1].Value}'");
                    return new List<(VirtualKey, bool)>();
                }

                if (!m.Groups[2].Success)
                {
                    l.Add((virtualKey, true));
                    l.Add((virtualKey, false));
                }
                else if (m.Groups[2].Value == "DOWN")
                    l.Add((virtualKey, true));
                else if (m.Groups[2].Value == "UP")
                    l.Add((virtualKey, false));
                else
                {
                    Console.WriteLine($"Unknown key state in macro: '{m.Groups[2].Value}', looking for UP or DOWN.");
                    return new List<(VirtualKey, bool)>();
                }
            }
        }

        return l;
    }

    private bool LoadAndCheckConfig()
    {
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

        var layoutsToCheck = AltLayoutMode == AltLayoutMode.Layout
            ? new[] { MainLayout, AltLayout }
            : new[] { MainLayout };

        foreach (var layout in layoutsToCheck)
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
                    if (!Enum.TryParse(s, out VirtualKey _)
                        && !ExecCommands.TryGetValue(s, out _)
                        && !Macros.TryGetValue(s, out _))
                    {
                        Console.WriteLine(
                            $"WARN keyboard.yaml: {layoutName} layout, row {i}: Keycode/Exec/Macro is not known for {s}! ** This key will not function! **");
                        layout[i][j] = null;
                    }
                }
            }
        }

        return true;
    }
}

public enum AltLayoutMode
{
    None,
    Shift,
    Super,
    Ctrl,
    Meta,
    Layout
}