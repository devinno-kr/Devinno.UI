using Devinno.UI;
using Devinno.UI.Controls;
using Devinno.UI.Layout;
using Devinno.UI.Styling;
using Xunit;

// 공정도 장비의 id 핸들 — 코드의 Visible 덮어쓰기가 일반 요소와 같이 적용된다.
public class ProcessHandleTests
{
    public sealed class Vm : IDvControlHost
    {
        public DvControl? p1;
        public void Attach(DvControlLookup c) => p1 = c.Bind(p1, "p1");
    }

    private const string Markup = """
        <DvScreen><DvProcess>
          <DvPump id="p1" name="P1" label="P-101"/>
          <DvPump name="P2" label="P-102"/>
          <DvLine from="P1.out" to="P2.in"/>
        </DvProcess></DvScreen>
        """;

    private static IEnumerable<LayoutBox> Flatten(LayoutBox b) => new[] { b }.Concat(b.Children.SelectMany(Flatten));

    private static bool HasPump(DvSurface s, string name) =>
        Flatten(s.Root!).Any(b => b.Node.Tag == "DvPump" && b.Node.Attributes.GetValueOrDefault("name") == name);

    [Fact]
    public void Equipment_HandleVisibleFalse_RemovesIt()
    {
        var vm = new Vm();
        var s = new DvSurface(Markup, vm, DvTheme.Light);
        s.Layout(800, 500);
        Assert.True(HasPump(s, "P1"));

        vm.p1!.Visible = false;
        s.Layout(800, 500);
        Assert.False(HasPump(s, "P1"));
        Assert.True(HasPump(s, "P2"));

        vm.p1.ResetOverrides();
        s.Layout(800, 500);
        Assert.True(HasPump(s, "P1"));
    }
}
