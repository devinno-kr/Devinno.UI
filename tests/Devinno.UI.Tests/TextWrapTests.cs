using Devinno.UI.Layout;
using Devinno.UI.Markup;
using Devinno.UI.Painting;
using Devinno.UI.Text;
using SkiaSharp;
using Xunit;

// 글자 줄바꿈(spec 2026-10-19 §4.1·§8) — 배치된 폭에 맞춰 줄을 나누고 줄 수만큼 높이가 늘어난다
public class TextWrapTests
{
    private const string Long = "The quick brown fox jumps over the lazy dog again and again until the line is long";

    private sealed class Vm
    {
        public string Msg { get; set; } = "";
    }

    private static LayoutBox Lay(string inner, float w = 800, float h = 600, object? vm = null)
        => new LayoutEngine().Layout(MarkupParser.Parse(
            "<DvScreen style=\"padding:0; display:flex; flex-direction:column; align-items:start\">" + inner + "</DvScreen>"), vm, w, h);

    private static LayoutBox Label(LayoutBox root) => LayoutQuery.FindByTag(root, "DvLabel")!;

    private static SKFont Font(string text, float size = 14f) => TextFont.Create(text, size, bold: false);

    private static int Lines(string text, float width)
    {
        using var f = Font(text);
        return TextWrap.Wrap(text, f, width).Count;
    }

    private static float Spacing(string text)
    {
        using var f = Font(text);
        return f.Spacing;
    }

    private static float OneLineWidth(string text)
    {
        using var f = Font(text);
        return f.MeasureText(text);
    }

    // 기존 InputDisplay.Wrap(글자 하나씩 늘리며 재던 O(n²) 구현) — 결과 동일성 기준
    private static List<string> Legacy(string text, SKFont font, float width)
    {
        var lines = new List<string>();
        foreach (var para in text.Replace("\r\n", "\n").Split('\n'))
        {
            var rest = para;
            while (rest.Length > 0 && font.MeasureText(rest) > width)
            {
                var cut = 1;
                while (cut < rest.Length && font.MeasureText(rest.AsSpan(0, cut + 1)) <= width)
                    cut++;
                var space = rest.LastIndexOf(' ', cut - 1);
                if (space > 0)
                    cut = space + 1;
                lines.Add(rest[..cut].TrimEnd());
                rest = rest[cut..];
            }

            lines.Add(rest);
        }

        return lines;
    }

    [Theory]
    [InlineData("hello world this is a wrap test", 60f)]           // 공백 우선
    [InlineData("abcdefghijklmnopqrstuvwxyz0123456789", 50f)]       // 공백 없음 → 글자 단위
    [InlineData("first line\nsecond line is longer", 70f)]          // \n
    [InlineData("a\n\nb", 100f)]                                    // 빈 줄
    [InlineData("word    word    word    word", 40f)]               // 연속 공백
    [InlineData("   leading spaces then words here", 50f)]          // 앞 공백
    [InlineData("windows\r\nnewline", 100f)]
    [InlineData("", 50f)]
    [InlineData("가나다라마바사아자차카타파하 한글도 잘 나뉘어야 한다", 60f)]
    [InlineData("x", 1f)]                                           // 한 글자도 안 들어가는 폭
    [InlineData(Long + " " + Long + " " + Long, 90f)]               // 여러 줄 — 직전 줄 길이로 추정해 찾기
    [InlineData(Long + "\n" + Long, 300f)]
    [InlineData("i i i i i i i i i i i i i i i i WWWWWWWWWWWWWWWWWWWWWWWW", 60f)] // 줄마다 글자 폭이 크게 다름
    [InlineData("café résumé naïve words", 40f)] // 결합 문자
    public void Wrap_SameAsLegacy(string text, float width)
    {
        using var f = Font(text.Length > 0 ? text : " ");
        Assert.Equal(Legacy(text, f, width), TextWrap.Wrap(text, f, width));
    }

    [Theory]
    [InlineData("😀😀😀😀😀😀😀😀", 30f)]
    [InlineData("ab😀cd😀ef😀gh😀ij", 20f)]
    [InlineData("😀", 1f)]
    public void Wrap_NeverSplitsSurrogatePairs(string text, float width)
    {
        using var f = Font(text);
        var lines = TextWrap.Wrap(text, f, width);
        Assert.Equal(text, string.Concat(lines)); // 공백 없는 글 — 글자를 잃거나 더하지 않음
        foreach (var line in lines)
        {
            if (line.Length == 0)
                continue;
            Assert.False(char.IsHighSurrogate(line[^1]), "line ends inside a surrogate pair");
            Assert.False(char.IsLowSurrogate(line[0]), "line starts inside a surrogate pair");
        }
    }

