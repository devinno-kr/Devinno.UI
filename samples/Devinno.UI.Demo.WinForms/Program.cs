using Devinno.UI.App;
using Devinno.UI.Localization;
using Devinno.UI.Styling;
using Devinno.UI.WinForms;

// WinForms 어댑터 데모 — 같은 InputPage 화면을 숨긴 TextBox(기본) 또는 가상 키보드(touch)로 편집.
//   dotnet run --project Devinno.UI.Demo.WinForms [-- touch] [-- light]
internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize();

        var theme = args.Contains("light") ? DvTheme.Light : DvTheme.Dark;
        var app = new DvApp { Theme = theme, Strings = new DvStrings { Language = "ko" } }; // 데모 화면이 한국어 — 라이브러리 기본 글자도 ko
        app.Pages.Add(new InputPage());
        var view = new ViewControl
        {
            Dock = DockStyle.Fill,
            TouchMode = args.Contains("touch"),
            App = app
        };

        // 크기는 100% 기준(논리 px). 디자이너 코드처럼 SuspendLayout~ResumeLayout 안에서 AutoScaleMode.Dpi 를 주면
        // 배율에 맞춰 창이 커지고, 실행 중 배율이 바뀌어도(다른 모니터로 옮김) 창이 따라간다
        var form = new Form();
        form.SuspendLayout();
        form.AutoScaleDimensions = new SizeF(96f, 96f);
        form.AutoScaleMode = AutoScaleMode.Dpi;
        form.ClientSize = new Size(InputPage.Width, InputPage.Height);
        form.Text = "Devinno.UI Demo — WinForms" + (view.TouchMode ? " (touch)" : "");
        form.StartPosition = FormStartPosition.CenterScreen;
        form.Controls.Add(view);
        form.ResumeLayout(false);
        Application.Run(form);
    }
}
