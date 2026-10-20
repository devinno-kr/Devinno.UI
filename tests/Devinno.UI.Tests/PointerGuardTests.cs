using Devinno.UI;
using Devinno.UI.Alarms;
using Devinno.UI.Layout;
using Devinno.UI.Styling;
using SkiaSharp;
using Xunit;

// 비활성(상위 상속 포함)이거나 보이지 않는(잘림·스크롤 밖·가려짐·visible 거짓) 요소에는 포인터 이벤트가 가지 않는다.
// 모두 DvSurface 에 실제 PointerDown/Move/Up/Wheel 을 보내서 확인한다.
public class PointerGuardTests
{
    private const float W = 400, H = 400;

    public sealed class Vm
    {
        public List<string> Calls { get; } = new();
        public double Level { get; set; } = 40;
        public string Name { get; set; } = "a";
        public string Mode { get; set; } = "x";
        public DateTime At { get; set; } = new(2026, 10, 14, 9, 0, 0);
        public string Pen { get; set; } = "#0000FF";
        public bool On { get; set; }
        public int Gates;

        public bool Gate(double v) { Gates++; return true; }
        public void Hit() => Calls.Add("Hit");
        public void Outer() => Calls.Add("Outer");
        public void Over() => Calls.Add("Over");
        public void Under() => Calls.Add("Under");
        public void H0() => Calls.Add("H0");
        public void H1() => Calls.Add("H1");
        public void H2() => Calls.Add("H2");
        public void H3() => Calls.Add("H3");
    }

    private static DvSurface Surface(string markup, object vm, bool native = true)
    {
        var s = new DvSurface(markup, vm, DvTheme.Light) { UseNativeEditor = native };
        s.Layout(W, H);
        return s;
    }

    private static IEnumerable<LayoutBox> Flatten(LayoutBox b) => new[] { b }.Concat(b.Children.SelectMany(Flatten));
    private static LayoutBox Find(DvSurface s, string tag) => Flatten(s.Root!).First(b => b.Node.Tag == tag);
    private static SKPoint Mid(LayoutBox b) => new(b.Rect.MidX, b.Rect.MidY);

    private static void Tap(DvSurface s, SKPoint p)
    {
        s.PointerDown(p);
        s.PointerUp(p);
        s.Layout(W, H);
    }

    private static SKBitmap Render(DvSurface s)
    {
        var bmp = new SKBitmap((int)W, (int)H);
        using var c = new SKCanvas(bmp);
        s.RenderTo(c, W, H, 0);
        return bmp;
    }

    private static string Locked(string inner) =>
        "<DvScreen style=\"padding:0; gap:0; display:flex; flex-direction:column; align-items:stretch\">" +
        "<DvStack disabled=\"true\" style=\"padding:0; gap:0; align-items:stretch\">" + inner + "</DvStack></DvScreen>";

    // ── A. 비활성(상위 상속) — 아무 동작 없음

    [Fact]
    public void A1_knob_in_disabled_container_does_not_rotate_or_commit()
    {
        var vm = new Vm();
        var s = Surface(Locked("<DvKnob value=\"{Bind Level}\" onbeforecommit=\"Gate\" style=\"width:100; height:100\"/>"), vm);
        var k = Find(s, "DvKnob").Rect;

        s.PointerDown(new SKPoint(k.MidX, k.Top + 10));
        s.PointerMove(new SKPoint(k.Right - 10, k.MidY));
        Assert.Equal(40, Find(s, "DvKnob").KnobValue); // 미리보기도 없음
        s.PointerUp(new SKPoint(k.Right - 10, k.MidY));
        s.Layout(W, H);

        Assert.Equal(40, vm.Level);
        Assert.Equal(0, vm.Gates);
    }

    [Fact]
    public void A2_input_in_disabled_container_does_not_start_editing()
    {
        foreach (var native in new[] { true, false })
        {
            var s = Surface(Locked("<DvInput value=\"{Bind Name}\"/>"), new Vm(), native);
            Tap(s, Mid(Find(s, "DvInput")));
            Assert.False(s.Edit.IsEditing);
            Assert.False(s.Keyboard is { IsOpen: true });
        }
    }

