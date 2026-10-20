using Devinno.UI;
using Devinno.UI.Layout;
using SkiaSharp;
using Xunit;

// P8b-2~4 드롭다운 — 팝업 스택, 열기·닫기·위치, 선택 커밋, 스크롤, 수명
public class DropdownTests
{
    public enum Direction { Left, Right, Up }

    public sealed class Vm
    {
        private int _count = 2;
        public int Writes;
        public int Count { get => _count; set { _count = value; Writes++; } }
        public Direction Dir { get; set; } = Direction.Right;
        public int Big { get; set; } = 15;
        public List<int> Many { get; } = Enumerable.Range(0, 20).ToList();
        public string Name { get; set; } = "펌프";
        public bool Show { get; set; } = true;
        public int Saves;
        public void Save() => Saves++;
        public double? Gated;
        public bool CheckCount(int v) { Gated = v; return v != 3; }
    }

    private const float W = 400, H = 300;

    // select(0~40) · 입력(40~80) · 저장 버튼(오른쪽 아래, 목록이 덮지 않는 자리)
    private const string Form = """
        <DvScreen style="padding:0; gap:0; align-items:start">
          <DvSelect id="sel" value="{Bind Data.Count}" onbeforecommit="CheckCount" visible="{Data.Show}">
            <DvOption value="1" text="하나"/>
            <DvOption value="2" text="둘"/>
            <DvOption value="3" text="셋"/>
          </DvSelect>
          <DvInput id="in" value="{Bind Data.Name}"/>
          <DvButton text="저장" onclick="Save" style="position:absolute; left:300; top:250; width:80; height:40"/>
        </DvScreen>
        """;

    private static DvSurface Surface(string markup, Vm vm, bool native = true)
    {
        var s = new DvSurface(markup, vm) { UseNativeEditor = native };
        s.Layout(W, H);
        return s;
    }

    private static void Click(DvSurface s, float x, float y)
    {
        s.PointerDown(new SKPoint(x, y));
        s.PointerUp(new SKPoint(x, y));
        s.Layout(W, H);
    }

    private static LayoutBox Box(DvSurface s, string id)
        => Flatten(s.Root!).First(b => b.Node.Attributes.GetValueOrDefault("id") == id);

    private static IEnumerable<LayoutBox> Flatten(LayoutBox b) => new[] { b }.Concat(b.Children.SelectMany(Flatten));

    // 항목 i 의 가운데(창 좌표)
    private static SKPoint Item(DvSurface s, int i)
    {
        var d = s.Dropdown!;
        var r = d.ItemRect(i);
        return new SKPoint(r.MidX, r.MidY);
    }

    // ── 열기·닫기
    [Fact]
    public void Clicking_select_opens_a_dropdown_below()
    {
        var vm = new Vm();
        var s = Surface(Form, vm);
        Click(s, 50, 20);
        var d = s.Dropdown;
        Assert.NotNull(d);
        Assert.True(Box(s, "sel").Editing); // 열림 표시(링·위 화살표)
        var a = Box(s, "sel").Rect;
        Assert.True(d!.Bounds.Top >= a.Bottom);
        Assert.Equal(a.Left, d.Bounds.Left);
        Assert.True(d.Bounds.Width >= a.Width);
        Assert.Equal(3 * DropdownPopup.ItemHeight + 2 * DropdownPopup.Pad, d.Bounds.Height);
    }

    [Fact]
    public void Outside_tap_closes_and_is_not_forwarded()
    {
        var vm = new Vm();
        var s = Surface(Form, vm);
        Click(s, 50, 20);
        Click(s, 390, 290); // 목록 밖
        Assert.Null(s.Dropdown);
        Assert.False(Box(s, "sel").Editing);

        Click(s, 50, 20);
        Assert.False(s.Dropdown!.Bounds.Contains(340, 270));
        Click(s, 340, 270); // 저장 버튼 — 닫히기만 하고 버튼은 눌리지 않는다
        Assert.Null(s.Dropdown);
        Assert.Equal(0, vm.Saves);
        Click(s, 340, 270); // 닫힌 뒤에는 정상
        Assert.Equal(1, vm.Saves);
    }

    [Fact]
    public void Tapping_the_same_select_closes()
    {
        var s = Surface(Form, new Vm());
        Click(s, 50, 20);
        Click(s, 50, 20);
        Assert.Null(s.Dropdown);
    }

