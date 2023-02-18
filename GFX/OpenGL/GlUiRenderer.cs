using Silk.NET.Maths;
using Silk.NET.OpenGL;
using X11Overlay.Numerics;

namespace X11Overlay.GFX.OpenGL;

public class GlUiRenderer : IUiRenderer, IDisposable
{
    private readonly GlBuffer<uint> _ebo;
    private readonly GlBuffer<float> _vbo;
    private readonly GlVertexArray<float, uint> _vao;
    private readonly float[] _projectionMatrix = new float[16];
    private readonly GL _gl;

    private readonly float[] _vertices =
    {
        //X    Y      U   V
        0.5f,  0.5f,  1f, 0f,
        0.5f,  -0.5f, 1f, 1f,
        -0.5f, -0.5f, 0f, 1f,
        -0.5f, 0.5f,  0f, 0f
    };

    private readonly uint[] _indices =
    {
        0, 1, 3,
        1, 2, 3
    };

    private GlFramebuffer? _framebuffer;

    public GlUiRenderer(GL gl)
    {
        _gl = gl;
        _ebo = new GlBuffer<uint>(_gl, _indices, BufferTargetARB.ElementArrayBuffer);
        _vbo = new GlBuffer<float>(_gl, _vertices, BufferTargetARB.ArrayBuffer);
        _vao = new GlVertexArray<float, uint>(_gl, _vbo, _ebo);

        _vao.VertexAttributePointer(0, 2, VertexAttribPointerType.Float, 4, 0);
        _vao.VertexAttributePointer(1, 2, VertexAttribPointerType.Float, 4, 2);

    }

    public void Begin(ITexture texture)
    {
        var glTex = (GlTexture)texture;

        _framebuffer = new GlFramebuffer(_gl, glTex);
        _framebuffer.Bind();

        _gl.Viewport(0, 0, glTex.Width, glTex.Height);
        var m = Matrix4X4.CreateOrthographicOffCenter(0, glTex.Width, 0, glTex.Height, -5f, 5f);

        for (var i = 0; i < 4; i++)
            for (var j = 0; j < 4; j++)
                _projectionMatrix[i * 4 + j] = m[i, j];

        _gl.BlendFuncSeparate(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha, BlendingFactor.One, BlendingFactor.OneMinusSrcAlpha);
        _gl.BlendEquationSeparate(BlendEquationModeEXT.FuncAdd, BlendEquationModeEXT.FuncAdd);
        _gl.ColorMask(true, true, true, true);
    }

    public void End()
    {
        _gl.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
        _framebuffer?.Dispose();
        _framebuffer = null;
    }

    public void Clear()
    {
        _gl.Clear(ClearBufferMask.ColorBufferBit);
    }

    public unsafe void DrawSprite(ITexture sprite, int x, int y, uint w, uint h)
    {
        var shader = GlGraphicsEngine.SpriteShader;
        var glSprite = (GlTexture)sprite;

        UseRect(x, y, w, h);

        _vao.Bind();
        shader.Use();
        glSprite.Bind();
        shader.SetUniformM4("projection", _projectionMatrix);
        shader.SetUniform("uTexture0", 0);

        _gl.DrawElements(PrimitiveType.Triangles, (uint)_indices.Length, DrawElementsType.UnsignedInt, null);
    }

    public unsafe void DrawFont(Glyph glyph, Vector3 color, int x, int y, uint w, uint h)
    {
        var shader = GlGraphicsEngine.FontShader;
        var glSprite = (GlTexture)glyph.Texture;

        UseRect(x + glyph.Left, y - glyph.Top, w, h);

        _vao.Bind();
        shader.Use();
        glSprite.Bind();
        shader.SetUniformM4("projection", _projectionMatrix);
        shader.SetUniform("uTexture0", 0);
        shader.SetUniform("uColor", color.x, color.y, color.z, 1f);

        _gl.DrawElements(PrimitiveType.Triangles, (uint)_indices.Length, DrawElementsType.UnsignedInt, null);
    }

    public unsafe void DrawColor(Vector3 color, int x, int y, uint w, uint h)
    {
        var shader = GlGraphicsEngine.ColorShader;

        UseRect(x, y, w, h);

        _vao.Bind();
        shader.Use();
        shader.SetUniformM4("projection", _projectionMatrix);
        shader.SetUniform("uColor", color.x, color.y, color.z, 1f);

        _gl.DrawElements(PrimitiveType.Triangles, (uint)_indices.Length, DrawElementsType.UnsignedInt, null);
        _gl.GetError().AssertNone();
    }

    private void UseRect(float x, float y, float w, float h)
    {
        _vertices[8] = _vertices[12] = x;
        _vertices[5] = _vertices[9] = y;
        _vertices[0] = _vertices[4] = x + w;
        _vertices[1] = _vertices[13] = y + h;
        _vbo.Data(_vertices);
    }

    public void Dispose()
    {
        _ebo.Dispose();
        _vbo.Dispose();
        _vao.Dispose();
    }
}