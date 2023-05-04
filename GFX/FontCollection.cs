using System.Diagnostics;

namespace WlxOverlay.GFX;

public class FontCollection
{
    private const string PrimaryFont = "LiberationSans";
    private static readonly object Lock = new();
    private static readonly Dictionary<(int, FontStyle), FontCollection> _collections = new();

    public static FontCollection Get(int size, FontStyle style)
    {
        if (!_collections.TryGetValue((size, style), out var collection))
        {
            collection = new FontCollection(size, style);
            lock (Lock)
                _collections[(size, style)] = collection;
        }
        return collection;
    }

    private readonly List<Font> _loadedFonts = new();
    private readonly Dictionary<int, Font> _codePointToFont = new();

    private readonly int _size;
    private readonly FontStyle _style;

    private FontCollection(int size, FontStyle style)
    {
        _size = size;
        _style = style;

        LoadFontForCodePoint('a', size, style);
        _codePointToFont[0] = _codePointToFont['a'];
    }

    public int GetTextWidth(string s)
    {
        return GetTextures(s).Sum(x => x?.AdvX ?? _size / 3);
    }

    public IEnumerable<Glyph?> GetTextures(string s)
    {
        for (var i = 0; i < s.Length; i += char.IsSurrogatePair(s, i) ? 2 : 1)
        {
            var cp = char.ConvertToUtf32(s, i);
            if (!_codePointToFont.TryGetValue(cp, out var font))
            {
                LoadFontForCodePoint(cp, _size, _style);
                font = _codePointToFont[cp];
            }

            yield return font.GetTexture(cp);
        }
    }

    public int Size() => _size;

    public static void CloseHandles()
    {
        lock (Lock)
            foreach (var fontCollection in _collections.Values)
                foreach (var font in fontCollection._loadedFonts)
                    font.CloseHandles();
    }

    private void LoadFontForCodePoint(int codepoint, int size, FontStyle style)
    {
        var psi = new ProcessStartInfo("fc-match")
        {
            ArgumentList = { "-f", "%{file} %{index}", $"{PrimaryFont}-{size}:style={style}:charset={codepoint:X4}" },
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        var p = Process.Start(psi)!;
        p.WaitForExit();
        var output = p.StandardOutput.ReadToEnd();
        var parts = output.Split(' ');
        if (parts.Length != 2)
        {
            _codePointToFont[codepoint] = _codePointToFont[0];
            return;
        }
        
        try 
        {
            var font = new Font(parts[0], int.Parse(parts[1]), size);
            lock (Lock)
                _loadedFonts.Add(font);
            foreach (var cp in font.GetSupportedCodePoints())
                _codePointToFont.TryAdd(cp, font);
        }
        catch (FontLoaderException x)
        {
            Console.WriteLine("WARN: " + x.Message);
        }

        if (!_codePointToFont.ContainsKey(codepoint))
            _codePointToFont[codepoint] = _codePointToFont[0];
    }
}

public enum FontStyle
{
    Regular,
    Bold,
}
