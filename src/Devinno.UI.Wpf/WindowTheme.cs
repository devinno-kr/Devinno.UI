using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using Devinno.UI.Styling;

namespace Devinno.UI.Wpf;

// ViewControl.ThemeWindow 의 실제 작업 — 맨 위 창의 타이틀바·테두리(DWM 속성)와 WPF 메뉴(MenuTheme.xaml 스타일 + 테마 색 브러시).
// 창 모양은 OS 것 그대로 두고 색만 바꾼다(최소화·최대화·닫기, 스냅, 그림자, 크기 조절은 OS 가 계속 처리).
internal sealed class WindowTheme
{
    // DWMWINDOWATTRIBUTE — 34~36 은 Windows 11(22000) 부터, 20 은 Windows 10 20H1 부터(그 전 1809~1909 는 19)
    private const int DwmwaUseImmersiveDarkModeOld = 19;
    private const int DwmwaUseImmersiveDarkMode = 20;
    private const int DwmwaBorderColor = 34;
    private const int DwmwaCaptionColor = 35;
    private const int DwmwaTextColor = 36;
    private const int DwmwaColorDefault = unchecked((int)0xFFFFFFFF);
    private const uint GaRoot = 2;

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

    [DllImport("user32.dll")]
    private static extern IntPtr GetAncestor(IntPtr hwnd, uint flags);

    private readonly FrameworkElement _owner;
    private IntPtr _hwnd;               // 색을 입힌 맨 위 창
    private DvTheme? _theme;            // 그때의 테마
    private ResourceDictionary? _menus; // 앱(또는 창) 리소스에 넣은 메뉴 스타일
    private ResourceDictionary? _menusHost;

    public WindowTheme(FrameworkElement owner) => _owner = owner;

    // 창·테마가 지난번과 다를 때만 다시 입힌다 — 앱 요청마다 불리므로 같으면 바로 돌아간다. 아직 창에 안 올라갔으면 Loaded 에서 다시 불린다
    public void Sync(DvTheme? appTheme)
    {
        var theme = appTheme ?? DvTheme.Default;
        var hwnd = PresentationSource.FromVisual(_owner) is HwndSource source ? GetAncestor(source.Handle, GaRoot) : IntPtr.Zero;
        if (hwnd == _hwnd && ReferenceEquals(theme, _theme))
            return;
        if (hwnd != _hwnd && _hwnd != IntPtr.Zero)
            ResetFrame(_hwnd);
        _hwnd = hwnd;
        _theme = theme;
        if (hwnd != IntPtr.Zero)
            ApplyFrame(hwnd, theme);
        ApplyMenus(theme);
    }

    public void Release()
    {
        if (_hwnd != IntPtr.Zero)
            ResetFrame(_hwnd);
        if (_menus is not null)
            _menusHost?.MergedDictionaries.Remove(_menus);
        (_hwnd, _theme, _menus, _menusHost) = (IntPtr.Zero, null, null, null);
    }

    // 테마 토큰 → 색. 없거나 못 읽으면 fallback(복사해 고친 테마에 토큰이 빠져도 메뉴가 깨지지 않게)
    internal static Color Get(DvTheme theme, string token, Color fallback)
        => theme.TryResolve(token, out var raw) && DvColor.TryParse(raw, out var c) ? Color.FromArgb(c.A, c.R, c.G, c.B) : fallback;

    // 배경이 어두운 테마인가 — 타이틀바 버튼 글리프 색과 Windows 10 의 검정/흰색 선택에 쓴다
    internal static bool IsDark(DvTheme theme)
    {
        var bg = Get(theme, "background", Colors.White);
        return (0.2126 * bg.R + 0.7152 * bg.G + 0.0722 * bg.B) / 255.0 < 0.5;
    }

    private static int ColorRef(Color c) => c.R | (c.G << 8) | (c.B << 16); // COLORREF = 0x00BBGGRR

    private static bool Set(IntPtr hwnd, int attribute, int value) => DwmSetWindowAttribute(hwnd, attribute, ref value, sizeof(int)) == 0;

    private static void ApplyFrame(IntPtr hwnd, DvTheme theme)
    {
        var dark = IsDark(theme) ? 1 : 0;
        if (!Set(hwnd, DwmwaUseImmersiveDarkMode, dark))
            Set(hwnd, DwmwaUseImmersiveDarkModeOld, dark);
        Set(hwnd, DwmwaCaptionColor, ColorRef(Get(theme, "background", SystemColors.ActiveCaptionColor)));
        Set(hwnd, DwmwaTextColor, ColorRef(Get(theme, "text", SystemColors.ActiveCaptionTextColor)));
        Set(hwnd, DwmwaBorderColor, ColorRef(Get(theme, "border", SystemColors.ActiveBorderColor)));
    }

    private static void ResetFrame(IntPtr hwnd)
    {
        if (!Set(hwnd, DwmwaUseImmersiveDarkMode, 0))
            Set(hwnd, DwmwaUseImmersiveDarkModeOld, 0);
        Set(hwnd, DwmwaCaptionColor, DwmwaColorDefault);
        Set(hwnd, DwmwaTextColor, DwmwaColorDefault);
        Set(hwnd, DwmwaBorderColor, DwmwaColorDefault);
    }

    // 메뉴 막대는 타이틀바와 같은 background 로 이어 붙이고, 펼친 메뉴는 surface 판, 가리킨 항목은 surface-raised
    private void ApplyMenus(DvTheme theme)
    {
        var host = Application.Current?.Resources ?? (Window.GetWindow(_owner) ?? (FrameworkElement)_owner).Resources;
        if (_menus is null || !ReferenceEquals(host, _menusHost))
        {
            if (_menus is not null)
                _menusHost?.MergedDictionaries.Remove(_menus);
            _menus = new ResourceDictionary { Source = new Uri("pack://application:,,,/Devinno.UI.Wpf;component/MenuTheme.xaml", UriKind.Absolute) };
            _menusHost = host;
            host.MergedDictionaries.Add(_menus);
        }

        SetBrush("Dv.Menu.Bar", Get(theme, "background", SystemColors.MenuBarColor));
        SetBrush("Dv.Menu.Panel", Get(theme, "surface", SystemColors.MenuColor));
        SetBrush("Dv.Menu.Hover", Get(theme, "surface-raised", SystemColors.MenuHighlightColor));
        SetBrush("Dv.Menu.Border", Get(theme, "border", SystemColors.ActiveBorderColor));
        SetBrush("Dv.Menu.Text", Get(theme, "text", SystemColors.MenuTextColor));
        SetBrush("Dv.Menu.Disabled", Get(theme, "text-subtle", SystemColors.GrayTextColor));
        SetBrush("Dv.Menu.Check", Get(theme, "primary", SystemColors.HighlightColor));
        SetBrush("Dv.Menu.Status", Get(theme, "background-alt", SystemColors.ControlColor));
    }

    private void SetBrush(string key, Color color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        _menus![key] = brush; // DynamicResource 가 새 브러시를 따라간다
    }
}
