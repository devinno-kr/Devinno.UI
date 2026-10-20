using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using Devinno.UI;
using Devinno.UI.Markup;
using Xunit;

/// <summary>dvml-ui-markup.md 의 xml 예시가 실제로 파싱·렌더되는지 — 문서가 코드와 어긋나면 실패.</summary>
public class MarkupDocTests
{
    public static TheoryData<int, string> XmlBlocks()
    {
        var data = new TheoryData<int, string>();
        var text = File.ReadAllText(DocPath("dvml-ui-markup.md"));
        var i = 0;
        foreach (Match m in Regex.Matches(text, "```xml\\r?\\n(.*?)```", RegexOptions.Singleline))
            data.Add(++i, m.Groups[1].Value);
        return data;
    }

    [Theory]
    [MemberData(nameof(XmlBlocks))]
    public void Markup_example_parses_and_renders(int index, string block)
    {
        // 조각 예시(형제 여러 개)도 받도록 감싼다
        var markup = $"<DvBox>{block}</DvBox>";
        MarkupParser.Parse(markup);
        var png = DvRenderer.RenderToPng(markup, null, 800, 600);
        Assert.True(png.Length > 0, $"block #{index}");
    }

    // 빌드 출력 위치와 무관하게(--artifacts-path 등) 이 소스 파일 위치에서 위로 찾는다
    private static string DocPath(string name, [CallerFilePath] string source = "")
    {
        var dir = new DirectoryInfo(Path.GetDirectoryName(source)!);
        while (dir is not null)
        {
            var p = Path.Combine(dir.FullName, name);
            if (File.Exists(p)) return p;
            dir = dir.Parent;
        }

        throw new FileNotFoundException(name);
    }
}
