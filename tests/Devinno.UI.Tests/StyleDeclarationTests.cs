using Devinno.UI.Styling;
using Xunit;

// style 글에서 선언 하나만 읽고·바꾸고·지운다 — 나머지 글(순서·공백)은 그대로. 나누기 규칙은 파서와 같다.
public class StyleDeclarationTests
{
    [Fact]
    public void Get_ReturnsTheWinningValue()
    {
        Assert.Equal("10", StyleParser.GetDeclaration("a:1; left: 10 ;b:2", "left"));
        Assert.Equal("30", StyleParser.GetDeclaration("left:10; LEFT:30", "left")); // 뒤가 이김, 키는 대소문자 무시
        Assert.Null(StyleParser.GetDeclaration("a:1", "left"));
        Assert.Null(StyleParser.GetDeclaration(null, "left"));
        Assert.Null(StyleParser.GetDeclaration("  ", "left"));
    }

    [Fact]
    public void Set_ChangesOnlyTheValue()
    {
        Assert.Equal("a:1; left:20 ;b:2", StyleParser.SetDeclaration("a:1; left:10 ;b:2", "left", "20"));
        Assert.Equal("a:1; left: 20;", StyleParser.SetDeclaration("a:1; left: 10;", "left", "20"));
        Assert.Equal("left:10; left:5", StyleParser.SetDeclaration("left:10; left:30", "left", "5")); // 이기는 쪽(뒤)을 바꾼다
    }

    [Fact]
    public void Set_AppendsAMissingKey()
    {
        Assert.Equal("a:1; top:8", StyleParser.SetDeclaration("a:1", "top", "8"));
        Assert.Equal("a:1; top:8", StyleParser.SetDeclaration("a:1;", "top", "8"));
        Assert.Equal("a:1; top:8", StyleParser.SetDeclaration("a:1 ; ", "top", "8"));
        Assert.Equal("top:8", StyleParser.SetDeclaration("", "top", "8"));
        Assert.Equal("top:8", StyleParser.SetDeclaration(null, "top", "8"));
    }

    [Fact]
    public void Set_RejectsAValueOrKeyThatWouldBreakTheList()
    {
        Assert.Throws<ArgumentException>(() => StyleParser.SetDeclaration("a:1", "top", "8; color:red"));
        Assert.Throws<ArgumentException>(() => StyleParser.SetDeclaration("a:1", "to:p", "8"));
        Assert.Throws<ArgumentException>(() => StyleParser.SetDeclaration("a:1", " ", "8"));
        Assert.Equal("background-image:url('a;b.png')", StyleParser.SetDeclaration("", "background-image", "url('a;b.png')"));
    }

    [Fact]
    public void Remove_TakesTheDeclarationAndItsSeparator()
    {
        Assert.Equal("a:1; b:2", StyleParser.RemoveDeclaration("a:1; left:10; b:2", "left"));
        Assert.Equal("a:1", StyleParser.RemoveDeclaration("a:1; left:10", "left"));
        Assert.Equal("a:1;", StyleParser.RemoveDeclaration("a:1; left:10;", "left"));
        Assert.Equal("b:2", StyleParser.RemoveDeclaration("left:10; b:2", "left"));
        Assert.Equal("a:1", StyleParser.RemoveDeclaration("left:1; a:1; Left:2", "left")); // 같은 키 전부
        Assert.Equal("", StyleParser.RemoveDeclaration("left:10", "left"));
        Assert.Equal("a:1", StyleParser.RemoveDeclaration("a:1", "left"));
        Assert.Equal("", StyleParser.RemoveDeclaration(null, "left"));
    }

    [Fact]
    public void Semicolons_InsideQuotesOrParentheses_DoNotSplit()
    {
        Assert.Equal("url('a;b.png')", StyleParser.GetDeclaration("background-image:url('a;b.png'); color:#fff", "background-image"));
        Assert.Equal("#fff", StyleParser.GetDeclaration("background-image:url(a;b.png); color:#fff", "color"));
        // 파서도 같은 규칙 — 괄호 안 ; 뒤의 선언이 살아 있다
        Assert.Equal(StyleParser.Parse("color:#fff").Color, StyleParser.Parse("background-image:url('a;b.png'); color:#fff").Color);
        Assert.NotEqual(StyleParser.Parse("").Color, StyleParser.Parse("color:#fff").Color);
        // 닫히지 않은 따옴표 — 단순 나누기로(뒤 선언을 잃지 않게)
        Assert.Equal("#fff", StyleParser.GetDeclaration("font-family:'x; color:#fff", "color"));
    }
}
