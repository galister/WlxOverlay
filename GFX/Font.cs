using System.Diagnostics;
using FreeTypeSharp.Native;
using static FreeTypeSharp.Native.FT;

namespace X11Overlay.GFX;

public class Font : IDisposable
{
    private readonly Dictionary<char, ITexture?> _glyphTextures = new();

    private static FT_Vector _nullVector;

    public Font(string font, int size)
    {
        var sw = Stopwatch.StartNew();
        
        var err = FT_Init_FreeType(out var ftLib);
        if (err != FT_Error.FT_Err_Ok)
            throw new ApplicationException($"Could not load FreeType library: {err}");

        err = FT_New_Face(ftLib, $"Resources/{font}.otf", 0, out var ftFace);
        if (err != FT_Error.FT_Err_Ok)
            throw new ApplicationException($"Could not load font {font}: {err}");

        err = FT_Set_Char_Size(ftFace, (IntPtr)(size << 6), (IntPtr)(size << 6), 96, 96);
        if (err != FT_Error.FT_Err_Ok)
            throw new ApplicationException($"Could not set size to {size}px for {font}: {err}");

        for (var c = (char)1; c < 255; c++)
        {
            LoadGlyph(c, ftFace);
        }
        
        FT_Done_Face(ftFace);
        FT_Done_FreeType(ftLib);
        
        sw.Stop();
        Console.WriteLine($"GlFont {font} {size}pt was loaded in: {sw.Elapsed}");
    }

    private unsafe void LoadGlyph(char ch, IntPtr ftFace)
    {
        var chIdx = FT_Get_Char_Index(ftFace, ch);
        
        var err = FT_Load_Glyph(ftFace, chIdx, FT_LOAD_DEFAULT);
        if (err != FT_Error.FT_Err_Ok)
            throw new ApplicationException($"Could not Load_Glyph for {(char)ch}: {err}");

        var ftFaceRec = (FT_FaceRec*)ftFace;
        
        err = FT_Get_Glyph((IntPtr)ftFaceRec->glyph, out var glyph);
        if (err != FT_Error.FT_Err_Ok)
            throw new ApplicationException($"Could not Get_Glyph for {(char)ch}: {err}");

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
            
            var tex = GraphicsEngine.Instance.TextureFromRaw(bitmap.width, bitmap.rows, inputFormat, bitmap.buffer, GraphicsFormat.R8);
            _glyphTextures[ch] = tex;
        }
    }

    public ITexture? GetTexture(char c)
    {
        return _glyphTextures.GetValueOrDefault(c, null);
    }

    public void Dispose()
    {
        foreach (var texture in _glyphTextures.Values) 
            texture!.Dispose();
    }
}