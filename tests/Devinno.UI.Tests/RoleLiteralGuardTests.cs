using System.Text.RegularExpressions;
using Xunit;

// 역할 토큰 감시(단계 2) — 그리기 코드가 글자 굵기·작은 글자 크기를 숫자로 박지 않는다(테마 역할 Roles.Weight/Roles.Font 로 읽는다).
// 위젯 해부도 안의 비율(반지름 × 0.34 같은 값)과 공정도 자체 표(ProcessLook)는 예외 — 단계 5에서 해부도와 함께 정한다.
public class RoleLiteralGuardTests
{
    private static string Root()
    {
        var dir = AppContext.BaseDirectory;
        while (!File.Exists(Path.Combine(dir, "Devinno.UI.slnx")))
            dir = Path.GetDirectoryName(dir)!;
        return dir;
    }

    private static IEnumerable<(string File, int Line, string Text)> DrawingLines(Func<string, bool> match)
    {
        var src = Path.Combine(Root(), "src", "Devinno.UI");
        var files = Directory.GetFiles(Path.Combine(src, "Painting"), "*.cs")
            .Concat(Directory.GetFiles(Path.Combine(src, "Overlay"), "*.cs"))
            .Append(Path.Combine(src, "Editing", "SoftKeyboard.cs"));
        foreach (var f in files)
        {
            var lines = File.ReadAllLines(f);
            for (var i = 0; i < lines.Length; i++)
            {
                var code = lines[i].Split("//")[0];
                if (match(code))
                    yield return (Path.GetFileName(f), i + 1, code.Trim());
            }
        }
    }

    // 예외: (파일, 줄 안의 글자) — 위젯 해부도 비율의 최소 글자 크기, 키보드 미리보기 등
    private static readonly (string File, string Snippet)[] Allowed =
    [
        ("SoftKeyboard.cs", "18f"),       // 키보드 미리보기 글자(키보드 해부도)
        ("Painter.Grid.cs", "10f"),       // 정렬 순서 숫자(머리글 안 작은 표식)
        ("Painter.Instruments.cs", "MathF.Max(10f"), // 링 값 글자의 최소 크기(링 해부도 비율의 바닥)
    ];

    private static bool IsAllowed((string File, int Line, string Text) l) => Allowed.Any(a => a.File == l.File && l.Text.Contains(a.Snippet));

    [Fact]
    public void Text_weights_come_from_theme_roles()
    {
        var weight = new Regex(@"\b[47]00\b");
        var bad = DrawingLines(c => (c.Contains("TextFont.Create(") || c.Contains("new Slot(") || c.Contains("DrawBannerText(")) && weight.IsMatch(c))
            .Where(l => !IsAllowed(l)).ToList();
        Assert.True(bad.Count == 0, "literal font weights:\n" + string.Join("\n", bad.Select(b => $"{b.File}:{b.Line} {b.Text}")));
    }

    // 선 두께 1 / 1.5 / 2 는 stroke.hairline / outline / emphasis 역할로. 해부도 고유 두께(탱크 테두리 2.5×, 노브 표시선, 슬라이더 손잡이 링 3·4,
    // 단계 연결선 3, 색 고르기 손잡이 5·3, ACK 외곽 1.2)와 공정도 표는 예외 — 단계 3~5에서 생김새와 함께 정한다.
    [Fact]
    public void Common_stroke_widths_come_from_theme_roles()
    {
        var common = new Regex(@"(?<![\w.])(1|1f|1\.5f|2|2f)(?![\w.])");
        var bad = DrawingLines(c => c.Contains("StrokeWidth =") && common.IsMatch(c.Substring(c.IndexOf("StrokeWidth =") + 13)))
            .Where(l => l.File != "Painter.Process.cs").ToList();
        Assert.True(bad.Count == 0, "literal stroke widths:\n" + string.Join("\n", bad.Select(b => $"{b.File}:{b.Line} {b.Text}")));
    }

    // 반투명 단계 0x14 / 0x26 / 0x33 / 0x60 / 0x80 은 overlay.hover / alpha.selected / alpha.soft / alpha.dim / opacity.disabled 역할로.
    // 그림자 색(DropShadow)은 질감(look.texture) 묶음이라 예외.
    [Fact]
    public void Alpha_steps_come_from_theme_roles()
    {
        var alpha = new Regex(@"0x(14|26|33|60|80)\b");
        var bad = DrawingLines(c => alpha.IsMatch(c) && !c.Contains("DropShadow")).ToList();
        Assert.True(bad.Count == 0, "literal alpha steps:\n" + string.Join("\n", bad.Select(b => $"{b.File}:{b.Line} {b.Text}")));
    }

    [Fact]
    public void Fixed_small_text_sizes_come_from_theme_roles()
    {
        var size = new Regex(@"(?<![\w.])1[0-4]f\b");
        var bad = DrawingLines(c => (c.Contains("TextFont.Create(") || c.Contains("DrawBannerText(")) && size.IsMatch(c))
            .Where(l => !IsAllowed(l)).ToList();
        Assert.True(bad.Count == 0, "literal text sizes:\n" + string.Join("\n", bad.Select(b => $"{b.File}:{b.Line} {b.Text}")));
    }
}