    [Fact]
    public void OverriddenWidth_MeasuresAtStretchWidth()
    {
        // 폭 없는 라벨을 stretch 가 늘림 — 배치 폭(200)으로 재야 그린 줄이 상자 높이 안에 든다
        var root = Lay("<DvBox style=\"width:200; display:flex; flex-direction:column; align-items:stretch\">" +
                       $"<DvLabel text=\"{Long}\" style=\"padding:2 4\"/><DvBox style=\"height:10\"/></DvBox>");
        var col = root.Children[0];
        var label = col.Children[0];
        var n = Lines(Long, 192);
        Assert.True(n >= 2);
        Assert.Equal(200, label.Rect.Width, 3);
        Assert.Equal(n * Spacing(Long) + 4, label.Rect.Height, 2);
        Assert.True(col.Children[1].Rect.Top >= label.Rect.Bottom - 0.01f);
    }

    [Fact]
    public void OverriddenWidth_Nested_MeasuresAtStretchWidth()
    {
        // 폭 없는 stretch 상자 안의 늘어나는 라벨 — 바깥 측정도 가용 폭으로 내려가야 한다
        var root = Lay("<DvBox style=\"width:300; display:flex; flex-direction:column; align-items:stretch\">" +
                       "<DvBox style=\"display:flex; flex-direction:column; align-items:stretch\">" +
                       $"<DvLabel text=\"{Long} {Long}\"/></DvBox><DvBox style=\"height:10\"/></DvBox>");
        var outer = root.Children[0];
        var inner = outer.Children[0];
        var label = inner.Children[0];
        var n = Lines(Long + " " + Long, 300);
        Assert.True(n >= 2);
        Assert.Equal(n * Spacing(Long), label.Rect.Height, 2);
        Assert.True(inner.Rect.Bottom >= label.Rect.Bottom - 0.01f);
        Assert.True(outer.Children[1].Rect.Top >= inner.Rect.Bottom - 0.01f);
    }

    [Fact]
    public void Stretch_keeps_declared_width_like_css()
    {
        // CSS 와 같이 — width 를 준 자식은 stretch 여도 그 폭, max-width 는 늘어난 폭을 자른다
        var root = Lay("<DvBox style=\"width:300; display:flex; flex-direction:column; align-items:stretch\">" +
                       "<DvBox style=\"width:100; height:10\"/><DvBox style=\"max-width:150; height:10\"/><DvBox style=\"height:10\"/></DvBox>");
        var col = root.Children[0];
        Assert.Equal(100, col.Children[0].Rect.Width, 3);
        Assert.Equal(150, col.Children[1].Rect.Width, 3);
        Assert.Equal(300, col.Children[2].Rect.Width, 3);
    }

    [Fact]
    public void FrGrid_NotStretched_RowsFitGridRect()
    {
        // 1fr 1fr, 내용 폭이 고르지 않음 — CSS 처럼 fr 트랙은 가장 큰 (내용/fr) 비율로 같은 폭, 글이 잘리지 않음
        var root = Lay("<DvBox style=\"display:grid; grid-template-columns:1fr 1fr\">" +
                       "<DvLabel text=\"A\"/><DvLabel text=\"a medium length text\"/></DvBox><DvBox style=\"height:10\"/>");
        var grid = root.Children[0];
        foreach (var c in grid.Children)
            Assert.True(c.Rect.Bottom <= grid.Rect.Bottom + 0.01f);
        Assert.Equal(grid.Children[0].Rect.Width, grid.Children[1].Rect.Width, 3);
        Assert.Equal(Spacing("A"), grid.Rect.Height, 3); // 한 줄 그대로
        Assert.True(root.Children[1].Rect.Top >= grid.Rect.Bottom - 0.01f);
    }

    [Fact]
    public void FrGrid_InNarrowColumn_RowsFitGridRect()
    {
        var root = Lay("<DvBox style=\"width:200; display:flex; flex-direction:column\">" +
                       "<DvBox style=\"display:grid; grid-template-columns:1fr 1fr\">" +
                       $"<DvLabel text=\"x\"/><DvLabel text=\"{Long}\"/></DvBox><DvBox style=\"height:10\"/></DvBox>");
        var col = root.Children[0];
        var grid = col.Children[0];
        Assert.True(Lines(Long, 100) >= 2);
        Assert.Equal(Lines(Long, 100) * Spacing(Long), grid.Rect.Height, 2);
        foreach (var c in grid.Children)
            Assert.True(c.Rect.Bottom <= grid.Rect.Bottom + 0.01f);
        Assert.True(col.Children[1].Rect.Top >= grid.Rect.Bottom - 0.01f);
    }

