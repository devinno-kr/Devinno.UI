using Devinno.UI.App;
using Xunit;

// 8c-1 Task 3 — ShowDialog 생성자 선택(spec §6.4)
public class DialogFactoryTests
{
    private const string Markup = "<DvStack/>";

    public sealed class Dlg : DvDialog<string>
    {
        public string Name { get; }
        public int N { get; }
        public Dlg(string name, int n) => (Name, N) = (name, n);
        protected override string MarkupSource => Markup;
    }

    public sealed class Dlg2 : DvDialog<string>
    {
        public Dlg2(string s) { }
        public Dlg2(Uri u) { }
        protected override string MarkupSource => Markup;
    }

    public sealed class Dlg3 : DvDialog<string>
    {
        public string? S { get; } = "unset";
        public Dlg3(string? s) => S = s;
        protected override string MarkupSource => Markup;
    }

    public sealed class Dlg4 : DvDialog<string>
    {
        public string[] Items { get; }
        public Dlg4(string[] items) => Items = items;
        protected override string MarkupSource => Markup;
    }

    public sealed class Dlg5 : DvDialog<string>
    {
        public Dlg5() => throw new InvalidOperationException("c");
        protected override string MarkupSource => Markup;
    }

    public abstract class AbsDlg : DvDialog<string>
    {
        protected override string MarkupSource => Markup;
    }

    [Fact]
    public void Abstract_ThrowsArgumentException()
    {
        var ex = Assert.Throws<ArgumentException>(() => DialogFactory.Create<AbsDlg>([]));
        Assert.Contains("AbsDlg", ex.Message);
        Assert.Contains("abstract", ex.Message);
    }

    [Fact]
    public void MatchingCtor_Creates()
    {
        var d = DialogFactory.Create<Dlg>(["a", 3]);
        Assert.Equal("a", d.Name);
        Assert.Equal(3, d.N);
    }

    [Fact]
    public void NoMatch_ThrowsArgumentException_ListsCtors()
    {
        var ex = Assert.Throws<ArgumentException>(() => DialogFactory.Create<Dlg>([3]));
        Assert.Contains("Dlg", ex.Message);
        Assert.Contains("Int32", ex.Message);
        Assert.Contains("Dlg(String name, Int32 n)", ex.Message);
    }

    [Fact]
    public void Ambiguous_Throws()
    {
        Assert.Throws<ArgumentException>(() => DialogFactory.Create<Dlg2>([null]));
    }

    [Fact]
    public void NullArgs_TreatedAsSingleNull()
    {
        var d = DialogFactory.Create<Dlg3>(null);
        Assert.Null(d.S);
    }

    [Fact]
    public void SingleStringArray_TreatedAsOneArg()
    {
        var d = DialogFactory.Create<Dlg4>(new[] { "x", "y" });
        Assert.Equal(2, d.Items.Length);
    }

    [Fact]
    public void CtorThrows_Unwrapped()
    {
        var ex = Assert.Throws<InvalidOperationException>(() => DialogFactory.Create<Dlg5>([]));
        Assert.Equal("c", ex.Message);
    }
}
