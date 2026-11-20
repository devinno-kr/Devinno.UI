using OpenTK.Graphics.OpenGL4;
using OpenTK.Windowing.GraphicsLibraryFramework;
using SkiaSharp;

namespace Devinno.UI.Scene3D;

/// <summary>
/// What one view drew. One per view: on the GPU path Skia reads the texture at the end of the frame, so views cannot share it.
/// <list type="bullet">
/// <item>GPU path: a texture in the host context and the <see cref="SKImage"/> that wraps it (no copy).</item>
/// <item>CPU path: a renderbuffer in the hidden context and the <see cref="SKBitmap"/> that receives its pixels (kept while the size is the same).</item>
/// </list>
/// </summary>
internal sealed unsafe class SceneTarget
{
    private const uint GlTexture2D = 0x0DE1, GlRgba8 = 0x8058;

    private int _texture, _renderbuffer;
    private SKImage? _image;
    private SKImage? _pixels;

    /// <summary>The context (GLFW window) drawn in; null before the first draw.</summary>
    public Window* Context { get; private set; }

    /// <summary>The host GRContext on the GPU path.</summary>
    public GRContext? Gr { get; private set; }

    public int Fbo { get; private set; }
    public int Width { get; private set; }
    public int Height { get; private set; }

    /// <summary>Pixels on the CPU path (top row first: the renderer draws upside down).</summary>
    public SKBitmap? Bitmap { get; private set; }

    /// <summary>Measurement: bytes of this view's GPU target.</summary>
    public long GpuBytes => (long)Width * Height * 4;

    /// <summary>Measurement: bytes of this view's CPU buffer.</summary>
    public long CpuBytes => Bitmap is null ? 0 : (long)Width * Height * 4;

    /// <summary>
    /// Prepares the target for this draw, making it again when the context, the GRContext or the size changed. <paramref name="context"/> must be current.
    /// </summary>
    public void Prepare(Window* context, GRContext? gr, int w, int h)
    {
        if (context == Context && ReferenceEquals(gr, Gr) && w == Width && h == Height && Fbo != 0)
            return;

        var gpu = gr is not null;
        if (context != Context || w != Width || h != Height || gpu != (_texture != 0))
        {
            Release();
            (Fbo, _texture, _renderbuffer) = SceneRenderer.CreateTarget(gpu, w, h);
            Context = context;
            Width = w;
            Height = h;
            if (!gpu)
                Bitmap = new SKBitmap(new SKImageInfo(w, h, SKColorType.Rgba8888, SKAlphaType.Premul));
        }

        // GRContext 만 바뀐 경우(호스트가 GPU 모드를 다시 켬)는 GL 텍스처는 그대로 두고 감싼 이미지만 다시 만든다
        _image?.Dispose();
        _image = null;
        Gr = gr;
        if (gr is not null)
        {
            var texture = new GRBackendTexture(w, h, false, new GRGlTextureInfo(GlTexture2D, (uint)_texture, GlRgba8));
            _image = SKImage.FromTexture(gr, texture, GRSurfaceOrigin.BottomLeft, SKColorType.Rgba8888, SKAlphaType.Premul)
                     ?? throw new InvalidOperationException("SKImage.FromTexture returned null");
        }
    }

    /// <summary>Puts the last drawn result on the canvas at <paramref name="bounds"/>.</summary>
    public void Draw(SKCanvas canvas, SKRect bounds, SKPaint paint)
    {
        var sampling = new SKSamplingOptions(SKFilterMode.Linear);
        if (_image is not null)
        {
            canvas.DrawImage(_image, bounds, sampling, paint);
        }
        else if (Bitmap is not null)
        {
            // 이미지는 비트맵 메모리를 감싼 것(복사 없음). 읽을 때마다 새로 감싸서 GPU 캔버스의 업로드 캐시가 옛 픽셀을 쓰지 않게 한다
            _pixels ??= SKImage.FromPixels(Bitmap.PeekPixels());
            canvas.DrawImage(_pixels, bounds, sampling, paint);
        }
    }

    /// <summary>CPU path: new pixels were read into the bitmap.</summary>
    public void PixelsChanged()
    {
        _pixels?.Dispose();
        _pixels = null;
    }

    /// <summary>When the renderer last drew into this target (Environment.TickCount64).</summary>
    public long LastUsed { get; set; }

    /// <summary>Frees the GL objects and the bitmap; the next draw makes them again.</summary>
    public void Release()
    {
        PixelsChanged();
        _image?.Dispose();
        _image = null;
        Bitmap?.Dispose();
        Bitmap = null;
        if (Fbo != 0 && Context != null)
        {
            SceneRenderer.Delete(Context, Fbo, _texture, _renderbuffer);
            SceneRenderer.ResetSkia(Gr); // 지운 텍스처가 묶여 있었을 수 있다
        }
        Fbo = _texture = _renderbuffer = 0;
        Context = null;
        Gr = null;
    }
}
