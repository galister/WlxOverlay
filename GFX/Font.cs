using FreeTypeSharp.Native;
using static FreeTypeSharp.Native.FT;

namespace X11Overlay.GFX;

public class Font : IDisposable
{
    private readonly Dictionary<char, Glyph?> _glyphTextures = new();

    private static FT_Vector _nullVector;
    private string _font;
    private int _size;
    private bool _loaded;

    public Font(string font, int size)
    {
        _font = font;
        _size = size;
    }

    private void LoadFont()
    {
        var err = FT_Init_FreeType(out var ftLib);
        if (err != FT_Error.FT_Err_Ok)
            throw new ApplicationException($"Could not load FreeType library: {err}");

        err = FT_New_Face(ftLib, $"Resources/{_font}", 0, out var ftFace);
        if (err != FT_Error.FT_Err_Ok)
            throw new ApplicationException($"Could not load font {_font}: {err}");

        err = FT_Set_Char_Size(ftFace, (IntPtr)(_size << 6), (IntPtr)(_size << 6), 96, 96);
        if (err != FT_Error.FT_Err_Ok)
            throw new ApplicationException($"Could not set size to {_size}px for {_font}: {err}");

        for (var c = (char)1; c < 255; c++)
        {
            LoadGlyph(c, ftFace);
        }
        
        FT_Done_Face(ftFace);
        FT_Done_FreeType(ftLib);
    }

    private unsafe void LoadGlyph(char ch, IntPtr ftFace)
    {
        var chIdx = FT_Get_Char_Index(ftFace, ch);
        
        var err = FT_Load_Glyph(ftFace, chIdx, FT_LOAD_DEFAULT);
        if (err != FT_Error.FT_Err_Ok)
            throw new ApplicationException($"Could not Load_Glyph for {ch}: {err}");

        var ftFaceRec = (FT_FaceRec*)ftFace;
        
        err = FT_Get_Glyph((IntPtr)ftFaceRec->glyph, out var glyph);
        if (err != FT_Error.FT_Err_Ok)
            throw new ApplicationException($"Could not Get_Glyph for {ch}: {err}");

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

            var gSlot = ((FT_FaceRec*)ftFace)->glyph;

            var g = new Glyph
            {
                Texture = GraphicsEngine.Instance.TextureFromRaw(bitmap.width, bitmap.rows, inputFormat, bitmap.buffer, GraphicsFormat.R8),
                Left = ((int)gSlot->metrics.horiBearingX >> 6),
                Top = (int)bitmap.rows - ((int)gSlot->metrics.horiBearingY >> 6),
                AdvX = (int)gSlot->metrics.horiAdvance >> 6
            };
            
            _glyphTextures[ch] = g;
        }
    }

    public Glyph? GetTexture(char c)
    {
        if (!_loaded)
        {
            LoadFont();
            _loaded = true;
        }
        
        return _glyphTextures.GetValueOrDefault(c, null);
    }

    public int Size() => _size;

    public void Dispose()
    {
        foreach (var glyph in _glyphTextures.Values)
            glyph!.Texture.Dispose();
    }
}

public class Glyph
{
    public ITexture Texture;
    public int Left;
    public int Top;
    public int BearX;
    public int BearY;
    public int AdvX;
}