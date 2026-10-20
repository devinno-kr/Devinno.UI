using Devinno.UI;
using Devinno.UI.Editing;
using Devinno.UI.Styling;
using SkiaSharp;
using Xunit;

[Trait("Category", "Sample")] // PNG 다시 찍기 — 기본 실행 제외, `dotnet test -p:RunSamples=true --filter Category=Sample`
public class P8aKeyboardSamples
{
    private const float W = 800, H = 480;

    private const string Markup = """
        <DvPageFrame title="레시피 편집" subtitle="입력을 누르면 가상 키보드가 올라옵니다">
          <DvGrid columns="2">
            <DvCard title="레시피">
              <DvStack gap="sm">
                <DvLabel class="label" text="이름"/>
                <DvInput value="{Bind Data.Name}" placeholder="레시피 이름"/>
              </DvStack>
            </DvCard>
            <DvCard title="설정값">
              <DvStack gap="sm">
                <DvLabel class="label" text="온도"/>
                <DvInput value="{Bind Data.Temp}" keyboard-type="numeric" min="0" max="120" unit="℃"/>
              </DvStack>
            </DvCard>
          </DvGrid>
          <DvCard title="메모" style="margin-top:16">
            <DvInput value="{Bind Data.Memo}" placeholder="메모"/>
          </DvCard>
        </DvPageFrame>
        """;

    public sealed class Vm
    {
        public string Name { get; set; } = "";
        public double Temp { get; set; } = 62.5;
        public string Memo { get; set; } = "";
    }

    private sealed class Rig
    {
        public long Now = 1000;
        public readonly DvSurface S;

        public Rig(DvTheme theme)
        {
            S = new DvSurface(Markup, new Vm(), theme) { Clock = () => Now };
            Render();
        }

        public SKBitmap Render()
        {
            var bmp = new SKBitmap((int)W, (int)H);
            using var c = new SKCanvas(bmp);
            S.RenderTo(c, W, H);
            return bmp;
        }

        public void Click(float x, float y)
        {
            S.PointerDown(new SKPoint(x, y));
            S.PointerUp(new SKPoint(x, y));
            Render().Dispose();
        }

        public void EditAt(float x, float y)
        {
            Click(x, y);
            Now += SoftKeyboard.AnimMs + 10;
            Render().Dispose();
        }

        public void Tap(string id)
        {
            var k = S.Keyboard!.Keys(W, H, Now).First(k => k.Id == id);
            Click(k.Rect.MidX, k.Rect.MidY);
        }

        public void Save(string name)
        {
            using var bmp = Render();
            var path = Path.Combine(ReportsDir(), name);
            using var fs = File.Create(path);
            bmp.Encode(fs, SKEncodedImageFormat.Png, 100);
        }
    }

    private static (float X, float Y) InputCenter(DvSurface s, int index)
    {
        var inputs = Flatten(s.Root!).Where(b => b.Node.Tag == "DvInput").ToList();
        var r = inputs[index].Rect;
        return (r.MidX, r.MidY);
    }

    private static IEnumerable<Devinno.UI.Layout.LayoutBox> Flatten(Devinno.UI.Layout.LayoutBox b)
        => new[] { b }.Concat(b.Children.SelectMany(Flatten));

    [Theory]
    [InlineData("light")]
    [InlineData("dark")]
    public void Render_keyboards(string themeName)
    {
        var theme = themeName == "light" ? DvTheme.Light : DvTheme.Dark;

        // 한글 + 조합 중
        var r = new Rig(theme);
        var (x, y) = InputCenter(r.S, 0);
        r.EditAt(x, y);
        foreach (var k in new[] { "ㅅ", "ㅔ", "ㅊ", "ㅓ", "ㄱ" })
            r.Tap(k);
        r.Save($"phase8a-kbd-hangul-{themeName}.png");

        // 영문 Shift
        r.Tap(SoftKeyboard.Lang);
        r.Tap(SoftKeyboard.Shift);
        r.Save($"phase8a-kbd-english-{themeName}.png");

        // 기호
        r.Tap(SoftKeyboard.Sym);
        r.Save($"phase8a-kbd-symbols-{themeName}.png");

        // 숫자 키패드 + 범위 초과 거절
        var n = new Rig(theme);
        (x, y) = InputCenter(n.S, 1);
        n.EditAt(x, y);
        n.Tap(SoftKeyboard.Clear);
        foreach (var k in new[] { "1", "5", "0" })
            n.Tap(k);
        n.Tap(SoftKeyboard.Done);
        n.Save($"phase8a-kbd-numeric-{themeName}.png");

        // 아래쪽 입력 → 화면이 올라감(pan)
        var p = new Rig(theme);
        (x, y) = InputCenter(p.S, 2);
        p.EditAt(x, y);
        p.Save($"phase8a-kbd-pan-{themeName}.png");
        Assert.True(p.S.KeyboardPan > 0);
    }

    private static string ReportsDir()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var marker = Path.Combine(dir.FullName, "docs", "superpowers");
            if (Directory.Exists(marker))
            {
                var reports = Path.Combine(marker, "reports");
                Directory.CreateDirectory(reports);
                return reports;
            }

            dir = dir.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate docs/superpowers from test output.");
    }
}
