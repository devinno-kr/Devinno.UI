using Devinno.UI.App;

// 쇼케이스 3 — 위젯별 데모 페이지 목록. 카드를 누르면 그 페이지로(돌아올 때는 상단 바 Back 또는 Widgets).
// 각 페이지는 실행 인자로 따로 띄울 수도 있다(Program.cs 의 -- grid 등).
public sealed partial class WidgetsPage(IReadOnlyList<WidgetsPage.Entry> entries) : DvPage
{
    public sealed record Entry(string Icon, string Title, string Description, DvPage Page);

    public IReadOnlyList<Entry> Entries => entries;

    public void Open(Entry e) => App.Show(e.Page);
}
