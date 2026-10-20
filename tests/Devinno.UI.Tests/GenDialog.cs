using Devinno.UI.App;
using Devinno.UI.Controls;
using Devinno.UI;
using Xunit;

namespace GenDialogTest
{
    // 실제 생성기로 컴파일되는 다이얼로그 — Pages/GenDialog.dvml
    public sealed partial class GenDialog : DvDialog<string>
    {
        public int Loaded;
        public DvButton? Handle => btnOk;
        partial void OnLoaded() => Loaded++;
    }
}

// 생성기 통합 — DvDialog<string> 하위 vm 이 MarkupSource 를 얻어 실제로 뜨고 핸들이 묶인다.
[Collection("AttachedApp")]
public class GenDialogTests
{
    private sealed class Page : DvPage
    {
        protected override string MarkupSource => "<DvScreen style=\"padding:0; gap:0\"><DvLabel text=\"p\"/></DvScreen>";
    }

    [Fact]
    public void GenDialog_ShowsAndBindsHandle()
    {
        var app = new DvApp();
        app.Pages.Add(new Page());
        var root = app.Surface;
        root.Layout(800, 600);

        app.ShowDialog<GenDialogTest.GenDialog, string>();
        var d = Assert.IsType<GenDialogTest.GenDialog>(app.Dialogs.Top);
        Assert.Null(d.Handle);

        root.Layout(800, 600);
        Assert.NotNull(d.Handle);
        Assert.Equal(1, d.Loaded);
        root.Layout(800, 600);
        Assert.Equal(1, d.Loaded);
    }
}
