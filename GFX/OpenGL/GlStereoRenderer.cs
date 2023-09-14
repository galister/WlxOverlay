using Silk.NET.Maths;
using Silk.NET.OpenGL;
using Silk.NET.OpenXR;
using WlxOverlay.Numerics;

namespace WlxOverlay.GFX.OpenGL;

public class GlStereoRenderer : IDisposable
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

    private readonly GlFramebuffer?[] _fbs = new GlFramebuffer[2];
    private readonly Transform3D[] _pvMatrices = new Transform3D[2];
    private readonly Rect2Di[] _viewRects = new Rect2Di[2];

    private GlShader _shader = GlGraphicsEngine.QuadShader;

    public GlStereoRenderer(GL gl)
    {
        _gl = gl;
        _ebo = new GlBuffer<uint>(_gl, _indices, BufferTargetARB.ElementArrayBuffer);
        _vbo = new GlBuffer<float>(_gl, _vertices, BufferTargetARB.ArrayBuffer);
        _vao = new GlVertexArray<float, uint>(_gl, _vbo, _ebo);
        _gl.DebugAssertSuccess();

        _vao.VertexAttributePointer(0, 2, VertexAttribPointerType.Float, 4, 0);
        _gl.DebugAssertSuccess();
        _vao.VertexAttributePointer(1, 2, VertexAttribPointerType.Float, 4, 2);
        _gl.DebugAssertSuccess();
    }

    public void Begin(uint[] eyeTextures, Rect2Di[] rects, Transform3D[] projectionViews)
    {
        for (var i = 0; i < 2; i++)
        {
            _fbs[i] = new GlFramebuffer(_gl, eyeTextures[i]);
            _viewRects[i] = rects[i];
            _pvMatrices[i] = projectionViews[i];
        }

        _gl.BlendFuncSeparate(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha, BlendingFactor.One, BlendingFactor.OneMinusSrcAlpha);
        _gl.DebugAssertSuccess();

        _gl.BlendEquationSeparate(BlendEquationModeEXT.FuncAdd, BlendEquationModeEXT.FuncAdd);
        _gl.DebugAssertSuccess();

        _gl.ColorMask(true, true, true, true);
        _gl.DebugAssertSuccess();

        _vao.Bind();
    }

    public void UseShader(GlShader shader)
    {
        _shader = shader;
        _shader.Use();
    }

    public unsafe void DrawColor(Vector3 color)
    {
        _gl.BindTexture(TextureTarget.Texture2D, 0);
        _shader.SetUniform("uTexture0", 0);
        _shader.SetUniform("uColor", color.x, color.y, color.z, 1f);

        for (var eye = 0; eye < 2; eye++)
        {
            ref var rect = ref _viewRects[eye];

            _gl.Viewport(rect.Offset.X, rect.Offset.Y, (uint)rect.Extent.Width, (uint)rect.Extent.Height);
            _gl.DebugAssertSuccess();

            var m = Matrix4X4.CreateOrthographicOffCenter(0, rect.Extent.Width, 0, rect.Extent.Height, -5f, 5f);

            for (var i = 0; i < 4; i++)
                for (var j = 0; j < 4; j++)
                    _projectionMatrix[i * 4 + j] = m[i, j];

            _shader.SetUniformM4("projection", _projectionMatrix);

            _gl.DrawElements(PrimitiveType.Triangles, (uint)_indices.Length, DrawElementsType.UnsignedInt, null);
            _gl.DebugAssertSuccess();
        }
    }

    public unsafe void DrawQuad(ITexture? texture, Vector3 color, float alpha, Transform3D tModel)
    {
        if (texture is GlTexture glTex)
            glTex.Bind();
        else
        {
            _gl.BindTexture(TextureTarget.Texture2D, 0);
            _gl.DebugAssertSuccess();
        }

        _shader.SetUniform("uTexture0", 0);
        _shader.SetUniform("uColor", color.x, color.y, color.z, alpha);

        for (var eye = 0; eye < 2; eye++)
        {
            ref var rect = ref _viewRects[eye];

            _gl.Viewport(rect.Offset.X, rect.Offset.Y, (uint)rect.Extent.Width, (uint)rect.Extent.Height);
            _gl.DebugAssertSuccess();

            var mvp = _pvMatrices[eye] * tModel;

            for (var i = 0; i < 4; i++)
                for (var j = 0; j < 3; j++)
                    _projectionMatrix[i * 4 + j] = mvp[i, j];

            _shader.SetUniformM4("projection", _projectionMatrix);

            _gl.DrawElements(PrimitiveType.Triangles, (uint)_indices.Length, DrawElementsType.UnsignedInt, null);
            _gl.DebugAssertSuccess();
        }
    }

    public void Clear()
    {
        foreach (var framebuffer in _fbs)
        {
            framebuffer!.Bind();
            _gl.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
            _gl.DebugAssertSuccess();
        }
    }

    public void End()
    {
        _gl.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
        _gl.DebugAssertSuccess();
        for (var i = 0; i < _fbs.Length; i++)
        {
            _fbs[i]?.Dispose();
            _fbs[i] = null;
        }
    }

    public void Dispose()
    {
        _ebo.Dispose();
        _vbo.Dispose();
        _vao.Dispose();
    }
}