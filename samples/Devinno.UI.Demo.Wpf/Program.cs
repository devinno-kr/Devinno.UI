using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Devinno.UI.App;
using Devinno.UI.Localization;
using Devinno.UI.Styling;
using Devinno.UI.Wpf;

// WPF 어댑터 데모 — 같은 InputPage 화면을 WPF 편집 상자(기본) 또는 가상 키보드(touch)로 편집.
// 메뉴 막대·오른쪽 클릭 메뉴와 타이틀바는 ThemeWindow 로 테마 색을 따른다(보기 메뉴에서 테마 바꾸기).
//   dotnet run --project samples/Devinno.UI.Demo.Wpf [-- touch] [-- light]
internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        var app = new DvApp { Theme = args.Contains("light") ? DvTheme.Light : DvTheme.Dark, Strings = new DvStrings { Language = "en" } }; // 데모 화면이 영어 — 라이브러리 기본 글자도 en
        app.Pages.Add(new InputPage());
        var view = new ViewControl { App = app, TouchMode = args.Contains("touch"), ThemeWindow = true };

        var menu = new Menu();
        var file = new MenuItem { Header = "_File" };
        file.Items.Add(new MenuItem { Header = "_New", InputGestureText = "Ctrl+N" });
        file.Items.Add(new MenuItem { Header = "_Open…", InputGestureText = "Ctrl+O" });
        file.Items.Add(new Separator());
        var exit = new MenuItem { Header = "E_xit" };
        file.Items.Add(exit);
        var viewMenu = new MenuItem { Header = "_View" };
        foreach (var (name, theme) in new[] { ("Dark", DvTheme.Dark), ("Light", DvTheme.Light), ("Graphite", DvTheme.Graphite), ("Carbon", DvTheme.Carbon) })
        {
            var item = new MenuItem { Header = name, IsCheckable = true, IsChecked = ReferenceEquals(app.Theme, theme) };
            item.Click += (_, _) =>
            {
                app.Theme = theme;
                foreach (MenuItem other in viewMenu.Items)
                    other.IsChecked = ReferenceEquals(other, item);
            };
            viewMenu.Items.Add(item);
        }
        menu.Items.Add(file);
        menu.Items.Add(viewMenu);

        var context = new ContextMenu();
        context.Items.Add(new MenuItem { Header = "Cut", InputGestureText = "Ctrl+X" });
        context.Items.Add(new MenuItem { Header = "Copy", InputGestureText = "Ctrl+C" });
        context.Items.Add(new MenuItem { Header = "Paste", InputGestureText = "Ctrl+V", IsEnabled = false });
        menu.ContextMenu = context; // 화면은 오른쪽 누름을 앱에 넘기므로 메뉴 막대에 둔다

        var root = new DockPanel();
        DockPanel.SetDock(menu, Dock.Top);
        root.Children.Add(menu);
        root.Children.Add(view);

        var window = new Window
        {
            Title = "Devinno.UI Demo — WPF" + (view.TouchMode ? " (touch)" : ""),
            Content = root,
            Width = InputPage.Width,
            Height = InputPage.Height,
            WindowStartupLocation = WindowStartupLocation.CenterScreen
        };
        window.SizeToContent = SizeToContent.Manual;
        exit.Click += (_, _) => window.Close();
        new Application().Run(window);
    }
}