    [Theory]
    [InlineData("<DvSelect value=\"{Bind Mode}\"><DvOption value=\"x\" text=\"X\"/><DvOption value=\"y\" text=\"Y\"/></DvSelect>", "DvSelect")]
    [InlineData("<DvDateTimeInput value=\"{Bind At}\"/>", "DvDateTimeInput")]
    [InlineData("<DvColorInput value=\"{Bind Pen}\"/>", "DvColorInput")]
    public void A3_popup_inputs_in_disabled_container_do_not_open(string widget, string tag)
    {
        var s = Surface(Locked(widget), new Vm());
        Tap(s, Mid(Find(s, tag)));
        Assert.Null(s.TopPopup);
        Assert.False(s.Edit.IsEditing);
    }

    public sealed class Row
    {
        public Row(int id) => Id = id;
        public int Id { get; }
        public int Writes;
        private string _name = "n";
        private string _mode = "x";
        private bool _on;
        public string Name { get => _name; set { _name = value; Writes++; } }
        public string Mode { get => _mode; set { _mode = value; Writes++; } }
        public bool On { get => _on; set { _on = value; Writes++; } }
    }

    public sealed class GridVm
    {
        public List<Row> Rows { get; set; } = Enumerable.Range(0, 10).Select(i => new Row(i)).ToList();
        public List<Row> Picked { get; set; } = new();
        public List<string> Calls { get; } = new();
        public void Inspect(Row r) => Calls.Add("row" + r.Id);
        public void Go(Row r) => Calls.Add("go" + r.Id);
        public void Hit() => Calls.Add("Hit");
    }

    // Id 0~60 | 이름(input) 60~160 | 모드(select) 160~260 | 사용(check) 260~310 | 버튼 310~370. 헤더 40, 행 48(행 r 가운데 y = 64 + 48r)
    private const string GridCols =
        "<DvColumn header=\"Id\" field=\"Id\" width=\"60\" sortable=\"true\"/>" +
        "<DvColumn header=\"이름\" field=\"Name\" width=\"100\" cell-type=\"input\"/>" +
        "<DvColumn header=\"모드\" field=\"Mode\" width=\"100\" cell-type=\"select\">" +
        "<DvOption value=\"x\" text=\"X\"/><DvOption value=\"y\" text=\"Y\"/></DvColumn>" +
        "<DvColumn header=\"사용\" field=\"On\" width=\"50\" cell-type=\"check\"/>" +
        "<DvColumn header=\"실행\" width=\"60\" cell-type=\"button\" text=\"GO\" onclick=\"Go\"/>";

    private static string GridMarkup(string extra = "") =>
        $"<DvDataGrid items=\"{{Rows}}\" row-key=\"Id\" selection-mode=\"single\" selection=\"{{Bind Picked}}\" onrowclick=\"Inspect\" {extra} style=\"height:240\">{GridCols}</DvDataGrid>";

    [Fact]
    public void A4_grid_in_disabled_container_ignores_row_cell_and_header_taps()
    {
        foreach (var native in new[] { true, false })
        {
            var vm = new GridVm();
            var s = Surface(Locked(GridMarkup()), vm, native);
            var grid = Find(s, "DvDataGrid");

            Tap(s, new SKPoint(30, 64));    // 행 탭
            Tap(s, new SKPoint(110, 64));   // input 셀
            Tap(s, new SKPoint(210, 64));   // select 셀
            Tap(s, new SKPoint(285, 64));   // check 셀
            Tap(s, new SKPoint(340, 64));   // button 셀
            Tap(s, new SKPoint(30, 20));    // 정렬 가능한 헤더

            grid = Find(s, "DvDataGrid");
            Assert.Empty(vm.Calls);
            Assert.Empty(vm.Picked);
            Assert.Empty(grid.Grid!.SelectedItems);
            Assert.Empty(grid.Grid.Sort);
            Assert.All(vm.Rows, r => Assert.Equal(0, r.Writes));
            Assert.False(s.Edit.IsEditing);
            Assert.Null(s.TopPopup);
        }
    }

