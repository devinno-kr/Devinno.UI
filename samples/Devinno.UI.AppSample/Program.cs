using Devinno.UI.App;
using Devinno.UI.OpenTK;
using Devinno.UI.Styling;

var app = new DvApp { Theme = DvTheme.Dark };
app.Top = new TopBar();                 // fixed area at the top (optional)
app.Pages.Add(new HomePage());          // the first page added = start page
app.Pages.Add(new MonitorPage());

using var window = new ViewWindow(app, 1024, 640, "App Sample");
window.Run();
