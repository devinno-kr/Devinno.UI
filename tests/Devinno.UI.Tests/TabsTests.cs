using Devinno.UI;
using Devinno.UI.Layout;
using Devinno.UI.Styling;
using SkiaSharp;
using Xunit;

// DvTabs · DvTab — 머리글 + 선택된 탭 내용만. value 가 있으면 VM 에 쓰고, 없으면 화면이 기억한다.
public class TabsTests
{
    private const float W = 600, H = 400;

    public sealed class Vm
    {
        public string Tab { get; set; } = "trend";
        public int Page { get; set; } = 1;
        public bool Allow { get; set; } = true;
        public bool NoSetting { get; set; }

        public bool Gate(string proposed) => Allow;
    }

    private static (DvSurface S, Vm Vm) Start(string tabsAttrs, Vm? vm = null, string extraTab = "")
    {
        vm ??= new Vm();
        var s = new DvSurface($"""
            <DvScreen style="padding:20">
              <DvTabs {tabsAttrs} style="height:300">
                <DvTab value="trend" text="트렌드"><DvLabel text="T-내용"/></DvTab>
                <DvTab value="alarm" text="알람"><DvLabel text="A-내용"/></DvTab>
                <DvTab value="setting" text="설정" {extraTab}><DvLabel text="S-내용"/></DvTab>
              </DvTabs>
            </DvScreen>
            """, vm, DvTheme.Light);
        s.Layout(W, H);
        return (s, vm);
    }

    private static IEnumerable<LayoutBox> Flatten(LayoutBox b) => new[] { b }.Concat(b.Children.SelectMany(Flatten));

    private static string[] Bodies(DvSurface s) =>
        Flatten(s.Root!).Where(b => b.Node.Tag == "DvLabel" && b.ResolvedText?.Contains("-내용") == true).Select(b => b.ResolvedText!).ToArray();

    private static List<LayoutBox> Headers(DvSurface s) => Flatten(s.Root!).Where(b => b.Node.Tag == "DvTabItem").ToList();

    private static void Tap(DvSurface s, LayoutBox b)
    {
        var p = new SKPoint(b.Rect.MidX, b.Rect.MidY);
        s.PointerDown(p);
        s.PointerUp(p);
        s.Layout(W, H);
    }

    [Fact]
    public void Bound_ShowsOnlySelectedTab_HeadersAll()
    {
        var (s, _) = Start("value=\"{Bind Tab}\"", new Vm { Tab = "alarm" });

        Assert.Equal(["트렌드", "알람", "설정"], Headers(s).Select(h => h.ResolvedText));
        Assert.Equal(["A-내용"], Bodies(s));
        Assert.NotEqual(Headers(s)[0].Style.Background, Headers(s)[1].Style.Background); // 선택 머리글 강조
    }

    [Fact]
    public void Bound_HeaderClickWritesValue_AndSwitches()
    {
        var (s, vm) = Start("value=\"{Bind Tab}\"");
        Tap(s, Headers(s)[2]);

        Assert.Equal("setting", vm.Tab);
        Assert.Equal(["S-내용"], Bodies(s));
    }

    [Fact]
    public void Bound_BeforeCommitRejects()
    {
        var (s, vm) = Start("value=\"{Bind Tab}\" onbeforecommit=\"Gate\"");
        vm.Allow = false;
        Tap(s, Headers(s)[1]);

        Assert.Equal("trend", vm.Tab);
        Assert.Equal(["T-내용"], Bodies(s));
    }

    [Fact]
    public void Bound_UnknownValue_ShowsFirstTab()
    {
        var (s, _) = Start("value=\"{Bind Tab}\"", new Vm { Tab = "없음" });
        Assert.Equal(["T-내용"], Bodies(s));
    }

    [Fact]
    public void Unbound_FirstTab_ClickSwitches_SurvivesRebuild()
    {
        var (s, _) = Start("");
        Assert.Equal(["T-내용"], Bodies(s));

        Tap(s, Headers(s)[1]);
        Assert.Equal(["A-내용"], Bodies(s));

        s.Invalidate();
        s.Layout(W, H);
        Assert.Equal(["A-내용"], Bodies(s)); // 다시 만들어도 기억
    }

    [Fact]
    public void DisabledTab_HeaderDoesNotSwitch()
    {
        var (s, vm) = Start("value=\"{Bind Tab}\"", new Vm { NoSetting = true }, "disabled=\"{NoSetting}\"");
        Tap(s, Headers(s)[2]);

        Assert.Equal("trend", vm.Tab);
        Assert.Equal(["T-내용"], Bodies(s));
    }

    [Fact]
    public void IntValue_Converted()
    {
        var vm = new Vm();
        var s = new DvSurface("""
            <DvScreen><DvTabs value="{Bind Page}">
              <DvTab value="1" text="1"><DvLabel text="1-내용"/></DvTab>
              <DvTab value="2" text="2"><DvLabel text="2-내용"/></DvTab>
            </DvTabs></DvScreen>
            """, vm, DvTheme.Light);
        s.Layout(W, H);
        Tap(s, Headers(s)[1]);

        Assert.Equal(2, vm.Page);
        Assert.Equal(["2-내용"], Bodies(s));
    }

    [Fact]
    public void Body_BelowHeader_FillsHeight()
    {
        var (s, _) = Start("value=\"{Bind Tab}\"");
        var tabs = Flatten(s.Root!).Single(b => b.Node.Tag == "DvTabs");
        var header = Flatten(s.Root!).Single(b => b.Node.Tag == "DvTabHeader");
        var body = Flatten(s.Root!).Single(b => b.Node.Tag == "DvTab");

        Assert.True(body.Rect.Top >= header.Rect.Bottom);
        Assert.Equal(tabs.Rect.Bottom, body.Rect.Bottom, 0.5); // 남은 높이를 채움
    }
}
