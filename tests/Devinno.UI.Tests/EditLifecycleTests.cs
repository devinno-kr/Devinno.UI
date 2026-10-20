using Devinno.UI;
using Devinno.UI.Layout;
using SkiaSharp;
using Xunit;

// P8a-5 순서·수명 — 버튼보다 먼저 커밋, 외부 값 무시, 재빌드 Remap, 사라짐/화면 밖, 스크롤 맞춤, 리사이즈
public class EditLifecycleTests
{
    public sealed class Vm
    {
        public string Name { get; set; } = "펌프 A";
        public bool Allow { get; set; } = true;
        public bool ShowName { get; set; } = true;
        public string? Saved;
        public int SaveCount;
        public void Save() { Saved = Name; SaveCount++; }
        public bool CheckName(string v) => Allow;
    }

    private const string Form = """
        <DvScreen style="padding:0; gap:0">
          <DvInput id="a" value="{Bind Data.Name}" onbeforecommit="CheckName" visible="{Data.ShowName}"/>
          <DvButton text="저장" onclick="Save" style="height:40"/>
        </DvScreen>
        """;

    private static DvSurface Begin(object vm, string markup = Form, float x = 50, float y = 20)
    {
        var s = new DvSurface(markup, vm) { UseNativeEditor = true }; // 다른 곳 누름 = 커밋(네이티브 모드)
        s.Layout(400, 300);
        Click(s, x, y);
        Assert.True(s.Edit.IsEditing);
        return s;
    }

    private static void Click(DvSurface s, float x, float y)
    {
        s.PointerDown(new SKPoint(x, y));
        s.PointerUp(new SKPoint(x, y));
        s.Layout(400, 300);
    }

    private static LayoutBox? Box(DvSurface s, string id)
        => Flatten(s.Root!).FirstOrDefault(b => b.Node.Attributes.GetValueOrDefault("id") == id);

    private static IEnumerable<LayoutBox> Flatten(LayoutBox b) => new[] { b }.Concat(b.Children.SelectMany(Flatten));

    [Fact]
    public void Pressing_a_button_commits_before_its_handler_runs()
    {
        var vm = new Vm();
        var s = Begin(vm);
        s.SetEditText(s.Edit.SessionId, "펌프 B");
        Click(s, 10, 60); // 저장 버튼(글자 폭만큼 좁음)
        Assert.Equal("펌프 B", vm.Saved);
        Assert.False(s.Edit.IsEditing);
    }

    [Fact]
    public void Rejected_focus_lost_reverts_and_the_click_still_proceeds()
    {
        var vm = new Vm { Allow = false };
        var s = Begin(vm);
        s.SetEditText(s.Edit.SessionId, "펌프 B");
        Click(s, 10, 60);
        Assert.Equal(1, vm.SaveCount);
        Assert.Equal("펌프 A", vm.Saved);
        Assert.False(s.Edit.IsEditing);
    }

    [Fact]
    public void Rebuild_keeps_the_session_and_external_changes_do_not_win()
    {
        var vm = new Vm();
        var s = Begin(vm);
        var id = s.Edit.SessionId;
        s.SetEditText(id, "운전원 입력");

        vm.Name = "폴링 갱신";
        s.Invalidate();
        s.Layout(400, 300);

        Assert.True(s.Edit.IsEditing);
        Assert.Equal(id, s.Edit.SessionId);
        Assert.Equal("펌프 A", s.Edit.InitialText);
        Assert.True(Box(s, "a")!.Editing);
        Assert.True(Box(s, "a")!.EditHideText); // 새 박스에도 편집 표시 상태가 옮겨졌다

        Assert.True(s.CommitEdit(id, DvEditExit.Enter));
        Assert.Equal("운전원 입력", vm.Name);
    }

    [Fact]
    public void Target_disappearing_commits_and_closes()
    {
        var vm = new Vm();
        var s = Begin(vm);
        s.SetEditText(s.Edit.SessionId, "사라지기 전");
        vm.ShowName = false;
        s.Invalidate();
        s.Layout(400, 300);

        Assert.False(s.Edit.IsEditing);
        Assert.Equal("사라지기 전", vm.Name);
    }

    private const string Scroller = """
        <DvScreen style="padding:0">
          <DvStack style="height:100; overflow:scroll; gap:0; padding:0">
            <DvInput id="i0" value="{Bind Data.Name}"/>
            <DvInput id="i1" value="{Bind Data.Name}"/>
            <DvInput id="i2" value="{Bind Data.Name}"/>
            <DvInput id="i3" value="{Bind Data.Name}"/>
            <DvInput id="i4" value="{Bind Data.Name}"/>
          </DvStack>
        </DvScreen>
        """;

    [Fact]
    public void Begin_scrolls_a_partly_hidden_target_into_view()
    {
        var vm = new Vm();
        var s = Begin(vm, Scroller, 50, 90); // i2 = 80~120, 뷰포트 0~100 → 아래 20 가려짐
        Assert.Equal(new SKRect(s.Edit.Rect.Left, 60, s.Edit.Rect.Right, 100), s.Edit.Rect);
        Assert.Equal(s.Edit.Rect, s.Edit.Clip);
    }

    [Fact]
    public void Scrolling_the_target_fully_out_of_view_commits_and_closes()
    {
        var vm = new Vm();
        var s = Begin(vm, Scroller, 50, 20); // i0
        s.SetEditText(s.Edit.SessionId, "스크롤 전");
        s.PointerWheel(new SKPoint(50, 50), -1f); // 48 → i0 = -48~-8, 완전히 밖
        s.RenderTo(new SKCanvas(new SKBitmap(400, 300)), 400, 300);

        Assert.False(s.Edit.IsEditing);
        Assert.Equal("스크롤 전", vm.Name);
    }

    [Fact]
    public void Resize_moves_rect_and_bumps_version()
    {
        var vm = new Vm();
        var s = new DvSurface("""
            <DvScreen style="padding:0; justify-content:center; align-items:center">
              <DvInput value="{Bind Data.Name}"/>
            </DvScreen>
            """, vm);
        s.Layout(400, 300);
        Click(s, 200, 150);
        Assert.True(s.Edit.IsEditing);
        var (rect, v) = (s.Edit.Rect, s.Edit.Version);

        s.Layout(600, 300);
        Assert.NotEqual(rect, s.Edit.Rect);
        Assert.True(s.Edit.Version > v);
    }
}
