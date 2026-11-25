using Devinno.UI;
using Devinno.UI.App;
using Devinno.UI.Layout;
using Xunit;

// DvApp.AutoRefresh — 주기마다 보이는 화면(페이지·막대·대화상자)이 지난 빌드가 읽은 바인딩을 다시 읽어, 값이 달라졌을 때만 다시 만든다.
// 기본은 꺼짐, 주기 전에는 그대로, 바뀐 것 없으면 다시 만들지 않음, static 바인딩도, 같은 목록 객체 안 변경은 모름, 호스트를 그 시각에 깨운다
public class AutoRefreshTests
{
    private const float W = 600, H = 400;

    private sealed class Page : DvPage
    {
        public double Level { get; set; } = 10;
        public List<string> Rows { get; set; } = ["a"];
        protected override string MarkupSource => """
            <DvScreen style="padding:0">
              <DvLabel text="L {Level:0.0}"/>
              <DvRepeat items="{Rows}"><DvLabel text="{Item}"/></DvRepeat>
              <DvLabel text="M {Static RefreshState.Mode}"/>
            </DvScreen>
            """;
    }

    private sealed class Bar : DvBar
    {
        public string Status { get; set; } = "ok";
        protected override string MarkupSource => "<DvStack style=\"height:40\"><DvLabel text=\"S {Status}\"/></DvStack>";
    }

    private static IEnumerable<LayoutBox> Flatten(LayoutBox b) => new[] { b }.Concat(b.Children.SelectMany(Flatten));

    private static List<string?> Texts(DvSurface s) => Flatten(s.Root!).Where(b => b.Node.Tag == "DvLabel").Select(b => b.ResolvedText).ToList();

    private sealed class Rig
    {
        public long T = 1_000_000;
        public DvApp App = new();
        public Page Page = new();
        public Bar Bar = new();

        public Rig(TimeSpan refresh)
        {
            RefreshState.Mode = "AUTO";
            App.Clock = () => T;
            App.Pages.Add(Page);
            App.Top = Bar;
            App.Surface.Layout(W, H);
            App.AutoRefresh = refresh;
            Frame(); // 켜짐 적용 — 다음 빌드부터 기록
        }

        public void Frame() => App.Surface.Layout(W, H);

        public void Advance(long ms)
        {
            T += ms;
            Frame();
        }
    }

    [Fact]
    public void Off_by_default_values_changed_outside_are_not_seen()
    {
        var rig = new Rig(TimeSpan.Zero);
        rig.Page.Level = 99;
        rig.Advance(10_000);
        Assert.Equal("L 10.0", Texts(rig.Page.Surface!)[0]);
    }

    [Fact]
    public void A_changed_value_appears_at_the_next_period_without_Invalidate()
    {
        var rig = new Rig(TimeSpan.FromMilliseconds(200));
        rig.Advance(200); // 첫 확인 — 기록 없이 만든 화면을 기록하며 다시 만든다
        rig.Page.Level = 42;
        rig.Bar.Status = "fault";
        rig.Advance(150);
        Assert.Equal("L 10.0", Texts(rig.Page.Surface!)[0]); // 주기 전
        rig.Advance(50);
        Assert.Equal("L 42.0", Texts(rig.Page.Surface!)[0]);
        Assert.Equal("S fault", Texts(rig.Bar.Surface!)[0]);
    }

    [Fact]
    public void Nothing_changed_nothing_is_rebuilt()
    {
        var rig = new Rig(TimeSpan.FromMilliseconds(200));
        rig.Advance(200);
        var root = rig.Page.Surface!.Root;
        rig.Advance(200);
        rig.Advance(200);
        Assert.Same(root, rig.Page.Surface!.Root);
    }

    [Fact]
    public void Static_values_are_checked_too()
    {
        var rig = new Rig(TimeSpan.FromMilliseconds(100));
        rig.Advance(100);
        RefreshState.Mode = "MANUAL";
        rig.Advance(100);
        Assert.Equal("M MANUAL", Texts(rig.Page.Surface!)[^1]);
    }

    [Fact]
    public void A_new_list_is_seen_but_a_change_inside_the_same_list_is_not()
    {
        var rig = new Rig(TimeSpan.FromMilliseconds(100));
        rig.Advance(100);
        rig.Page.Rows.Add("b"); // 같은 목록 객체 — 비교로는 모른다
        rig.Advance(100);
        Assert.Equal(3, Texts(rig.Page.Surface!).Count);
        rig.Page.Rows = ["a", "b", "c"]; // 새 목록
        rig.Advance(100);
        Assert.Equal(5, Texts(rig.Page.Surface!).Count);
    }

    [Fact]
    public void The_host_is_woken_at_the_next_check()
    {
        var rig = new Rig(TimeSpan.FromMilliseconds(250));
        var due = rig.App.Surface.NextAnimationDue(rig.T);
        Assert.NotNull(due);
        Assert.InRange(due!.Value, rig.T + 1, rig.T + 250);
        rig.App.AutoRefresh = TimeSpan.Zero;
        rig.Frame();
        Assert.Null(rig.App.Surface.NextAnimationDue(rig.T));
    }

    [Fact]
    public void A_negative_period_throws()
        => Assert.Throws<ArgumentOutOfRangeException>(() => new DvApp().AutoRefresh = TimeSpan.FromMilliseconds(-1));
}

// 이 테스트 전용 static — 다른 테스트 클래스(병렬 실행)와 값을 나눠 쓰지 않게
public static class RefreshState
{
    public static string Mode { get; set; } = "AUTO";
}
