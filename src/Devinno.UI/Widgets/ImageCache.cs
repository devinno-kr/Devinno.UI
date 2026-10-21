using System.Diagnostics;
using SkiaSharp;

namespace Devinno.UI.Widgets;

/// <summary>
/// A cache of <see cref="SKImage"/> objects read and decoded from file paths. Used when painting the <c>src</c> image of a <c>DvImage</c>.
/// </summary>
/// <remarks>
/// <para>This is an internal engine stage. App code normally does not use it directly; it uses <c>&lt;DvImage src="…"/&gt;</c> in markup. Custom widgets may use it to share the same images.</para>
/// <para>For each path string (case-sensitive), the file is read and decoded to pixels once, on the first successful load, and the same <see cref="SKImage"/> is returned afterwards. Changing the file after a successful load does not cause a re-read.</para>
/// <para><b>Limits</b>: when the decoded pixels exceed about 128MB in total or 256 images, the least recently used images are evicted from the cache first (so memory does not pile up on a 24-hour screen whose image paths keep changing).
/// Images used within roughly the last 2 seconds (those visible on screen now) are not evicted, so if the visible images are larger than the limit, the limit can be exceeded during that time.
/// Evicted images are not released immediately but a few seconds later (another surface thread may still be painting them). Using an evicted path again re-reads the file.</para>
/// <para>A missing file or a read/decode failure is remembered only briefly (about 2 seconds). During that time the disk is not checked again (to avoid disk I/O every frame) and <see langword="null"/> is returned;
/// a call after that tries reading again. So a file created later is also shown shortly afterwards. Read errors (no permission, locked file, etc.) do not throw;
/// a <see cref="Trace"/> warning is written once per path.</para>
/// <para>Uses an internal lock, so it can be called from any thread. The returned image is owned by the cache, so do not Dispose it. Do not hold on to it long after painting either (if evicted by the limits, it is released a few seconds later).</para>
/// </remarks>
public static class ImageCache
{
    // 실패를 기억하는 시간 — DvImage 는 매 프레임 Get 을 부르므로, 없는 파일을 프레임마다 확인하지 않게 한다
    internal const int RetryMs = 2000;

    // 상한 — 디코드한 픽셀 바이트 합계·장수. 최근 InUseMs 안에 쓴 그림은 빼지 않는다(보이는 그림을 매 프레임 다시 읽지 않게)
    internal static long MaxBytes = 128L * 1024 * 1024;
    internal static int MaxCount = 256;
    internal const int InUseMs = 2000;
    // 뺀 그림을 실제로 해제하기까지 — 다른 스레드의 그리기가 끝나도록
    internal const int RetireMs = 5000;

    // 테스트용 시계
    internal static Func<long> Clock = () => Environment.TickCount64;

    private sealed class Entry(SKImage image, long bytes)
    {
        public SKImage Image { get; } = image;
        public long Bytes { get; } = bytes;
        public long LastUse;
    }

    private static readonly object Gate = new();
    private static readonly Dictionary<string, Entry> Images = new(StringComparer.Ordinal);
    private static readonly Dictionary<string, long> RetryAt = new(StringComparer.Ordinal);
    private static readonly HashSet<string> Warned = new(StringComparer.Ordinal);
    private static readonly List<(SKImage Image, long At)> Retired = new();
    private static long _bytes;

    /// <summary>Number of images in the cache and total decoded pixel bytes (for measurement and tests).</summary>
    internal static (int Count, long Bytes, int Retired) Stats
    {
        get { lock (Gate) return (Images.Count, _bytes, Retired.Count); }
    }

    // 테스트용 — 캐시는 앱 전체가 공유하므로, 상한·시계를 바꾸는 테스트는 앞 테스트가 남긴 그림 없이 시작한다
    internal static void Clear()
    {
        lock (Gate)
        {
            foreach (var e in Images.Values)
                e.Image.Dispose();
            foreach (var (image, _) in Retired)
                image.Dispose();
            Images.Clear();
            Retired.Clear();
            RetryAt.Clear();
            Warned.Clear();
            _bytes = 0;
        }
    }

    /// <summary>Returns the image for the path. If it has not been read yet (or a short time has passed since the last failure, or it was evicted by the limits), reads and decodes the file and adds it to the cache.</summary>
    /// <param name="path">Image file path (relative paths are resolved against the current working directory). Any format Skia can decode, such as PNG or JPEG.</param>
    /// <returns>
    /// The decoded image. <see langword="null"/> if <paramref name="path"/> is empty, the file does not exist, a read error occurs (I/O, permission), or decoding fails
    /// (the calling widget paints an empty box). Does not throw.
    /// </returns>
    public static SKImage? Get(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return null;

        lock (Gate)
        {
            var now = Clock();
            DisposeRetired(now);
            if (Images.TryGetValue(path, out var cached))
            {
                cached.LastUse = now;
                return cached.Image;
            }

            if (RetryAt.TryGetValue(path, out var at) && now < at)
                return null;

            var image = Load(path);
            if (image is null)
            {
                RetryAt[path] = now + RetryMs;
                return null;
            }

            RetryAt.Remove(path);
            Warned.Remove(path);
            var entry = new Entry(image, image.Info.BytesSize64) { LastUse = now };
            Images[path] = entry;
            _bytes += entry.Bytes;
            Trim(now);
            return image;
        }
    }

    // 잠금 안에서만 — 상한을 넘으면 오래 안 쓴 것부터(최근 InUseMs 안에 쓴 것은 남김) 뺀다
    private static void Trim(long now)
    {
        if (_bytes <= MaxBytes && Images.Count <= MaxCount)
            return;
        foreach (var (key, e) in Images.OrderBy(kv => kv.Value.LastUse).ToList())
        {
            if (_bytes <= MaxBytes && Images.Count <= MaxCount)
                break;
            if (now - e.LastUse < InUseMs)
                break; // 이보다 뒤는 모두 최근에 쓴 것
            Images.Remove(key);
            _bytes -= e.Bytes;
            Retired.Add((e.Image, now));
        }
    }

    // 잠금 안에서만
    private static void DisposeRetired(long now)
    {
        for (var i = Retired.Count - 1; i >= 0; i--)
        {
            if (now - Retired[i].At < RetireMs)
                continue;
            Retired[i].Image.Dispose();
            Retired.RemoveAt(i);
        }
    }

    private static SKImage? Load(string path)
    {
        try
        {
            if (!File.Exists(path))
                return null;

            // 픽셀로 바로 디코드 — 미뤄 두면(Skia 기본) 그리는 프레임에서 디코드가 일어나 시간이 튀고, 메모리가 이 캐시 밖(Skia 리소스 캐시)에 생긴다
            using var encoded = SKImage.FromEncodedData(File.ReadAllBytes(path));
            if (encoded is null)
            {
                WarnOnce(path, "image format cannot be decoded");
                return null;
            }

            var raster = encoded.ToRasterImage(true);
            if (raster is null)
                WarnOnce(path, "image format cannot be decoded");
            return raster;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or NotSupportedException or System.Security.SecurityException)
        {
            WarnOnce(path, e.Message);
            return null;
        }
    }

    // 잠금 안에서만
    private static void WarnOnce(string path, string reason)
    {
        if (Warned.Add(path))
            Trace.TraceWarning($"Devinno.UI: DvImage could not load image '{path}': {reason}");
    }
}
