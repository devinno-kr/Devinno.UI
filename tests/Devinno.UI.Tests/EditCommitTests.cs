using Devinno.UI;
using Devinno.UI.Layout;
using SkiaSharp;
using Xunit;

// P8a-3 커밋/취소 — 입력 중엔 세션 버퍼만, VM 쓰기는 커밋 때 1회
public class EditCommitTests
{
    public sealed class Vm
    {
        private string _name = "펌프 A";
        public int NameWrites;
        public string Name { get => _name; set { _name = value; NameWrites++; } }
        public string Other { get; set; } = "B";
        public bool Allow { get; set; } = true;
        public string? Proposed;

        public bool CheckName(string proposed)
        {
            Proposed = proposed;
            return Allow;
        }
    }

    private const string Markup = """
        <DvScreen style="padding:0; gap:0">
          <DvInput id="a" value="{Bind Data.Name}" onbeforecommit="CheckName"/>
          <DvInput id="b" value="{Bind Data.Other}"/>
          <DvButton text="저장" onclick="Save" style="height:40"/>
        </DvScreen>
        """;

    // 다른 곳을 누르면 커밋 = 네이티브 편집기 모드의 동작(가상 키보드 모드는 바깥 = 취소)
    private static DvSurface Start(Vm vm, float y = 20, bool native = true)
    {
        var s = new DvSurface(Markup, vm) { UseNativeEditor = native };
        s.Layout(400, 300);
        s.PointerDown(new SKPoint(50, y));
        s.PointerUp(new SKPoint(50, y));
        s.Layout(400, 300);
        return s;
    }

    private static LayoutBox Box(DvSurface s, string id)
        => Flatten(s.Root!).First(b => b.Node.Attributes.GetValueOrDefault("id") == id);

    private static IEnumerable<LayoutBox> Flatten(LayoutBox b) => new[] { b }.Concat(b.Children.SelectMany(Flatten));

    [Fact]
    public void Typing_does_not_touch_vm_and_commit_writes_once()
    {
        var vm = new Vm();
        var s = Start(vm);
        var id = s.Edit.SessionId;

        s.SetEditText(id, "펌");
        s.SetEditText(id, "펌프");
        s.SetEditText(id, "펌프 C");
        Assert.Equal(0, vm.NameWrites);
        Assert.Equal("펌프 A", vm.Name);

        Assert.True(s.CommitEdit(id, DvEditExit.Enter));
        Assert.Equal(1, vm.NameWrites);
        Assert.Equal("펌프 C", vm.Name);
        Assert.Equal("펌프 C", vm.Proposed);
        Assert.False(s.Edit.IsEditing);
    }

    [Fact]
    public void Cancel_writes_nothing()
    {
        var vm = new Vm();
        var s = Start(vm);
        s.SetEditText(s.Edit.SessionId, "xyz");
        s.CancelEdit(s.Edit.SessionId);
        Assert.Equal(0, vm.NameWrites);
        Assert.False(s.Edit.IsEditing);
    }

    [Fact]
    public void Unchanged_value_writes_nothing()
    {
        var vm = new Vm();
        var s = Start(vm);
        Assert.True(s.CommitEdit(s.Edit.SessionId, DvEditExit.Enter));
        Assert.Equal(0, vm.NameWrites);
        Assert.Null(vm.Proposed);
        Assert.False(s.Edit.IsEditing);
    }

    [Fact]
    public void Stale_session_id_is_ignored()
    {
        var vm = new Vm();
        var s = Start(vm);
        var stale = s.Edit.SessionId - 1;
        s.SetEditText(stale, "무시");
        Assert.False(s.CommitEdit(stale, DvEditExit.Enter));
        s.CancelEdit(stale);
        Assert.True(s.Edit.IsEditing);

        Assert.True(s.CommitEdit(s.Edit.SessionId, DvEditExit.Enter));
        Assert.Equal(0, vm.NameWrites); // "무시" 는 반영되지 않았다
    }

    [Fact]
    public void Rejected_enter_keeps_session_with_error()
    {
        var vm = new Vm { Allow = false };
        var s = Start(vm);
        var id = s.Edit.SessionId;
        s.SetEditText(id, "금지");

        Assert.False(s.CommitEdit(id, DvEditExit.Enter));
        s.Layout(400, 300);
        Assert.Equal(0, vm.NameWrites);
        Assert.True(s.Edit.IsEditing);
        Assert.Equal(id, s.Edit.SessionId);
        Assert.NotNull(s.Edit.Error);
        Assert.NotNull(Box(s, "a").EditError);

        // 고쳐서 다시 → 오류 해제·커밋
        vm.Allow = true;
        s.SetEditText(id, "허용");
        s.Layout(400, 300);
        Assert.Null(s.Edit.Error);
        Assert.True(s.CommitEdit(id, DvEditExit.Enter));
        Assert.Equal("허용", vm.Name);
    }

    [Fact]
    public void Rejected_focus_lost_reverts_and_closes()
    {
        var vm = new Vm { Allow = false };
        var s = Start(vm);
        var id = s.Edit.SessionId;
        s.SetEditText(id, "금지");
        Assert.False(s.CommitEdit(id, DvEditExit.FocusLost));
        Assert.False(s.Edit.IsEditing);
        Assert.Equal("펌프 A", vm.Name);
    }

    [Fact]
    public void Switching_to_another_input_commits_the_first()
    {
        var vm = new Vm();
        var s = Start(vm);
        s.SetEditText(s.Edit.SessionId, "펌프 Z");
        s.PointerDown(new SKPoint(50, 60));
        s.PointerUp(new SKPoint(50, 60));
        s.Layout(400, 300);

        Assert.Equal("펌프 Z", vm.Name);
        Assert.True(s.Edit.IsEditing);
        Assert.Equal("B", s.Edit.InitialText);
    }

    [Fact]
    public void Core_keyboard_mode_shows_session_text_in_the_box()
    {
        var vm = new Vm();
        var s = Start(vm, native: false);
        s.SetEditText(s.Edit.SessionId, "입력중");
        s.Layout(400, 300);
        Assert.Equal("입력중", Box(s, "a").EditText);
        Assert.False(Box(s, "a").EditHideText);
    }

    [Fact]
    public void Native_mode_hides_box_text_while_editing()
    {
        var vm = new Vm();
        var s = new DvSurface(Markup, vm) { UseNativeEditor = true };
        s.Layout(400, 300);
        s.PointerDown(new SKPoint(50, 20));
        s.PointerUp(new SKPoint(50, 20));
        s.Layout(400, 300);
        Assert.True(Box(s, "a").EditHideText);
    }
}
