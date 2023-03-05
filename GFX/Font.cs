using FreeTypeSharp.Native;
using static FreeTypeSharp.Native.FT;

namespace WlxOverlay.GFX;

internal class Font : IDisposable
{
    private static FT_Vector _nullVector;
    
    private readonly Dictionary<int, uint> _glyphIndices = new();
    private readonly Dictionary<int, Glyph?> _glyphTextures = new();

    private readonly string _font;
    private readonly int _size;
    private readonly string _path;
    private readonly int _index;

    internal Font(string path, int index, int size)
    {
        _font = Path.GetFileNameWithoutExtension(path);
        _path = path;
        _index = index;
        _size = size;
        
        LoaderInit();
        LoadGlyphIndices();
    }

    private IntPtr _ftLib = IntPtr.Zero;
    private IntPtr _ftFace = IntPtr.Zero;
    private void LoaderInit()
    {
        if (_ftLib != IntPtr.Zero)
            return;

        var err = FT_Init_FreeType(out _ftLib);
        if (err != FT_Error.FT_Err_Ok)
            throw new ApplicationException($"Could not load FreeType library: {err}");
        
        
        err = FT_New_Face(_ftLib, _path, _index, out _ftFace);
        if (err != FT_Error.FT_Err_Ok)
            throw new ApplicationException($"Could not load font {_font}: {err}");

        err = FT_Select_Charmap(_ftFace, FT_Encoding.FT_ENCODING_UNICODE);
        if (err != FT_Error.FT_Err_Ok)
            throw new ApplicationException($"Could not use unicode char map on {_font}: {err}");
        
        err = FT_Set_Char_Size(_ftFace, (IntPtr)(_size << 6), (IntPtr)(_size << 6), 96, 96);
        if (err != FT_Error.FT_Err_Ok)
            throw new ApplicationException($"Could not set size to {_size}px for {_font}: {err}");
    }

    private void LoadGlyphIndices()
    {
        var ch = FT_Get_First_Char(_ftFace, out var gl);
        while (gl != 0)
        {
            _glyphIndices[(int)ch] = gl;
            ch = FT_Get_Next_Char(_ftFace, ch, out gl);
        }
    }

    public IEnumerable<int> GetSupportedCodePoints() => _glyphIndices.Keys;

    private void LoaderDone()
    {
        if (_ftLib == IntPtr.Zero)
            return;
         
        FT_Done_Face(_ftFace);
        FT_Done_FreeType(_ftLib);
        
        _ftFace = IntPtr.Zero;
        _ftLib = IntPtr.Zero;
    }

    private unsafe void LoadGlyph(int ch)
    {
        var chIdx = _glyphIndices.GetValueOrDefault(ch, 0U);

        var err = FT_Load_Glyph(_ftFace, chIdx, FT_LOAD_DEFAULT);
        if (err != FT_Error.FT_Err_Ok)
            throw new ApplicationException($"Could not Load_Glyph for U+{ch:X4} - {err}");

        var ftFaceRec = (FT_FaceRec*)_ftFace;

        err = FT_Get_Glyph((IntPtr)ftFaceRec->glyph, out var glyph);
        if (err != FT_Error.FT_Err_Ok)
            throw new ApplicationException($"Could not Get_Glyph for U+{ch:X4} - {err}");

        FT_Glyph_To_Bitmap(ref glyph, FT_Render_Mode.FT_RENDER_MODE_NORMAL, ref _nullVector, true);

        var bitmapGlyph = (FT_BitmapGlyphRec*)glyph;
        var bitmap = bitmapGlyph->bitmap;

        if (bitmap.width * bitmap.rows == 0)
            _glyphTextures[ch] = null;
        else
        {
            GraphicsFormat inputFormat;
            switch ((FT_Pixel_Mode)bitmap.pixel_mode)
            {
                case FT_Pixel_Mode.FT_PIXEL_MODE_MONO:
                case FT_Pixel_Mode.FT_PIXEL_MODE_GRAY:
                    inputFormat = GraphicsFormat.R8;
                    break;
                case FT_Pixel_Mode.FT_PIXEL_MODE_GRAY2:
                    inputFormat = GraphicsFormat.R16;
                    break;
                case FT_Pixel_Mode.FT_PIXEL_MODE_GRAY4:
                    inputFormat = GraphicsFormat.R32;
                    break;
                case FT_Pixel_Mode.FT_PIXEL_MODE_BGRA:
                    inputFormat = GraphicsFormat.BGRA8;
                    break;
                default:
                    throw new ApplicationException($"Unsupported FT_Pixel_Mode: {bitmap.pixel_mode}");
            }

            var gSlot = ((FT_FaceRec*)_ftFace)->glyph;

            var g = new Glyph
            {
                Texture = GraphicsEngine.Instance.TextureFromRaw(bitmap.width, bitmap.rows, inputFormat, bitmap.buffer, GraphicsFormat.R8),
                Left = (int)gSlot->metrics.horiBearingX >> 6,
                Top = (int)bitmap.rows - ((int)gSlot->metrics.horiBearingY >> 6),
                AdvX = (int)gSlot->metrics.horiAdvance >> 6
            };

            _glyphTextures[ch] = g;
        }
    }

    public Glyph? GetTexture(int cp)
    {
        if (!_glyphTextures.TryGetValue(cp, out var g))
        {
            LoadGlyph(cp);
            g = _glyphTextures[cp];
        }
        return g;
    }

    public void Dispose()
    {
        LoaderDone();
        foreach (var glyph in _glyphTextures.Values)
            glyph!.Texture.Dispose();
    }
}

public class Glyph
{
    public ITexture Texture = null!;
    public int Left;
    public int Top;
    public int BearX;
    public int BearY;
    public int AdvX;
}