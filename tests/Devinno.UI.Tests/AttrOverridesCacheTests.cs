using Devinno.UI.Controls;
using Xunit;

// 덮어쓰기 표(AttrOverrides)는 값이 바뀔 때만 새로 만든다 — 빌드가 요소마다 읽어도 읽을 때마다 할당하지 않는다
public class AttrOverridesCacheTests
{
    [Fact]
    public void AttrOverrides_IsReusedUntilAnOverrideChanges()
    {
        var c = new DvControl("x");
        Assert.Null(c.AttrOverrides);
        c.SetOverrideText("a");
        var first = c.AttrOverrides;
        Assert.Same(first, c.AttrOverrides);
        Assert.Equal("a", first!["text"]);

        c.SetOverrideText("b");
        Assert.NotSame(first, c.AttrOverrides);
        Assert.Equal("b", c.AttrOverrides!["text"]);
        Assert.Equal("a", first["text"]); // 앞서 받은 표는 그대로(읽는 쪽이 들고 있어도 안전)

        c.ResetOverrides();
        Assert.Null(c.AttrOverrides);
    }
}