    [Fact]
    public void A5_alarm_widgets_in_disabled_container_do_not_ack()
    {
        var vm = new AlarmWidgetTests.Vm();
        vm.Log.Raise(AlarmPriority.Critical, "critical");
        var s = Surface(Locked(
            "<DvAlarmBanner items=\"{Alarms}\" onack=\"AckAlarm\" style=\"height:40\"/>" +
            "<DvAlarmList items=\"{Alarms}\" onack=\"AckAlarm\" style=\"height:200\"/>"), vm);

        Tap(s, Mid(Find(s, "DvAlarmBanner")));
        var list = Find(s, "DvAlarmList");
        var ack = AlarmListLayout.Compute(list.Rect, vm.Alarms, list.AlarmRowHeight, list.AlarmShowAck).Rows[0].AckButton!.Value;
        Tap(s, new SKPoint(ack.MidX, ack.MidY));

        Assert.Empty(vm.AckedIds);
    }

    public sealed class RepeatVm
    {
        public List<string> Items { get; } = new() { "a", "b" };
        public List<string> Started { get; } = new();
        public void Start(string item) => Started.Add(item);
    }

    [Fact]
    public void A7_repeat_item_onclick_in_disabled_container_is_not_called()
    {
        var vm = new RepeatVm();
        var s = Surface(Locked("<DvRepeat items=\"{Items}\"><DvBox style=\"height:30\" onclick=\"Start\"/></DvRepeat>"), vm);
        foreach (var b in Flatten(s.Root!).Where(b => b.OnClick == "Start").ToList())
            Tap(s, Mid(b));
        Assert.Empty(vm.Started);
    }

    [Theory]
    [InlineData("<DvStack disabled=\"true\" style=\"width:200; height:100\"><DvLabel text=\"잠김\"/></DvStack>")]
    [InlineData("<DvStack disabled=\"true\" style=\"width:200; height:100\"/>")]
    [InlineData("<DvButton text=\"B\" disabled=\"true\" style=\"width:200; height:100\"/>")]
    [InlineData("<DvStack disabled=\"true\" style=\"width:200; height:100\"><DvButton text=\"B\" style=\"width:100; height:40\"/></DvStack>")]
    public void A8_click_on_disabled_child_does_not_leak_to_enabled_ancestor(string child)
    {
        var vm = new Vm();
        var s = Surface($"<DvScreen><DvBox onclick=\"Outer\" style=\"width:300; height:300; padding:0\">{child}</DvBox></DvScreen>", vm);
        var outer = Flatten(s.Root!).First(b => b.OnClick == "Outer");
        var inner = outer.Children[0];

        Tap(s, new SKPoint(inner.Rect.Left + 10, inner.Rect.Top + 10));
        Assert.Empty(vm.Calls);

        Tap(s, new SKPoint(outer.Rect.Right - 10, outer.Rect.Bottom - 10)); // 활성 부분은 그대로 클릭됨
        Assert.Equal(new[] { "Outer" }, vm.Calls);
    }

    [Fact]
    public void A9_scrolling_still_works_inside_disabled_area()
    {
        var items = string.Concat(Enumerable.Range(0, 8).Select(_ => "<DvButton onclick=\"Hit\" style=\"height:50\"/>"));
        var vm = new Vm();
        var s = Surface("<DvScreen style=\"padding:0\"><DvStack disabled=\"true\" style=\"overflow:scroll; width:300; height:200; padding:0; gap:0; align-items:stretch\">" +
                        items + "</DvStack></DvScreen>", vm);
        var box = s.Root!.Children[0];

        s.PointerWheel(new SKPoint(100, 100), -1);
        Assert.True(box.ScrollY > 0);

        var before = box.ScrollY;
        s.PointerDown(new SKPoint(100, 150));
        s.PointerMove(new SKPoint(100, 120));
        s.PointerMove(new SKPoint(100, 100));
        s.PointerUp(new SKPoint(100, 100));
        Assert.True(box.ScrollY > before);
        Assert.Empty(vm.Calls);
    }