    [Fact]
    public void Read_only_and_disabled_do_not_open()
    {
        var s = Surface("<DvScreen style=\"padding:0\"><DvSelect value=\"{Data.Dir}\"/></DvScreen>", new Vm());
        Click(s, 50, 20);
        Assert.Null(s.Dropdown);
        var d = Surface("<DvScreen style=\"padding:0\"><DvSelect value=\"{Bind Data.Dir}\" disabled=\"true\"/></DvScreen>", new Vm());
        Click(d, 50, 20);
        Assert.Null(d.Dropdown);
    }

    [Fact]
    public void Opening_commits_a_pending_input_edit()
    {
        var vm = new Vm();
        var s = Surface(Form, vm);
        Click(s, 50, 60); // 입력 편집
        Assert.True(s.Edit.IsEditing);
        s.SetEditText(s.Edit.SessionId, "새 이름");
        Click(s, 50, 20);
        Assert.Equal("새 이름", vm.Name);
        Assert.NotNull(s.Dropdown);
    }

    // ── 위치
    private static DvSurface At(string style, Vm vm, string valueExpr = "{Bind Data.Count}", string options = "<DvOption value=\"1\"/><DvOption value=\"2\"/><DvOption value=\"3\"/>")
    {
        var s = Surface($"""
            <DvScreen style="padding:0">
              <DvSelect id="sel" value="{valueExpr}" style="position:absolute; {style}">{options}</DvSelect>
            </DvScreen>
            """, vm);
        var a = Box(s, "sel").Rect;
        Click(s, a.Left + 10, a.MidY); // 화면 밖으로 넘친 상자도 보이는 쪽을 누른다
        return s;
    }

    [Fact]
    public void Flips_up_near_the_bottom()
    {
        var s = At("left:10; top:250", new Vm());
        var a = Box(s, "sel").Rect;
        Assert.True(s.Dropdown!.Bounds.Bottom <= a.Top);
    }

    [Fact]
    public void Clamps_into_the_screen_horizontally()
    {
        var s = At("left:300; top:10", new Vm());
        Assert.True(s.Dropdown!.Bounds.Right <= W);
        Assert.True(s.Dropdown.Bounds.Left >= 0);
    }

    [Fact]
    public void Shows_at_most_8_items_and_scrolls_to_the_selection()
    {
        var vm = new Vm();
        var s = Surface("""
            <DvScreen style="padding:0">
              <DvSelect id="sel" value="{Bind Data.Big}" items="{Data.Many}"/>
            </DvScreen>
            """, vm);
        s.Layout(W, 500); // 8개(328)가 아래에 들어갈 높이
        s.PointerDown(new SKPoint(50, 20));
        s.PointerUp(new SKPoint(50, 20));
        s.Layout(W, 500);
        var d = s.Dropdown!;
        Assert.Equal(8 * DropdownPopup.ItemHeight + 2 * DropdownPopup.Pad, d.Bounds.Height);
        var sel = d.ItemRect(15);
        Assert.True(sel.Top >= d.Bounds.Top && sel.Bottom <= d.Bounds.Bottom, $"선택 항목이 보여야 함: {sel} in {d.Bounds}");
    }

    [Fact]
    public void Shrinks_when_neither_side_fits()
    {
        var vm = new Vm();
        var s = Surface("""
            <DvScreen style="padding:0">
              <DvSelect id="sel" value="{Bind Data.Big}" items="{Data.Many}" style="position:absolute; left:10; top:130"/>
            </DvScreen>
            """, vm);
        Click(s, 50, 150);
        var d = s.Dropdown!.Bounds;
        Assert.True(d.Top >= 0 && d.Bottom <= H);
        Assert.True(d.Height < 8 * DropdownPopup.ItemHeight);
    }

    // ── 선택
    [Fact]
    public void Picking_an_item_writes_converted_value_once_and_closes()
    {
        var vm = new Vm();
        var s = Surface(Form, vm);
        Click(s, 50, 20);
        var p = Item(s, 0);
        Click(s, p.X, p.Y);
        Assert.Equal(1, vm.Count);
        Assert.Equal(1, vm.Writes);
        Assert.Null(s.Dropdown);
    }

