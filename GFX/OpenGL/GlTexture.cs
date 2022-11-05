using Silk.NET.OpenGL;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using X11Overlay.Numerics;

namespace X11Overlay.GFX.OpenGL;


    public class GlTexture : ITexture
    {
        internal readonly uint Handle;
        
        private readonly GL _gl;
        
        public uint Width { get; private set; }
        public uint Height { get; private set; }

        private bool _dynamic = false;

        public InternalFormat InternalFormat { get; private set; }

        public unsafe GlTexture(GL gl, string path, InternalFormat internalFormat = InternalFormat.Rgba8)
        {
            _gl = gl;
            Handle = _gl.GenTexture();
            _gl.GetError().AssertNone();
            
            Bind();

            using (var img = Image.Load<Rgba32>(path))
            {
                Allocate(internalFormat, (uint) img.Width, (uint) img.Height, 0, PixelFormat.Rgba, PixelType.UnsignedByte, null);

                img.ProcessPixelRows(accessor =>
                {
                    var maxY = accessor.Height - 1;
                    for (int y = 0; y < accessor.Height; y++)
                    {
                        fixed (void* data = accessor.GetRowSpan(y))
                        {
                            gl.TexSubImage2D(TextureTarget.Texture2D, 0, 0, maxY - y, (uint) accessor.Width, 1, PixelFormat.Rgba, PixelType.UnsignedByte, data);
                            _gl.GetError().AssertNone();
                        }
                    }
                });
            }

            SetParameters();
        }

        public unsafe GlTexture(GL gl, uint width, uint height, InternalFormat internalFormat = InternalFormat.Rgba8, bool dynamic = false)
        {
            _gl = gl;
            _dynamic = dynamic;
            
            Handle = _gl.GenTexture();
            _gl.GetError().AssertNone();
            
            Bind();

            //Reserve enough memory from the gpu for the whole image
            Allocate(internalFormat, width, height, 0, PixelFormat.Rgba, PixelType.UnsignedByte, null);
            SetParameters();
        }

        public unsafe GlTexture(GL gl, void* data, uint width, uint height, PixelFormat pixelFormat = PixelFormat.Rgba, PixelType pixelType = PixelType.UnsignedByte, InternalFormat internalFormat = InternalFormat.Rgba8)
        {
            _gl = gl;
            Handle = _gl.GenTexture();
            _gl.GetError().AssertNone();
            Bind();

            _gl.PixelStore( GLEnum.PackAlignment, 1 );
            _gl.PixelStore( GLEnum.UnpackAlignment, 1 );
            //We want the ability to create a texture using data generated from code aswell.
            //Setting the data of a texture.
            Allocate(internalFormat, width, height, 0, pixelFormat, pixelType, data);
            SetParameters();
        }

        private void SetParameters()
        {
            //Setting some texture perameters so the texture behaves as expected.
            _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int) GLEnum.ClampToEdge);
            _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int) GLEnum.ClampToEdge);
            _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int) GLEnum.LinearMipmapLinear);
            _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int) GLEnum.Linear);
            _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureBaseLevel, 0);
            _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMaxLevel, 8);
            //Generating mipmaps.
            _gl.GenerateMipmap(TextureTarget.Texture2D);
        }

        private unsafe void Allocate(InternalFormat internalFormat, uint width, uint height, int border,
            PixelFormat pixelFormat, PixelType pixelType, void* data)
        {
            Width = width;
            Height = height;
            InternalFormat = internalFormat;
            
            _gl.TexImage2D(TextureTarget.Texture2D, 0, (int) internalFormat, width, height, 0, pixelFormat, pixelType, data);
            _gl.GetError().AssertNone();
        }

        public void Bind(TextureUnit textureSlot = TextureUnit.Texture0)
        {
            //When we bind a texture we can choose which textureslot we can bind it to.
            _gl.ActiveTexture(textureSlot);
            _gl.BindTexture(TextureTarget.Texture2D, Handle);
        }

        public void Dispose()
        {
            //In order to dispose we need to delete the opengl handle for the texure.
            _gl.DeleteTexture(Handle);
        }

        public unsafe void LoadRawPixels(IntPtr ptr, GraphicsFormat graphicsFormat)
        {
            var (pf, pt) = GlGraphicsEngine.GraphicsFormatAsInput(graphicsFormat);
            
            var d = ptr.ToPointer();
            Bind();
            
            _gl.TexSubImage2D(TextureTarget.Texture2D, 0, 0, 0, Width, Height, pf, pt, d);
            _gl.GetError().AssertNone();
        }

        public void Begin()
        {
        }

        public void End()
        {
            
        }
        
        public void Draw(ITexture overlay, int xOffset, int yOffset)
        {
            Draw(overlay, xOffset, yOffset, overlay.GetWidth(), overlay.GetHeight());
        }
        
        public unsafe void Draw(ITexture overlay, int xOffset, int yOffset, uint width, uint height)
        {

            //var fbo = new GlFramebuffer(_gl, Width, Height);
            //fbo.Bind();
            //fbo.Texture(_handle);
            
            _gl.Viewport(0, 0, Width, Height);
            _gl.GetError().AssertNone();

            var verts = new Vertex[]
            {
                new(-1f, -1f, 0f, -1f, -1f),
                new(1f, -1f, 0f, 1f, -1f),
                new(-1f,  1f, 0f, -1f, 1f),
                new(-1f,  1f, 0f, -1f, 1f),
                new(1f, -1f, 0f, 1f, -1f),
                new(1f,  1f, 0f, 1f, 1f)
            };
            
            var indices = new uint[]
            {
                0, 1, 3,
                1, 2, 3
            };

            var ebo = new GlBuffer<uint>(_gl, indices, BufferTargetARB.ElementArrayBuffer);
            var vbo = new GlBuffer<Vertex>(_gl, verts, BufferTargetARB.ArrayBuffer);
            var vao = new GlVertexArray<Vertex, uint>(_gl, vbo, ebo);
            
            vao.VertexAttributePointer(0, 3, VertexAttribPointerType.Float, 5, 0);
            _gl.GetError().AssertNone();
            vao.VertexAttributePointer(1, 2, VertexAttribPointerType.Float, 5, 3);
            _gl.GetError().AssertNone();

            vao.Bind();
            var shader = GlGraphicsEngine.FontShader;
            shader.Use();
            ((GlTexture)overlay).Bind();
            shader.SetUniform("mainTex", 0);
            
            
            _gl.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
            _gl.ColorMask(true, true, true, false);
            
            _gl.DrawElements(PrimitiveType.Triangles, (uint)indices.Length, DrawElementsType.UnsignedInt, null);
            _gl.GetError().AssertNone();
            
            //_pixelUnpackBuffer.Bind();
            //_gl.MapBuffer(BufferTargetARB.PixelUnpackBuffer, BufferAccessARB.WriteOnly);
            //_gl.GetError().AssertNone();
            //_gl.BindBuffer(BufferTargetARB.PixelUnpackBuffer, 0);
            //_gl.GetError().AssertNone();

            _gl.BindBuffer(BufferTargetARB.ElementArrayBuffer, 0);
            _gl.BindBuffer(BufferTargetARB.ArrayBuffer, 0);
            _gl.BindVertexArray(0);
            _gl.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
        }

        public void Clear(Vector3 color)
        {
            Clear(color, 0, 0, Width, Height);
        }

        public void Clear(Vector3 color, int xOffset, int yOffset, uint width, uint height)
        {
            
            var arr = new[] { color.x, color.y, color.z };
            Bind();
            _gl.ClearTexSubImage(Handle, 0, xOffset, yOffset, 0, width, height, 0, PixelFormat.Rgb, PixelType.Float, (ReadOnlySpan<float>)arr);
        }

        public uint GetWidth()
        {
            return Width;
        }

        public uint GetHeight()
        {
            return Height;
        }

        public IntPtr GetNativeTexturePtr()
        {
            return (IntPtr) Handle;
        }

        public bool IsDynamic()
        {
            return _dynamic;
        }
    }
    
public struct Vertex
{
    Vector3 position;
    Vector2 texcoord;

    public Vertex(float x, float y, float z, float u, float v)
    {
        position = new Vector3(x, y, z);
        texcoord = new Vector2(u, v);
    }
};