    private const string Mid = "The quick brown fox jumps over the lazy dog again";
    private const string Stretch = "<DvBox style=\"width:800; display:flex; flex-direction:column; align-items:stretch\">";

    [Fact]
    public void PercentGrid_Stretched_MeasuredAtArrangedWidth()
    {
        // 50% 50% 가 열 폭(800)으로 늘어난다 — 측정도 그 폭이어야 한다(자연 폭으로 재면 칸이 좁아 줄이 늘고 grid 가 웃자람)
        var root = Lay(Stretch + "<DvBox style=\"display:grid; grid-template-columns:50% 50%\">" +
                       $"<DvLabel text=\"{Mid}\"/><DvLabel text=\"B\"/></DvBox><DvBox style=\"height:10\"/></DvBox>");
        var col = root.Children[0];
        var grid = col.Children[0];
        Assert.Equal(1, Lines(Mid, 400));
        Assert.Equal(800, grid.Rect.Width, 3);
        Assert.Equal(Spacing(Mid), grid.Children[0].Rect.Height, 3);
        Assert.Equal(Spacing(Mid), grid.Rect.Height, 3); // 빈 공간 없음
        Assert.Equal(grid.Rect.Bottom, col.Children[1].Rect.Top, 2);
    }

    [Fact]
    public void FrGrid_Stretched_WidthCell_MeasuredAtArrangedWidth()
    {
        // 칸의 width:60 은 grid 가 덮어쓴다 — 늘어난 1fr 1fr(400) 칸 폭으로 재야 한다
        var root = Lay(Stretch + "<DvBox style=\"display:grid; grid-template-columns:1fr 1fr\">" +
                       $"<DvLabel text=\"{Mid}\" style=\"width:60\"/><DvLabel text=\"B\" style=\"width:60\"/></DvBox></DvBox>");
        var grid = root.Children[0].Children[0];
        Assert.True(Lines(Mid, 60) >= 2);
        Assert.Equal(400, grid.Children[0].Rect.Width, 3);
        Assert.Equal(Spacing(Mid), grid.Children[0].Rect.Height, 3);
        Assert.Equal(Spacing(Mid), grid.Rect.Height, 3);
    }

    [Fact]
    public void StretchedGrid_SameSize_NoRemeasure()
    {
        // 측정 칸 폭 = 배치 칸 폭 — 같은 크기로 다시 배치하면 칸을 다시 재지 않는다
        var root = Lay(Stretch + "<DvBox style=\"display:grid; grid-template-columns:1fr 1fr\">" +
                       $"<DvLabel text=\"{Mid}\" style=\"width:60\"/><DvLabel text=\"B\"/></DvBox></DvBox>");
        var cell = root.Children[0].Children[0].Children[0];
        Assert.Equal(400, cell.Rect.Width, 3);
        var count = cell.MeasureCount;
        LayoutEngine.Rearrange(root, 800, 600);
        Assert.Equal(count, cell.MeasureCount);
    }

    private static LayoutBox Centered(string inner)
        => new LayoutEngine().Layout(MarkupParser.Parse(
            "<DvScreen style=\"padding:0; display:flex; flex-direction:column; align-items:center\">" + inner + "</DvScreen>"), null, 800, 600);

    private const string AbGrid = "<DvBox style=\"display:grid; grid-template-columns:1fr 1fr\"><DvLabel text=\"A\"/><DvLabel text=\"B\"/></DvBox>";

    [Theory]
    [InlineData("DvStack", 18.0625f)]  // a34c919 값
    [InlineData("DvCard", 66.0605469f)]
    public void StretchedGrid_InShrinkToFitParent_KeepsContentWidth(string tag, float expected)
    {
        // 늘어나는 grid 가 측정 때 보고하는 폭 = min(내용 폭, 가용 폭) — 가운데 정렬된 부모가 창 폭으로 커지지 않는다
        var root = Centered($"<{tag}>{AbGrid}</{tag}>");
        var parent = root.Children[0];
        Assert.Equal(expected, parent.Rect.Width, 2);
        Assert.Equal((800 - expected) / 2, parent.Rect.Left, 2);
    }

    private const string L300 = "Long text label that is about three hundred px wide ok";