    // ── B. 비활성의 시각 반응 — hover·press 스타일을 쓰지 않음

    private const string Blue = "background:#0000ff";

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void B_disabled_box_never_paints_hover_or_press_style(bool inherited, bool withDisabledStyle)
    {
        var ds = withDisabledStyle ? " disabled-style=\"background:#808080\"" : "";
        var btn = $"<DvBox onclick=\"Hit\" style=\"width:100; height:100; {Blue}\" hover-style=\"background:#ff0000\" press-style=\"background:#00ff00\"{ds}" +
                  (inherited ? "/>" : " disabled=\"true\"/>");
        var markup = inherited
            ? $"<DvScreen style=\"padding:0\"><DvStack disabled=\"true\" style=\"padding:0\">{btn}</DvStack></DvScreen>"
            : $"<DvScreen style=\"padding:0\">{btn}</DvScreen>";
        var s = Surface(markup, new Vm());
        var expected = withDisabledStyle ? new SKColor(0x80, 0x80, 0x80) : new SKColor(0, 0, 0xff);

        Render(s).Dispose();
        s.PointerMove(new SKPoint(50, 50));
        using (var hover = Render(s))
            Assert.Equal(expected, hover.GetPixel(50, 50));

        s.PointerDown(new SKPoint(50, 50));
        using (var press = Render(s))
            Assert.Equal(expected, press.GetPixel(50, 50));
        s.PointerUp(new SKPoint(50, 50));
    }

