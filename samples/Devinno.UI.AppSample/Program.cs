using Devinno.UI.App;
using Devinno.UI.OpenTK;
using Devinno.UI.Styling;

var app = new DvApp { Theme = DvTheme.Dark };
app.Top = new TopBar();                 // 상단 고정 영역(선택)
app.Pages.Add(new HomePage());          // 처음 넣은 페이지 = 시작 페이지
app.Pages.Add(new MonitorPage());

using var window = new ViewWindow(app, 1024, 640, "App Sample");
window.Run();