    [Fact]
    public void Picking_the_same_value_writes_nothing()
    {
        var vm = new Vm();
        var s = Surface(Form, vm);
        Click(s, 50, 20);
        var p = Item(s, 1); // "둘" = 현재 값
        Click(s, p.X, p.Y);
        Assert.Equal(0, vm.Writes);
        Assert.Null(vm.Gated);
    }

    [Fact]
    public void Gate_can_reject()
    {
        var vm = new Vm();
        var s = Surface(Form, vm);
        Click(s, 50, 20);
        var p = Item(s, 2); // 셋 — 거절
        Click(s, p.X, p.Y);
        Assert.Equal(3d, vm.Gated);
        Assert.Equal(0, vm.Writes);
        Assert.Null(s.Dropdown);
    }

    [Fact]
    public void Enum_values_are_written()
    {
        var vm = new Vm();
        var s = Surface("<DvScreen style=\"padding:0\"><DvSelect value=\"{Bind Data.Dir}\"/></DvScreen>", vm);
        Click(s, 50, 20);
        var p = Item(s, 2);
        Click(s, p.X, p.Y);
        Assert.Equal(Direction.Up, vm.Dir);
    }

    [Fact]
    public void Dragging_the_list_scrolls_instead_of_picking()
    {
        var vm = new Vm();
        var s = Surface("""
            <DvScreen style="padding:0">
              <DvSelect id="sel" value="{Bind Data.Big}" items="{Data.Many}"/>
            </DvScreen>
            """, vm);
        Click(s, 50, 20);
        var d = s.Dropdown!;
        var before = d.ScrollY;
        var start = Item(s, 12);
        s.PointerDown(start);
        s.PointerMove(new SKPoint(start.X, start.Y - 60));
        s.PointerUp(new SKPoint(start.X, start.Y - 60));
        Assert.NotNull(s.Dropdown);
        Assert.Equal(before + 60, s.Dropdown!.ScrollY, 1);
        Assert.Equal(0, vm.Writes);
        Assert.Equal(15, vm.Big);
    }

    // ── 수명
    [Fact]
    public void Rebuild_keeps_it_open_and_anchor_removal_closes()
    {
        var vm = new Vm();
        var s = Surface(Form, vm);
        Click(s, 50, 20);
        s.Invalidate();
        s.Layout(W, H);
        Assert.NotNull(s.Dropdown);
        Assert.True(Box(s, "sel").Editing);

        vm.Show = false;
        s.Invalidate();
        s.Layout(W, H);
        Assert.Null(s.Dropdown);
    }

    private sealed class SelectPage() : MarkupPage("""
        <DvScreen style="padding:0">
          <DvSelect value="{Bind Data.Count}"><DvOption value="1"/><DvOption value="2"/></DvSelect>
        </DvScreen>
        """)
    {
        public int Count { get; set; } = 2;
    }

    [Fact]
    public void Page_select_opens_on_the_app_root_and_page_switch_closes_it()
    {
        var page = new SelectPage();
        var other = new MarkupPage("<DvScreen/>");
        var (app, root) = TestApp.WithLeft(page, 100, W, H);
        app.Pages.Add(other);
        Click(root, 150, 20);
        var d = root.Dropdown;
        Assert.NotNull(d);
        Assert.True(d!.Bounds.Left >= 100); // 페이지 좌표 + 페이지 칸 위치(창 기준)
        Assert.Null(page.Surface!.Dropdown);

        var p = Item(root, 0);
        Click(root, p.X, p.Y);
        Assert.Equal(1, page.Count);

        Click(root, 150, 20);
        Assert.NotNull(root.Dropdown);
        app.Show(other);
        root.Layout(W, H);
        Assert.Null(root.Dropdown);
    }

    [Fact]
    public void Dropdown_is_exposed_as_top_popup()
    {
        var s = Surface(Form, new Vm());
        Click(s, 50, 20);
        Assert.Same(s.Dropdown, s.TopPopup);
        Assert.IsAssignableFrom<IPopup>(s.TopPopup);
    }

    [Fact]
    public void Dropdown_anchors_on_a_box_slot()
    {
        var s = Surface(Form, new Vm());
        Click(s, 50, 20);
        var slot = Assert.IsType<BoxSlot>(s.Dropdown!.Slot);
        Assert.Same(Box(s, "sel"), slot.Box);
        Assert.True(slot.Editing);
    }
}