    // 가운데 정렬된(내용 폭) 세로 상자 안의 늘어나는 % grid — 상자 폭으로 칸을 다시 재서 높이가 배치와 같다(두 단계 측정)
    [Theory]
    [InlineData("DvStack", "50% 50%", true)]
    [InlineData("DvCard", "50% 50%", true)]
    [InlineData("DvStack", "auto 50%", false)]
    public void PercentGrid_InShrinkToFitColumn_HeightMatchesArrange(string tag, string cols, bool longFirst)
    {
        var cells = longFirst ? $"<DvLabel text=\"{L300}\"/><DvLabel text=\"B\"/>" : $"<DvLabel text=\"X\"/><DvLabel text=\"{L300}\"/>";
        var root = Centered($"<{tag}><DvBox style=\"display:grid; grid-template-columns:{cols}\">{cells}</DvBox><DvLabel text=\"after\"/></{tag}>");
        var parent = root.Children[0];
        var grid = Flatten(parent).First(b => b.Style.IsGrid);
        var after = Flatten(parent).Last(b => b.ResolvedText == "after");
        foreach (var c in grid.Children)
            Assert.True(c.Rect.Bottom <= grid.Rect.Bottom + 0.01f, $"cell {c.Rect} grid {grid.Rect}");
        Assert.True(after.Rect.Top >= grid.Rect.Bottom - 0.01f, $"after {after.Rect} grid {grid.Rect}");
        Assert.Equal(after.Rect.Bottom + parent.Style.Padding.Bottom, parent.Rect.Bottom, 2);
        Assert.True(parent.Rect.Width < 800, $"parent width {parent.Rect.Width}");
        Assert.Equal((800 - parent.Rect.Width) / 2, parent.Rect.Left, 2);
    }

    [Fact]
    public void PercentGrid_InMinWidthColumn_HeightMatchesArrange()
    {
        // min-width(500)가 내용 폭보다 크면 상자는 500 — 두 번째 단계도 그 폭으로 재야 빈 공간이 없다
        var root = Centered($"<DvStack style=\"min-width:500\"><DvBox style=\"display:grid; grid-template-columns:50% 50%\"><DvLabel text=\"{L300}\"/><DvLabel text=\"B\"/></DvBox><DvLabel text=\"after\"/></DvStack>");
        var stack = root.Children[0];
        var grid = stack.Children[0];
        var after = stack.Children[1];
        Assert.Equal(500, stack.Rect.Width, 2);
        foreach (var c in grid.Children)
            Assert.Equal(grid.Rect.Bottom, c.Rect.Bottom, 2);
        Assert.Equal(after.Rect.Bottom + stack.Style.Padding.Bottom, stack.Rect.Bottom, 2);
    }

    [Fact]
    public void Column_NoStretchedWrapping_NoExtraMeasure()
    {
        // 줄바꿈할 늘어나는 자식이 없는 세로 상자 — 두 번째 단계가 측정을 늘리지 않는다
        var root = Centered("<DvStack><DvLabel text=\"A\"/><DvLabel text=\"BB\"/><DvBox style=\"width:40; height:10\"/></DvStack>");
        var stack = root.Children[0];
        Assert.Equal(1, stack.MeasureCount);
        foreach (var c in stack.Children)
            Assert.Equal(1, c.MeasureCount);
    }

    private static IEnumerable<LayoutBox> Flatten(LayoutBox b) => new[] { b }.Concat(b.Children.SelectMany(Flatten));

    [Fact]
    public void Absolute_LeftRight_WrapsToThatWidth()
    {
        // left+right 로 폭이 정해진 absolute — 그 폭(200 − 10 − 10)으로 줄바꿈해 높이를 잰다
        var root = Lay("<DvBox style=\"width:200; height:300\">" +
                       $"<DvLabel text=\"{Long}\" style=\"position:absolute; left:10; right:10; top:0\"/></DvBox>");
        var label = Label(root);
        var n = Lines(Long, 180);
        Assert.True(n >= 2);
        Assert.Equal(180, label.Rect.Width, 3);
        Assert.Equal(n * Spacing(Long), label.Rect.Height, 2);
    }

    [Fact]
    public void WhiteSpace_ValueIsCaseSensitive()
    {
        // 값은 키에 따라 대소문자 구분(StyleParser 규칙) — text-align·content-align 과 같게 소문자만
        Assert.True(Devinno.UI.Styling.StyleParser.Parse("white-space:nowrap").NoWrap);
        Assert.False(Devinno.UI.Styling.StyleParser.Parse("white-space:NoWrap").NoWrap);
    }

