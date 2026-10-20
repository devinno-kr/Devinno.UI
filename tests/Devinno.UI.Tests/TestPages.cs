using Devinno.UI;
using Devinno.UI.App;

// 테스트용 페이지·틀 — 소스 생성기 대신 마크업을 직접 받는다(페이지·틀 자신이 마크업의 VM).
public class MarkupPage(string markup) : DvPage
{
    protected override string MarkupSource => markup;
}

public class MarkupBar(string markup) : DvBar
{
    protected override string MarkupSource => markup;
}

// 테스트용 다이얼로그 — 생성기 대신 마크업을 직접 받는다. Close 는 protected 이므로 테스트가 부를 수 있게 감싼다.
public class MarkupDialog<T>(string markup) : DvDialog<T>
{
    protected override string MarkupSource => markup;

    public void Done(T? result) => Close(result);

    public void Cancel() => Close();
}

internal static class TestApp
{
    /// <summary>왼쪽 빈 틀(폭 <paramref name="leftWidth"/>) + 페이지 하나인 앱의 루트 화면을 w×h 로 레이아웃해 돌려준다(페이지 칸 = x leftWidth 부터).</summary>
    public static (DvApp App, DvSurface Root) WithLeft(DvPage page, float leftWidth, float w, float h, bool native = false)
    {
        var app = new DvApp();
        app.Pages.Add(page);
        app.Left = new MarkupBar($"<DvStack style=\"width:{leftWidth}; padding:0\"/>");
        var root = app.Surface;
        root.UseNativeEditor = native;
        root.Layout(w, h);
        return (app, root);
    }
}
