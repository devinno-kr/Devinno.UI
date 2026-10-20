using Devinno.UI;
using Devinno.UI.Layout;
using Devinno.UI.Markup;
using Devinno.UI.Painting;
using Devinno.UI.Styling;
using SkiaSharp;
using Xunit;

[Trait("Category", "Sample")] // PNG 다시 찍기 — 기본 실행 제외, `dotnet test -p:RunSamples=true --filter Category=Sample`
public class P8aInputSamples
{
    public const string Markup = """
        <DvPageFrame title="입력" subtitle="DvInput — 텍스트 · 숫자 · 비밀번호 · 여러 줄">
          <DvGrid columns="2">
            <DvCard title="레시피">
              <DvStack gap="sm">
                <DvLabel class="label" text="이름"/>
                <DvInput value="{Bind Data.Name}" placeholder="레시피 이름"/>
                <DvLabel class="label" text="설명 (비어 있음)"/>
                <DvInput value="{Bind Data.Empty}" placeholder="설명을 입력하세요"/>
                <DvLabel class="label" text="메모 (여러 줄)"/>
                <DvInput value="{Bind Data.Memo}" multiline="true"/>
              </DvStack>
            </DvCard>
            <DvCard title="설정값">
              <DvStack gap="sm">
                <DvLabel class="label" text="온도 (편집 중)"/>
                <DvInput id="editing" value="{Bind Data.Temp}" keyboard-type="numeric" format="0.0" unit="℃"/>
                <DvLabel class="label" text="속도 (거절됨)"/>
                <DvInput id="error" value="{Bind Data.Speed}" keyboard-type="numeric" min="0" max="3600" unit="rpm"/>
                <DvLabel class="label" text="비밀번호"/>
                <DvInput value="{Bind Data.Password}" keyboard-type="password"/>
                <DvLabel class="label" text="읽기 전용 · 비활성"/>
                <DvRow>
                  <DvInput value="{Data.Name}" style="width:136"/>
                  <DvInput value="{Bind Data.Name}" disabled="true" style="width:136"/>
                </DvRow>
              </DvStack>
            </DvCard>
          </DvGrid>
        </DvPageFrame>
        """;

    public sealed class Vm
    {
        public string Name { get; set; } = "세척 공정 B";
        public string Empty { get; set; } = "";
        public string Memo { get; set; } = "1단계 예열 후 세척액 투입.\n온도가 60℃ 를 넘으면 펌프 속도를 낮춘다.";
        public double Temp { get; set; } = 62.456;
        public int Speed { get; set; } = 4200;
        public string Password { get; set; } = "1234";
    }

    [Theory]
    [InlineData("light")]
    [InlineData("dark")]
    public void Render_inputs(string themeName)
    {
        var theme = themeName == "light" ? DvTheme.Light : DvTheme.Dark;
        const int w = 760, h = 460;
        var root = new LayoutEngine(theme).Layout(MarkupParser.Parse(Markup), new Vm(), w, h);
        foreach (var b in Flatten(root))
        {
            if (b.Node.Attributes.GetValueOrDefault("id") == "editing")
                b.Editing = true;
            if (b.Node.Attributes.GetValueOrDefault("id") == "error")
                b.EditError = "0 ~ 3600";
        }

        using var bmp = new SKBitmap(w, h);
        using (var canvas = new SKCanvas(bmp))
        {
            canvas.Clear(SKColors.White);
            new Painter(theme).Paint(canvas, root, null);
        }

        var path = Path.Combine(ReportsDir(), $"phase8a-input-{themeName}.png");
        using (var fs = File.Create(path))
            bmp.Encode(fs, SKEncodedImageFormat.Png, 100);
        Assert.True(new FileInfo(path).Length > 1000);
    }

    private static IEnumerable<LayoutBox> Flatten(LayoutBox b) => new[] { b }.Concat(b.Children.SelectMany(Flatten));

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