    [Fact]
    public void Label_InColumn_WrapsToColumnWidth()
    {
        var root = Lay("<DvBox style=\"width:200; display:flex; flex-direction:column\">" +
                       $"<DvLabel text=\"{Long}\" style=\"padding:4 6\"/></DvBox>");
        var label = Label(root);
        var n = Lines(Long, 200 - 12);
        Assert.True(n >= 2);
        Assert.Equal(n * Spacing(Long) + 8, label.Rect.Height, 2);
        Assert.True(label.Rect.Width <= 200.01f);
    }

    [Fact]
    public void Label_FitsOneLine_Unchanged()
    {
        const string text = "Short label";
        var label = Label(Lay($"<DvLabel text=\"{text}\" style=\"padding:4 6\"/>"));
        Assert.Equal(OneLineWidth(text) + 12, label.Rect.Width, 3);
        Assert.Equal(Spacing(text) + 8, label.Rect.Height, 3);
    }

    [Fact]
    public void Label_Nowrap_OneLine()
    {
        var root = Lay("<DvBox style=\"width:200; display:flex; flex-direction:column\">" +
                       $"<DvLabel text=\"{Long}\" style=\"white-space:nowrap\"/></DvBox>");
        var label = Label(root);
        Assert.Equal(Spacing(Long), label.Rect.Height, 3);
        Assert.Equal(200, label.Rect.Width, 3); // CSS 와 같이 상자는 부모 폭(stretch), 글자는 한 줄로 넘쳐 그려짐
    }

    [Fact]
    public void WhiteSpace_Inherited()
    {
        var root = Lay("<DvBox style=\"width:200; display:flex; flex-direction:column; white-space:nowrap\">" +
                       $"<DvBox><DvLabel text=\"{Long}\"/></DvBox></DvBox>");
        Assert.Equal(Spacing(Long), Label(root).Rect.Height, 3);
    }

    [Fact]
    public void Label_Newline_AlwaysBreaks()
    {
        var vm = new Vm { Msg = "first\nsecond" };
        var root = Lay("<DvLabel text=\"{Msg}\" style=\"white-space:nowrap\"/>", vm: vm);
        Assert.Equal(2 * Spacing(vm.Msg), Label(root).Rect.Height, 3);
        Assert.Equal(MathF.Max(OneLineWidth("first"), OneLineWidth("second")), Label(root).Rect.Width, 3);
    }

    [Fact]
    public void Label_InRow_StaysOneLine()
    {
        var root = Lay($"<DvRow style=\"width:200\"><DvLabel text=\"{Long}\"/></DvRow>");
        Assert.Equal(Spacing(Long), Label(root).Rect.Height, 3);
    }

    [Fact]
    public void Label_FixedWidth_WrapsAtThatWidth()
    {
        var root = Lay($"<DvRow><DvLabel text=\"{Long}\" style=\"width:150; padding:0 5\"/></DvRow>");
        var label = Label(root);
        var n = Lines(Long, 140);
        Assert.True(n >= 2);
        Assert.Equal(150, label.Rect.Width, 3);
        Assert.Equal(n * Spacing(Long), label.Rect.Height, 2);
    }

    [Fact]
    public void GrowBasisWidth_IsNotWrapWidth()
    {
        // width:0; flex-grow:1 (같은 폭 열 관용구) — 0 은 최종 폭이 아니므로 0 폭으로 줄바꿈하지 않는다(변경 전과 같은 크기)
        var root = Lay("<DvRow style=\"width:400; align-items:stretch\">" +
                       "<DvBox style=\"width:0; flex-grow:1; display:flex; flex-direction:column\"><DvLabel text=\"LINE 1\"/></DvBox>" +
                       "<DvBox style=\"width:0; flex-grow:1; display:flex; flex-direction:column\"><DvLabel text=\"LINE 2\"/></DvBox></DvRow>");
        var row = root.Children[0];
        Assert.Equal(Spacing("LINE 1"), row.Rect.Height, 3);
        Assert.Equal(Spacing("LINE 1"), Label(root).Rect.Height, 3);
    }

    [Fact]
    public void Label_InGridCell_WrapsToColumn()
    {
        var root = Lay("<DvBox style=\"display:grid; grid-template-columns:120 1fr; width:400\">" +
                       $"<DvLabel text=\"{Long}\"/><DvLabel text=\"B\"/></DvBox>");
        var grid = root.Children[0];
        var n = Lines(Long, 120);
        Assert.True(n >= 2);
        Assert.Equal(n * Spacing(Long), grid.Children[0].Rect.Height, 2);
        Assert.Equal(n * Spacing(Long), grid.Rect.Height, 2); // 행 높이에 반영
    }

