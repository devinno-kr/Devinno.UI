using Devinno.UI.Widgets;
using SkiaSharp;
using Xunit;

[CollectionDefinition("ImageCache", DisableParallelization = true)]
public class ImageCacheCollection { }

// 이미지 캐시는 앱 전체가 공유 — 상한·시계를 바꾸는 테스트는 다른 테스트와 동시에 돌지 않게
[Collection("ImageCache")]
public class ImageCacheTests
{
    private static string TempPng()
        => Path.Combine(Path.GetTempPath(), $"dvui-imgcache-{Guid.NewGuid():N}.png");

    private static void WritePng(string path)
    {
        using var bmp = new SKBitmap(4, 4);
        bmp.Erase(SKColors.Red);
        using var data = bmp.Encode(SKEncodedImageFormat.Png, 100);
        File.WriteAllBytes(path, data.ToArray());
    }

    [Fact]
    public void Missing_file_is_retried_after_a_short_while_not_cached_forever()
    {
        var path = TempPng();
        var now = 1_000_000L;
        var old = ImageCache.Clock;
        ImageCache.Clock = () => now;
        try
        {
            Assert.Null(ImageCache.Get(path));

            WritePng(path);
            Assert.Null(ImageCache.Get(path)); // 바로 다음 프레임 — 디스크를 다시 보지 않는다

            now += ImageCache.RetryMs;
            var image = ImageCache.Get(path); // 잠시 뒤 다시 시도 — 이제 보인다
            Assert.NotNull(image);
            Assert.Same(image, ImageCache.Get(path)); // 성공은 계속 캐시
        }
        finally
        {
            ImageCache.Clock = old;
            File.Delete(path);
        }
    }

    [Fact]
    public void Locked_file_returns_null_without_throwing()
    {
        var path = TempPng();
        WritePng(path);
        try
        {
            using (new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                Assert.Null(ImageCache.Get(path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Concurrent_gets_are_safe()
    {
        var path = TempPng();
        WritePng(path);
        try
        {
            var results = new SKImage?[64];
            Parallel.For(0, results.Length, i => results[i] = ImageCache.Get(i % 2 == 0 ? path : path + ".missing"));
            var first = results[0];
            Assert.NotNull(first);
            Assert.All(results.Where((_, i) => i % 2 == 0), r => Assert.Same(first, r));
        }
        finally
        {
            File.Delete(path);
        }
    }

    // ── 상한(P7-2) — 오래 안 쓴 그림부터, 최근에 쓴 그림은 남김, 뺀 그림은 몇 초 뒤 해제 ──

    private static string[] Pngs(int n, int size = 16)
    {
        var paths = new string[n];
        for (var i = 0; i < n; i++)
        {
            paths[i] = TempPng();
            using var bmp = new SKBitmap(size, size);
            bmp.Erase(new SKColor((byte)(i * 40), 0, 0));
            using var data = bmp.Encode(SKEncodedImageFormat.Png, 100);
            File.WriteAllBytes(paths[i], data.ToArray());
        }
        return paths;
    }

    private static void WithLimits(long bytes, int count, Action<Func<long>, Action<long>> body)
    {
        var (oldClock, oldBytes, oldCount) = (ImageCache.Clock, ImageCache.MaxBytes, ImageCache.MaxCount);
        // 앞 테스트가 남긴 그림은 가짜 시계 기준 사용 시각이 제각각이라 빼는 순서·수를 흔든다(테스트 순서는 기계마다 다르다)
        ImageCache.Clear();
        long now = 1_000_000;
        ImageCache.Clock = () => now;
        ImageCache.MaxBytes = bytes;
        ImageCache.MaxCount = count;
        try { body(() => now, t => now = t); }
        finally
        {
            ImageCache.Clock = oldClock;
            ImageCache.MaxBytes = oldBytes;
            ImageCache.MaxCount = oldCount;
        }
    }

    [Fact]
    public void Count_limit_evicts_least_recently_used_and_reloads_on_demand()
    {
        var paths = Pngs(5);
        WithLimits(long.MaxValue, 3, (now, set) =>
        {
            var before = ImageCache.Stats.Count;
            var first = ImageCache.Get(paths[0]);
            for (var i = 1; i < 5; i++)
            {
                set(now() + ImageCache.InUseMs + 1); // 앞의 것은 '최근에 쓴 것' 이 아니게
                Assert.NotNull(ImageCache.Get(paths[i]));
            }
            Assert.True(ImageCache.Stats.Count - before <= 3);
            set(now() + ImageCache.InUseMs + 1);
            var again = ImageCache.Get(paths[0]); // 빠졌던 경로 — 다시 읽는다
            Assert.NotNull(again);
            Assert.NotSame(first, again);
        });
    }

    [Fact]
    public void Recently_used_images_are_kept_even_over_the_limit()
    {
        var paths = Pngs(4);
        WithLimits(long.MaxValue, 1, (now, set) =>
        {
            var images = paths.Select(ImageCache.Get).ToArray(); // 같은 시각 = 모두 지금 보이는 그림
            foreach (var (p, img) in paths.Zip(images))
                Assert.Same(img, ImageCache.Get(p)); // 매 프레임 다시 읽지 않는다
        });
    }

    [Fact]
    public void Byte_limit_counts_decoded_pixels()
    {
        var paths = Pngs(4, size: 64); // 64×64×4 = 16KB
        WithLimits(40 * 1024, int.MaxValue, (now, set) =>
        {
            foreach (var p in paths)
            {
                set(now() + ImageCache.InUseMs + 1);
                ImageCache.Get(p);
            }
            Assert.True(ImageCache.Stats.Bytes <= 40 * 1024, $"{ImageCache.Stats.Bytes} 바이트"); // 오래 안 쓴 것은 모두 빠지고 최근 것만
        });
    }

    [Fact]
    public void Evicted_images_are_disposed_only_after_a_delay()
    {
        var paths = Pngs(3);
        WithLimits(long.MaxValue, 1, (now, set) =>
        {
            var a = ImageCache.Get(paths[0])!;
            set(now() + ImageCache.InUseMs + 1);
            ImageCache.Get(paths[1]); // a 가 빠진다(아직 해제 안 됨 — 다른 화면이 그리는 중일 수 있다)
            Assert.True(ImageCache.Stats.Retired >= 1);
            using (var bmp = new SKBitmap(8, 8))
            using (var c = new SKCanvas(bmp))
                c.DrawImage(a, 0, 0); // 빠진 직후에도 그릴 수 있다
            set(now() + ImageCache.RetireMs + 1);
            ImageCache.Get(paths[2]);
            Assert.Equal(1, ImageCache.Stats.Retired); // 앞서 뺀 것은 해제, 방금 뺀 것만 남음
        });
    }

    [Fact]
    public void Images_are_decoded_up_front()
    {
        var path = Pngs(1)[0];
        var img = ImageCache.Get(path)!;
        Assert.False(img.IsLazyGenerated);
        Assert.NotNull(img.PeekPixels());
    }
}
