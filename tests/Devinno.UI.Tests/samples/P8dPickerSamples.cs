using Devinno.UI;
using Devinno.UI.Layout;
using Devinno.UI.Styling;
using SkiaSharp;
using Xunit;

[Trait("Category", "Sample")] // PNG 다시 찍기 — 기본 실행 제외, `dotnet test -p:RunSamples=true --filter Category=Sample`
[Collection("FixedPickerClock")]
public class P8dPickerSamples : FixedPickerClockSample
{
    public sealed class Vm
    {
        public DateTime ReserveAt { get; set; } = new(2026, 10, 14, 9, 30, 0);
        public DateOnly Day { get; set; } = new(2026, 10, 14);
        public TimeOnly Start { get; set; } = new(6, 15, 30);
        public DateTime? None { get; set; }
        public string Pen { get; set; } = "#3B82F6";
    }

    private const string Markup = """
        <DvPageFrame title="시간·색 선택" subtitle="DvDateTimeInput · DvColorInput">
          <DvGrid columns="3">
            <DvStack gap="sm">
              <DvLabel class="label" text="예약 시각 (datetime)"/>
              <DvDateTimeInput id="dt" value="{Bind Data.ReserveAt}"/>
              <DvLabel class="label" text="조회일 (date)"/>
              <DvDateTimeInput id="d" value="{Bind Data.Day}"/>
            </DvStack>
            <DvStack gap="sm">
              <DvLabel class="label" text="시작 시각 (time, 초)"/>
              <DvDateTimeInput id="t" value="{Bind Data.Start}" show-seconds="true"/>
              <DvLabel class="label" text="미설정 (placeholder)"/>
              <DvDateTimeInput value="{Bind Data.None}" placeholder="예약 없음"/>
            </DvStack>
            <DvStack gap="sm">
              <DvLabel class="label" text="펜 색"/>
              <DvColorInput id="c" value="{Bind Data.Pen}"/>
              <DvLabel class="label" text="읽기 전용"/>
              <DvColorInput value="{Data.Pen}"/>
            </DvStack>
          </DvGrid>
        </DvPageFrame>
        """;

    private const float W = 900, H = 640;

    private static LayoutBox Root(DvSurface s)
        => (LayoutBox)typeof(DvSurface)
            .GetProperty("Root", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
            .GetValue(s)!;

    private static SKRect Anchor(DvSurface s, string id)
        => Flatten(Root(s)).First(b => b.Node.Attributes.GetValueOrDefault("id") == id).Rect;

    private static void Tap(DvSurface s, SKPoint p)
    {
        s.PointerDown(p);
        s.PointerUp(p);
    }

    private static void Shot(DvTheme theme, string themeName, string name)
    {
        long now = 0;
        var s = new DvSurface(Markup, new Vm(), theme) { Clock = () => now };
        Render(s).Dispose();
        switch (name)
        {
            case "closed":
                break;
            case "datetime":
            {
                var a = Anchor(s, "dt");
                Tap(s, new SKPoint(a.MidX, a.MidY));
                Render(s).Dispose();
                var p = (DateTimePopup)s.TopPopup!;
                for (var i = 0; i < 42; i++)
                {
                    if (p.State.CellDate(i) == new DateOnly(2026, 10, 20))
                    {
                        var r = p.DayRect(i);
                        Tap(s, new SKPoint(r.MidX, r.MidY));
                        break;
                    }
                }
                break;
            }
            case "date":
            {
                var a = Anchor(s, "d");
                Tap(s, new SKPoint(a.MidX, a.MidY));
                break;
            }
            case "time":
            {
                var a = Anchor(s, "t");
                Tap(s, new SKPoint(a.MidX, a.MidY));
                break;
            }
            case "color":
            {
                var a = Anchor(s, "c");
                Tap(s, new SKPoint(a.MidX, a.MidY));
                Render(s).Dispose();
                var p = (ColorPopup)s.TopPopup!;
                var sv = p.SvRect;
                s.PointerDown(new SKPoint(sv.Left + 10, sv.Top + 10));
                s.PointerMove(new SKPoint(sv.MidX, sv.MidY));
                s.PointerUp(new SKPoint(sv.MidX, sv.MidY));
                break;
            }
            case "keypad":
            {
                s.UseNativeEditor = false;
                var a = Anchor(s, "dt");
                Tap(s, new SKPoint(a.MidX, a.MidY));
                Render(s).Dispose();
                var p = (DateTimePopup)s.TopPopup!;
                var f = p.FieldRect(0);
                Tap(s, new SKPoint(f.MidX, f.MidY));
                now = 10_000;
                break;
            }
        }

        using var bmp = Render(s);
        using var fs = File.Create(Path.Combine(ReportsDir(), $"phase8d-{name}-{themeName}.png"));
        bmp.Encode(fs, SKEncodedImageFormat.Png, 100);
    }

    private static SKBitmap Render(DvSurface s)
    {
        var bmp = new SKBitmap((int)W, (int)H);
        using var c = new SKCanvas(bmp);
        s.RenderTo(c, W, H);
        return bmp;
    }

    private static IEnumerable<LayoutBox> Flatten(LayoutBox b)
        => new[] { b }.Concat(b.Children.SelectMany(Flatten));

    [Theory]
    [InlineData("light")]
    [InlineData("dark")]
    public void Render_pickers(string themeName)
    {
        var theme = themeName == "light" ? DvTheme.Light : DvTheme.Dark;
        foreach (var n in new[] { "closed", "datetime", "date", "time", "color", "keypad" })
            Shot(theme, themeName, n);
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
