using Devinno.UI.Layout;

namespace Devinno.UI.App;

/// <summary>The built-in item selection dialog shown by <see cref="DvApp.SelectBox"/>. It is itself the VM of its markup.</summary>
internal sealed class DvSelectBoxDialog : DvOkCancelDialog<int?>
{
    // 행은 최소 40 — 긴 항목은 줄바꿈되어 행이 그만큼 높아진다.
    // 목록 높이 = 행 내용 높이, 최대 320. 창이 낮아 다이얼로그 칸 높이가 잘리면 스크롤 상자인 목록이 줄어들어(flex shrink) 버튼이 칸 안에 남는다.
    private const string Markup = """
        <DvDialogFrame title="{Caption}" style="width:360">
          <DvStyle>
            .sel-row { min-height: 40; padding: 0 12; content-align: middle-left; border-radius: token(radius.sm); color: token(text); font-size: token(font.body) }
            .sel-row:hover { background: token(surface-raised) }
            .selected, .selected:hover { background: token(primary); color: token(primary-fg) }
          </DvStyle>
          <DvRepeat items="{Rows}" gap="none" style="max-height:320; overflow:scroll">
            <DvLabel class="{Item.Class}" text="{Item.Text}" onclick="Pick"/>
          </DvRepeat>
          <DvRow gap="sm">
            <DvSpacer/>
            <DvButton text="{CancelText}" variant="secondary" onclick="PressCancel"/>
            <DvButton text="{OkText}" variant="primary" onclick="PressOk" disabled="{NothingSelected}"/>
          </DvRow>
        </DvDialogFrame>
        """;

    private readonly string[] _items;
    private int _selected;

    public DvSelectBoxDialog(string caption, IReadOnlyList<string> items, int selected)
    {
        Caption = caption;
        _items = [.. items];
        _selected = selected >= 0 && selected < _items.Length ? selected : -1;
        Rows = _items.Select((text, i) => new SelectRow(this, i, text)).ToList();
    }

    protected override string MarkupSource => Markup;

    public string Caption { get; }

    public IReadOnlyList<SelectRow> Rows { get; }

    // 처음 고른 행이 목록 밖이면 보이게 스크롤(첫 칸 배치 한 번)
    internal override bool AfterFirstLayout()
    {
        if (_selected >= 0 && Surface is { Root: { } root } s && FindList(root) is { } list && _selected < list.Children.Count)
            s.ScrollIntoView(list.Children[_selected]);
        return true;
    }

    private static LayoutBox? FindList(LayoutBox b)
    {
        if (b.Node.Tag == "DvRepeat")
            return b;
        foreach (var c in b.Children)
            if (FindList(c) is { } f)
                return f;
        return null;
    }

    public bool NothingSelected => _selected < 0;

    public void Pick(SelectRow row) => _selected = row.Index;

    public void PressOk()
    {
        if (_selected >= 0)
            Close(_selected);
    }

    /// <summary>One row of the list: its text and a class that depends on whether it is selected.</summary>
    internal sealed class SelectRow(DvSelectBoxDialog owner, int index, string text)
    {
        public int Index { get; } = index;

        public string Text { get; } = text;

        public string Class => owner._selected == Index ? "sel-row selected" : "sel-row";
    }
}
