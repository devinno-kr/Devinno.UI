using OpenTK.Graphics.OpenGL4;
using OpenTK.Windowing.GraphicsLibraryFramework;
using SkiaSharp;

namespace Devinno.UI.Scene3D;

/// <summary>
/// 뷰 하나가 그린 결과. 뷰마다 따로 둔다 — GPU 길에서는 Skia 가 프레임 끝에 이 텍스처를 읽으므로 뷰끼리 함께 쓸 수 없다.
/// <list type="bullet">
/// <item>GPU 길: 호스트 컨텍스트의 텍스처 + 그것을 감싼 <see cref="SKImage"/>(복사 없음).</item>
/// <item>CPU 길: 숨은 컨텍스트의 렌더버퍼 + 픽셀을 받는 <see cref="SKBitmap"/>(크기가 같으면 다시 쓴다).</item>
/// </list>
/// </summary>
internal sealed unsafe class SceneTarget
{
    private const uint GlTexture2D = 0x0DE1, GlRgba8 = 0x8058;

    private int _texture, _renderbuffer;
    private SKImage? _image;
    private SKImage? _pixels;

    /// <summary>그린 컨텍스트(GLFW 창). 아직 그리지 않았으면 null.</summary>
    public Window* Context { get; private set; }

    /// <summary>GPU 길이면 호스트의 GRContext.</summary>
    public GRContext? Gr { get; private set; }

    public int Fbo { get; private set; }
    public int Width { get; private set; }
    public int Height { get; private set; }

    /// <summary>CPU 길의 픽셀 (렌더러가 위아래를 뒤집어 그리므로 위 줄부터).</summary>
    public SKBitmap? Bitmap { get; private set; }

    /// <summary>측정용: 이 뷰가 잡은 GPU 대상 바이트 수.</summary>
    public long GpuBytes => (long)Width * Height * 4;

    /// <summary>측정용: 이 뷰가 잡은 CPU 버퍼 바이트 수.</summary>
    public long CpuBytes => Bitmap is null ? 0 : (long)Width * Height * 4;

    /// <summary>
    /// 이번 그리기에 맞는 대상을 준비한다. 컨텍스트·GRContext·크기가 바뀌었으면 다시 만든다. <paramref name="context"/> 가 현재 컨텍스트여야 한다.
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

    /// <summary>마지막으로 그린 결과를 캔버스의 <paramref name="bounds"/> 에 놓는다.</summary>
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

    /// <summary>CPU 길: 비트맵에 새 픽셀을 읽었다.</summary>
    public void PixelsChanged()
    {
        _pixels?.Dispose();
        _pixels = null;
    }

    private void Release()
    {
        PixelsChanged();
        _image?.Dispose();
        _image = null;
        Bitmap?.Dispose();
        Bitmap = null;
        if (Fbo != 0 && Context != null)
            SceneRenderer.Delete(Context, Fbo, _texture, _renderbuffer);
        Fbo = _texture = _renderbuffer = 0;
        Context = null;
        Gr = null;
    }
}
