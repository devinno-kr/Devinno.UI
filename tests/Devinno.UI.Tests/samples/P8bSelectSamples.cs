using Devinno.UI;
using Devinno.UI.Styling;
using SkiaSharp;
using Xunit;

[Trait("Category", "Sample")] // PNG 다시 찍기 — 기본 실행 제외, `dotnet test -p:RunSamples=true --filter Category=Sample`
public class P8bSelectSamples
{
    public enum Direction { 정방향, 역방향, 정지 }

    public sealed class Recipe
    {
        public int Id { get; init; }
        public string Name { get; init; } = "";
    }

    public sealed class Vm
    {
        public string Mode { get; set; } = "manual";
        public int RecipeId { get; set; } = 7;
        public Direction Dir { get; set; } = Direction.역방향;
        public string? Empty { get; set; }
        public List<Recipe> Recipes { get; } =
            Enumerable.Range(1, 14).Select(i => new Recipe { Id = i, Name = $"레시피 {i:00}" }).ToList();
    }

    private const string Markup = """
        <DvPageFrame title="선택" subtitle="DvSelect — DvOption · items · enum">
          <DvGrid columns="3">
            <DvStack gap="sm">
              <DvLabel class="label" text="운전 모드 (DvOption + 아이콘)"/>
              <DvSelect id="mode" value="{Bind Data.Mode}">
                <DvOption value="auto" text="자동" icon="fa-robot"/>
                <DvOption value="manual" text="수동" icon="fa-hand"/>
                <DvOption value="stop" text="정지" icon="fa-stop"/>
              </DvSelect>
              <DvLabel class="label" text="미선택 (placeholder)"/>
              <DvSelect value="{Bind Data.Empty}" placeholder="선택하세요">
                <DvOption value="a" text="A"/>
              </DvSelect>
            </DvStack>
            <DvStack gap="sm">
              <DvLabel class="label" text="레시피 (items, 14개)"/>
              <DvSelect id="recipe" value="{Bind Data.RecipeId}" items="{Data.Recipes}" item-value="Id" item-text="Name"/>
              <DvLabel class="label" text="방향 (enum 자동)"/>
              <DvSelect id="dir" value="{Bind Data.Dir}"/>
            </DvStack>
            <DvStack gap="sm">
              <DvLabel class="label" text="읽기 전용"/>
              <DvSelect value="{Data.Dir}"/>
              <DvLabel class="label" text="비활성"/>
              <DvSelect value="{Bind Data.Dir}" disabled="true"/>
            </DvStack>
          </DvGrid>
          <DvSpacer/>
          <DvRow>
            <DvLabel class="label" text="아래쪽 선택 (위로 열림)"/>
            <DvSelect id="low" value="{Bind Data.Mode}">
              <DvOption value="auto" text="자동" icon="fa-robot"/>
              <DvOption value="manual" text="수동" icon="fa-hand"/>
              <DvOption value="stop" text="정지" icon="fa-stop"/>
            </DvSelect>
          </DvRow>
        </DvPageFrame>
        """;

    private const float W = 900, H = 520;

    private static void Shot(DvTheme theme, string? open, string file)
    {
        var s = new DvSurface(Markup, new Vm(), theme);
        Render(s).Dispose();
        if (open is not null)
        {
            var root = (Devinno.UI.Layout.LayoutBox)typeof(DvSurface)
                .GetProperty("Root", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
                .GetValue(s)!;
            var a = Flatten(root).First(b => b.Node.Attributes.GetValueOrDefault("id") == open).Rect;
            s.PointerDown(new SKPoint(a.MidX, a.MidY));
            s.PointerUp(new SKPoint(a.MidX, a.MidY));
            // 첫 항목 위에 마우스(hover)
            var d = s.Dropdown!;
            var r = d.ItemRect(0);
            s.PointerMove(new SKPoint(r.MidX, r.MidY));
        }

        using var bmp = Render(s);
        using var fs = File.Create(Path.Combine(ReportsDir(), file));
        bmp.Encode(fs, SKEncodedImageFormat.Png, 100);
    }

    private static SKBitmap Render(DvSurface s)
    {
        var bmp = new SKBitmap((int)W, (int)H);
        using var c = new SKCanvas(bmp);
        s.RenderTo(c, W, H);
        return bmp;
    }

    private static IEnumerable<Devinno.UI.Layout.LayoutBox> Flatten(Devinno.UI.Layout.LayoutBox b)
        => new[] { b }.Concat(b.Children.SelectMany(Flatten));

    [Theory]
    [InlineData("light")]
    [InlineData("dark")]
    public void Render_selects(string themeName)
    {
        var theme = themeName == "light" ? DvTheme.Light : DvTheme.Dark;
        Shot(theme, null, $"phase8b-select-closed-{themeName}.png");
        Shot(theme, "mode", $"phase8b-select-open-{themeName}.png");
        Shot(theme, "recipe", $"phase8b-select-scroll-{themeName}.png");
        Shot(theme, "low", $"phase8b-select-up-{themeName}.png");
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