    [Fact]
    public void B_disabled_hover_does_not_request_render()
    {
        var s = Surface(Locked($"<DvBox style=\"height:100; {Blue}\" hover-style=\"background:#ff0000\" press-style=\"background:#00ff00\"/>" +
                               GridMarkup()), new GridVm());
        Render(s).Dispose();
        Assert.False(s.NeedsRender);

        s.PointerMove(new SKPoint(50, 50));   // hover 스타일 상자
        s.PointerMove(new SKPoint(30, 164));  // 비활성 그리드 행
        s.PointerDown(new SKPoint(50, 50));   // press 스타일 상자
        Assert.False(s.NeedsRender);
        Assert.Equal(-1, Find(s, "DvDataGrid").Grid!.HoverRow);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)] // 대조 — 활성 노브는 hover·press 때 면이 밝아진다
    public void B_disabled_knob_face_does_not_brighten_on_hover_or_press(bool locked)
    {
        const string knob = "<DvKnob value=\"{Bind Level}\" style=\"width:100; height:100\"/>";
        var s = new DvSurface(locked ? Locked(knob) : "<DvScreen>" + knob + "</DvScreen>", new Vm(), DvTheme.Dark); // 어두운 면이라 밝아짐이 보임
        s.Layout(W, H);
        var k = Find(s, "DvKnob").Rect;
        byte[] Px() { using var b = Render(s); return b.Bytes; }

        var idle = Px();
        s.PointerMove(new SKPoint(k.MidX, k.MidY));
        var hover = Px();
        s.PointerDown(new SKPoint(k.MidX, k.MidY));
        var press = Px();
        Assert.Equal(locked, idle.SequenceEqual(hover));
        Assert.Equal(locked, idle.SequenceEqual(press));
    }

    // ── C. 보이지 않는 곳 — 이벤트 없음

    [Fact]
    public void C1_clipped_part_of_child_in_overflow_hidden_parent_is_not_clickable_or_hoverable()
    {
        var vm = new Vm();
        var s = Surface("<DvScreen style=\"padding:0\"><DvBox style=\"overflow:hidden; width:100; height:100; padding:0\">" +
                        "<DvBox onclick=\"Hit\" style=\"position:absolute; left:0; top:0; width:300; height:50; " + Blue + "\" hover-style=\"background:#ff0000\"/>" +
                        "</DvBox></DvScreen>", vm);
        Assert.True(Flatten(s.Root!).First(b => b.OnClick == "Hit").Rect.Right >= 300); // 정말 삐져나옴

        Tap(s, new SKPoint(200, 25));
        Assert.Empty(vm.Calls);

        Render(s).Dispose();
        s.PointerMove(new SKPoint(200, 25));
        using (var bmp = Render(s))
            Assert.Equal(new SKColor(0, 0, 0xff), bmp.GetPixel(50, 25)); // 보이는 부분에 hover 가 생기지 않음

        Tap(s, new SKPoint(50, 25)); // 보이는 부분은 클릭됨
        Assert.Equal(new[] { "Hit" }, vm.Calls);
    }

    // overflow:visible 부모 밖으로 삐져나와 보이는 부분은 클릭·hover 된다(보이는 곳 = 이벤트를 받는 곳)
    [Fact]
    public void C1b_overflowing_part_of_child_in_overflow_visible_parent_is_clickable_and_hoverable()
    {
        var vm = new Vm();
        var s = Surface("<DvScreen style=\"padding:0\"><DvBox style=\"width:100; height:100; padding:0\">" +
                        "<DvBox onclick=\"Hit\" style=\"position:absolute; left:0; top:0; width:300; height:50; " + Blue + "\" hover-style=\"background:#ff0000\"/>" +
                        "</DvBox></DvScreen>", vm);

        Tap(s, new SKPoint(200, 25)); // 부모(100×100) 밖, 자식은 보임
        Assert.Equal(new[] { "Hit" }, vm.Calls);

        Render(s).Dispose();
        s.PointerMove(new SKPoint(200, 25));
        using var bmp = Render(s);
        Assert.Equal(new SKColor(0xff, 0, 0), bmp.GetPixel(200, 25));
    }

    // 삐져나온 자식 위에 나중에 그려진 형제가 겹치면 위에 보이는 형제가 받는다
    [Fact]
    public void C1c_later_sibling_drawn_over_overflowing_child_wins()
    {
        var vm = new Vm();
        var s = Surface("<DvScreen style=\"padding:0; gap:0\">" +
                        "<DvBox style=\"width:100; height:100; padding:0\">" +
                        "<DvBox onclick=\"Under\" style=\"position:absolute; left:0; top:0; width:300; height:300\"/></DvBox>" +
                        "<DvBox onclick=\"Over\" style=\"width:100; height:100\"/></DvScreen>", vm);

        var over = Flatten(s.Root!).First(b => b.OnClick == "Over");
        Tap(s, Mid(over));
        Assert.Equal(new[] { "Over" }, vm.Calls);
    }

    [Fact]
    public void C2_child_scrolled_out_of_viewport_is_not_hit_from_sibling_area()
    {
        var vm = new Vm();
        var s = Surface("<DvScreen style=\"padding:0; gap:0; align-items:stretch\">" +
                        "<DvBox style=\"height:50\"/>" +
                        "<DvStack style=\"overflow:scroll; height:100; padding:0; gap:0; align-items:stretch\">" +
                        "<DvButton onclick=\"H0\" style=\"height:50\"/><DvButton onclick=\"H1\" style=\"height:50\"/>" +
                        "<DvButton onclick=\"H2\" style=\"height:50\"/><DvButton onclick=\"H3\" style=\"height:50\"/></DvStack>" +
                        "<DvBox style=\"height:200\"/></DvScreen>", vm);
        var scroller = s.Root!.Children[1];
        Assert.Equal(50, scroller.Rect.Top);

        Tap(s, new SKPoint(100, 175)); // 아래 형제 — 스크롤 상자 내용으로는 H2 자리
        s.PointerWheel(new SKPoint(100, 100), -1); // 아래로 48
        Tap(s, new SKPoint(100, 25));  // 위 형제 — 스크롤 오프셋으로 밀려 올라간 H0 자리
        Assert.Empty(vm.Calls);

        Tap(s, new SKPoint(100, 60)); // 뷰포트 맨 위 = 내용 y 58 → H1
        Assert.Equal(new[] { "H1" }, vm.Calls);
    }

    [Fact]
    public void C2_grid_rows_scrolled_under_header_or_below_body_are_not_hit()
    {
        var vm = new GridVm();
        var s = Surface("<DvScreen style=\"padding:0; gap:0; display:flex; flex-direction:column; align-items:stretch\">" +
                        GridMarkup() + "<DvBox onclick=\"Hit\" style=\"height:100\"/></DvScreen>", vm);
        var grid = Find(s, "DvDataGrid");
        grid.ScrollY = 100;

        Tap(s, new SKPoint(110, 20));   // 헤더(이름 열 — 정렬 불가) 아래로 밀려 올라간 행
        Tap(s, new SKPoint(210, 20));
        Assert.Empty(vm.Calls);
        Assert.False(s.Edit.IsEditing);
        Assert.Null(s.TopPopup);

        Tap(s, new SKPoint(30, 260));   // 그리드 아래 형제 — 몸통 밖 행 자리
        Assert.Equal(new[] { "Hit" }, vm.Calls);
        Assert.Empty(vm.Picked);
    }

    [Fact]
    public void C3_invisible_element_has_no_box_and_gets_no_click()
    {
        var vm = new Vm();
        var s = Surface("<DvScreen style=\"padding:0\"><DvButton onclick=\"Hit\" visible=\"false\" style=\"width:100; height:40\"/></DvScreen>", vm);
        Assert.DoesNotContain(Flatten(s.Root!), b => b.OnClick == "Hit");
        Tap(s, new SKPoint(50, 20));
        Assert.Empty(vm.Calls);
    }

    [Fact]
    public void C4_absolute_overlay_painted_on_top_receives_the_click()
    {
        var vm = new Vm();
        var s = Surface("<DvScreen style=\"padding:0\"><DvBox style=\"width:200; height:200; padding:0\">" +
                        "<DvBox onclick=\"Under\" style=\"width:200; height:200; background:#0000ff\"/>" +
                        "<DvBox onclick=\"Over\" style=\"position:absolute; left:0; top:0; width:100; height:100; background:#ff0000\"/>" +
                        "</DvBox></DvScreen>", vm);

        using (var bmp = Render(s))
            Assert.Equal(new SKColor(0xff, 0, 0), bmp.GetPixel(50, 50));
        Tap(s, new SKPoint(50, 50));
        Tap(s, new SKPoint(150, 150));
        Assert.Equal(new[] { "Over", "Under" }, vm.Calls);
    }

    [Fact]
    public void C5_element_under_open_dropdown_is_not_clicked()
    {
        var vm = new Vm();
        var s = Surface("<DvScreen style=\"padding:0; gap:0; align-items:stretch\">" +
                        "<DvSelect value=\"{Bind Mode}\" style=\"height:40\"><DvOption value=\"x\" text=\"X\"/><DvOption value=\"y\" text=\"Y\"/>" +
                        "<DvOption value=\"z\" text=\"Z\"/></DvSelect>" +
                        "<DvBox onclick=\"Hit\" style=\"height:300\"/></DvScreen>", vm);
        Tap(s, Mid(Find(s, "DvSelect")));
        var popup = s.TopPopup!;
        Render(s).Dispose();

        Tap(s, new SKPoint(popup.Bounds.MidX, popup.Bounds.Bottom - 2)); // 목록 위 = 그 아래 상자는 안 눌림
        Assert.Empty(vm.Calls);

        if (s.TopPopup is null)
            Tap(s, Mid(Find(s, "DvSelect")));
        Assert.NotNull(s.TopPopup);
        Tap(s, new SKPoint(W - 5, H - 60)); // 팝업 밖 = 닫기만
        Assert.Null(s.TopPopup);
        Assert.Empty(vm.Calls);
    }

    [Fact]
    public void C5_element_under_virtual_keyboard_is_not_clicked()
    {
        var vm = new Vm();
        var s = Surface("<DvScreen style=\"padding:0; gap:0; align-items:stretch\">" +
                        "<DvInput value=\"{Bind Name}\" style=\"height:40\"/>" +
                        "<DvBox onclick=\"Hit\" style=\"height:360\"/></DvScreen>", vm, native: false);
        Tap(s, Mid(Find(s, "DvInput")));
        Assert.True(s.Edit.IsEditing);

        Tap(s, new SKPoint(W / 2, H - 5));
        Assert.Empty(vm.Calls);
    }

    // ── D. hover 도 같은 원칙 — 포인터가 떠난(가려진) 상자에 hover 가 남지 않음

    [Fact]
    public void D_hover_does_not_stay_on_select_after_pointer_moves_into_its_popup()
    {
        var s = Surface("<DvScreen style=\"padding:0; gap:0; align-items:stretch\">" +
                        "<DvSelect value=\"{Bind Mode}\" style=\"height:40; background:#0000ff\" hover-style=\"background:#ff0000\">" +
                        "<DvOption value=\"x\" text=\"X\"/><DvOption value=\"y\" text=\"Y\"/></DvSelect></DvScreen>", new Vm());
        var sel = Find(s, "DvSelect").Rect;
        s.PointerMove(new SKPoint(sel.Right - 30, sel.MidY));
        Tap(s, new SKPoint(sel.Right - 30, sel.MidY));
        var popup = s.TopPopup!;
        s.PointerMove(new SKPoint(popup.Bounds.MidX, popup.Bounds.Bottom - 2));

        using var bmp = Render(s);
        Assert.Equal(new SKColor(0, 0, 0xff), bmp.GetPixel((int)sel.Left + 4, (int)sel.Top + 4));
    }

    [Fact]
    public void D_hover_is_cleared_when_pointer_crosses_between_app_slots()
    {
        const string hoverBox = "<DvBox style=\"width:200; height:400; background:#0000ff\" hover-style=\"background:#ff0000\"/>";
        var app = new Devinno.UI.App.DvApp();
        app.Pages.Add(new MarkupPage("<DvScreen style=\"padding:0\">" + hoverBox + "</DvScreen>"));
        app.Left = new MarkupBar("<DvStack style=\"width:200; padding:0; gap:0\">" + hoverBox + "</DvStack>");
        var s = app.Surface;
        var blue = new SKColor(0, 0, 0xff);

        Render(s).Dispose();
        s.PointerMove(new SKPoint(100, 100)); // 왼쪽 영역 상자 hover
        s.PointerMove(new SKPoint(300, 100)); // 페이지 칸으로 — 영역 상자 hover 해제, 페이지 상자 hover
        using (var bmp = Render(s))
        {
            Assert.Equal(blue, bmp.GetPixel(100, 10));
            Assert.Equal(new SKColor(0xff, 0, 0), bmp.GetPixel(300, 10));
        }

        s.PointerMove(new SKPoint(100, 100)); // 영역으로 — 페이지 상자 hover 해제
        using (var bmp = Render(s))
            Assert.Equal(blue, bmp.GetPixel(300, 10));
    }

    [Fact]
    public void D_wheel_scroll_moves_hover_to_the_box_now_under_the_pointer()
    {
        const string btn = "<DvBox onclick=\"Hit\" style=\"height:100; background:#0000ff\" hover-style=\"background:#ff0000\"/>";
        var s = Surface("<DvScreen style=\"padding:0\"><DvStack style=\"overflow:scroll; width:300; height:200; padding:0; gap:0; align-items:stretch\">" +
                        btn + btn + btn + btn + "</DvStack></DvScreen>", new Vm());
        Render(s).Dispose();
        s.PointerMove(new SKPoint(100, 80));       // 첫 상자 hover
        s.PointerWheel(new SKPoint(100, 80), -1);   // 48 내림 — 포인터 밑은 이제 둘째 상자(내용 y 128)
        Assert.True(s.NeedsRender);

        using var bmp = Render(s);
        Assert.Equal(new SKColor(0, 0, 0xff), bmp.GetPixel(100, 10));  // 첫 상자의 남은 부분 — hover 아님
        Assert.Equal(new SKColor(0xff, 0, 0), bmp.GetPixel(100, 80));  // 둘째 상자 — hover
    }
}
