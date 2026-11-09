using Devinno.UI.Layout;
using Devinno.UI.Markup;
using Xunit;

// 다시 만들기 사이에 요소별 객체를 유지하는 저장소 (커스텀 위젯·비디오 재생기가 함께 씀).
// 키 = (마크업 요소, 이번 빌드에서 그 요소가 몇 번째로 만들어졌나).
public class InstanceStoreTests
{
    private sealed class Thing : IDisposable
    {
        public string Tag = "";
        public bool Disposed;
        public void Dispose() => Disposed = true;
    }

    private static readonly ElementNode A = new() { Tag = "A" };

    [Fact]
    public void SameKey_NextBuild_GetsSameObject()
    {
        var store = new InstanceStore<Thing>(disposeDropped: true);
        store.Begin();
        var first = store.Get(new InstanceKey(A, 0), () => new Thing());
        store.End();
        store.Begin();
        var again = store.Get(new InstanceKey(A, 0), () => new Thing());
        store.End();
        Assert.Same(first, again);
        Assert.False(first.Disposed);
    }

    [Fact]
    public void NotAskedFor_IsDisposedAtEnd()
    {
        var store = new InstanceStore<Thing>(disposeDropped: true);
        store.Begin();
        var a0 = store.Get(new InstanceKey(A, 0), () => new Thing());
        var a1 = store.Get(new InstanceKey(A, 1), () => new Thing());
        store.End();
        store.Begin();
        store.Get(new InstanceKey(A, 0), () => new Thing());
        store.End();
        Assert.False(a0.Disposed);
        Assert.True(a1.Disposed);
    }

    [Fact]
    public void ReuseRejected_ReplacesAndDisposesOld()
    {
        var store = new InstanceStore<Thing>(disposeDropped: true);
        store.Begin();
        var old = store.Get(new InstanceKey(A, 0), () => new Thing { Tag = "x" });
        store.End();
        store.Begin();
        var now = store.Get(new InstanceKey(A, 0), () => new Thing { Tag = "y" }, reuse: t => t.Tag == "y");
        store.End();
        Assert.NotSame(old, now);
        Assert.True(old.Disposed);
        Assert.False(now.Disposed);
    }

    [Fact]
    public void WithoutDisposeDropped_DroppedIsNotDisposed()   // 커스텀 위젯: 지금처럼 Dispose 하지 않음
    {
        var store = new InstanceStore<Thing>();
        store.Begin();
        var a = store.Get(new InstanceKey(A, 0), () => new Thing());
        store.End();
        store.Begin();
        store.End();
        Assert.False(a.Disposed);
    }

    [Fact]
    public void Engine_GivesEachBuildOfANodeItsOwnNumber()
    {
        var root = MarkupParser.Parse("<DvScreen><DvRepeat items=\"{Items}\"><DvLabel text=\"{Item}\"/></DvRepeat></DvScreen>");
        var box = new LayoutEngine().Layout(root, new { Items = new[] { "a", "b", "c" } }, 400, 300);
        var labels = Flatten(box).Where(b => b.Node.Tag == "DvLabel").ToList();
        Assert.Equal([0, 1, 2], labels.Select(l => l.Instance.Nth));
        Assert.All(labels, l => Assert.Same(labels[0].Node, l.Instance.Node));
    }

    private static IEnumerable<LayoutBox> Flatten(LayoutBox b) => new[] { b }.Concat(b.Children.SelectMany(Flatten));
}