    [Fact]
    public void Label_WithIcon_OneLine()
    {
        var root = Lay("<DvBox style=\"width:200; display:flex; flex-direction:column\">" +
                       $"<DvButton icon=\"fa-gear\" text=\"{Long}\"/></DvBox>");
        var btn = LayoutQuery.FindByTag(root, "DvButton")!;
        Assert.Equal(MathF.Max(Spacing(Long), 14f), btn.Rect.Height, 3);
    }

    [Fact]
    public void Resize_RewrapsEachTime()
    {
        var root = Lay($"<DvLabel text=\"{Long}\"/>");
        var label = Label(root);
        foreach (var w in new[] { 800f, 300f, 800f, 300f })
        {
            LayoutEngine.Rearrange(root, w, 600);
            Assert.Equal(Lines(Long, w) * Spacing(Long), label.Rect.Height, 2);
        }

        Assert.Equal(1, Lines(Long, 800));
        Assert.True(Lines(Long, 300) >= 2);
    }

    [Fact]
    public void ScrollGutter_RemeasuresAtNarrowerWidth()
    {
        var root = Lay("<DvBox style=\"width:300; height:100; overflow:scroll; display:flex; flex-direction:column; align-items:stretch\">" +
                       $"<DvLabel text=\"{Long} {Long}\"/><DvBox style=\"height:200\"/></DvBox>");
        var sc = root.Children[0];
        var label = Label(root);
        Assert.True(sc.MaxScrollY > 0);
        var inner = 300 - Devinno.UI.Interaction.ScrollBars.Gutter;
        var n = Lines(Long + " " + Long, inner);
        Assert.Equal(n * Spacing(Long), label.Rect.Height, 2);
        Assert.True(sc.ContentHeight >= n * Spacing(Long) + 200 - 0.01f); // 마지막 줄 잘림 없음
    }

    [Fact]
    public void SameWidth_NoRemeasure()
    {
        var root = Lay("<DvBox style=\"width:200; display:flex; flex-direction:column\">" +
                       $"<DvLabel text=\"{Long}\"/></DvBox>");
        var label = Label(root);
        var count = label.MeasureCount;
        Assert.True(count > 0);

        LayoutEngine.Rearrange(root, 800, 600);
        Assert.Equal(count, label.MeasureCount);

        LayoutEngine.MeasureBox(label, 200);
        Assert.Equal(count, label.MeasureCount);
    }

    [Fact]
    public void Paint_MultiLine_AlignsAndUsesSameFont()
    {
        var root = Lay("<DvBox style=\"width:200; display:flex; flex-direction:column; align-items:stretch\">" +
                       $"<DvLabel text=\"{Long}\" style=\"text-align:center; color:#000000; font-family:Arial\"/></DvBox>", 400, 300);
        var label = Label(root);
        using var f = TextFont.Create(Long, 14f, bold: false, "Arial");
        var lines = TextWrap.Wrap(Long, f, 200);
        Assert.True(lines.Count >= 2);
        Assert.Equal(lines.Count * f.Spacing, label.Rect.Height, 2);

        using var bmp = new SKBitmap(400, 300);
        using (var canvas = new SKCanvas(bmp))
        {
            canvas.Clear(SKColors.White);
            new Painter(null).Paint(canvas, root, null);
        }

        // 줄마다 그 줄 높이 띠 안에 글자 픽셀이 있고, 가운데 정렬이면 잉크 가운데가 상자 가운데
        for (var i = 0; i < lines.Count; i++)
        {
            var top = (int)(label.Rect.Top + i * f.Spacing);
            var bottom = (int)(label.Rect.Top + (i + 1) * f.Spacing);
            int minX = int.MaxValue, maxX = -1;
            for (var y = top; y < bottom; y++)
            for (var x = 0; x < 400; x++)
            {
                if (bmp.GetPixel(x, y).Red < 128)
                {
                    minX = Math.Min(minX, x);
                    maxX = Math.Max(maxX, x);
                }
            }

            Assert.True(maxX >= 0, $"line {i} has no ink");
            Assert.InRange((minX + maxX) / 2f, label.Rect.MidX - 3, label.Rect.MidX + 3);
            Assert.True(maxX - minX <= 200);
        }
    }
}
