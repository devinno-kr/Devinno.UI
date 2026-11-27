using System.Runtime.InteropServices;
using Devinno.UI.Styling;

namespace Devinno.UI.WinForms;

// ViewControl.ThemeWindow 의 실제 작업 — 폼 타이틀바·테두리(DWM 속성)와 WinForms 메뉴(ToolStripManager.Renderer)를 테마 토큰 색으로.
// 창 모양은 OS 것 그대로 두고 색만 바꾼다(최소화·최대화·닫기, 스냅, 그림자, 크기 조절은 OS 가 계속 처리).
internal static class WindowTheme
{
    // DWMWINDOWATTRIBUTE — 34~36 은 Windows 11(22000) 부터, 20 은 Windows 10 20H1 부터(그 전 1809~1909 는 19)
    private const int DwmwaUseImmersiveDarkModeOld = 19;
    private const int DwmwaUseImmersiveDarkMode = 20;
    private const int DwmwaBorderColor = 34;
    private const int DwmwaCaptionColor = 35;
    private const int DwmwaTextColor = 36;
    private const int DwmwaColorDefault = unchecked((int)0xFFFFFFFF);

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

    // 테마 토큰 → 색. 없거나 못 읽으면 fallback(복사해 고친 테마에 토큰이 빠져도 메뉴가 깨지지 않게)
    internal static Color Get(DvTheme theme, string token, Color fallback)
        => theme.TryResolve(token, out var raw) && DvColor.TryParse(raw, out var c) ? Color.FromArgb(c.A, c.R, c.G, c.B) : fallback;

    // 배경이 어두운 테마인가 — 타이틀바 버튼(최소화·최대화·닫기) 글리프 색과 Windows 10 의 검정/흰색 선택에 쓴다
    internal static bool IsDark(DvTheme theme)
    {
        var bg = Get(theme, "background", Color.White);
        return (0.2126 * bg.R + 0.7152 * bg.G + 0.0722 * bg.B) / 255.0 < 0.5;
    }

    private static int ColorRef(Color c) => c.R | (c.G << 8) | (c.B << 16); // COLORREF = 0x00BBGGRR

    private static bool Set(IntPtr hwnd, int attribute, int value) => DwmSetWindowAttribute(hwnd, attribute, ref value, sizeof(int)) == 0;

    // 폼 타이틀바·테두리를 테마 색으로. 지원하지 않는 Windows 에서는 해당 속성만 조용히 실패한다(Windows 10: 검정/흰색만)
    internal static void ApplyFrame(Form form, DvTheme theme)
    {
        if (!form.IsHandleCreated)
            return; // 폼 HandleCreated 에서 다시 부른다
        var hwnd = form.Handle;
        var dark = IsDark(theme) ? 1 : 0;
        if (!Set(hwnd, DwmwaUseImmersiveDarkMode, dark))
            Set(hwnd, DwmwaUseImmersiveDarkModeOld, dark);
        Set(hwnd, DwmwaCaptionColor, ColorRef(Get(theme, "background", SystemColors.ActiveCaption)));
        Set(hwnd, DwmwaTextColor, ColorRef(Get(theme, "text", SystemColors.ActiveCaptionText)));
        Set(hwnd, DwmwaBorderColor, ColorRef(Get(theme, "border", SystemColors.ActiveBorder)));
    }

    // OS 기본 타이틀바로 되돌린다
    internal static void ResetFrame(Form form)
    {
        if (!form.IsHandleCreated)
            return;
        var hwnd = form.Handle;
        if (!Set(hwnd, DwmwaUseImmersiveDarkMode, 0))
            Set(hwnd, DwmwaUseImmersiveDarkModeOld, 0);
        Set(hwnd, DwmwaCaptionColor, DwmwaColorDefault);
        Set(hwnd, DwmwaTextColor, DwmwaColorDefault);
        Set(hwnd, DwmwaBorderColor, DwmwaColorDefault);
    }

    // 앱의 모든 MenuStrip·ContextMenuStrip·ToolStrip·StatusStrip(RenderMode = ManagerRenderMode, 기본값)이 따른다
    internal static void ApplyMenus(DvTheme theme) => ToolStripManager.Renderer = new MenuRenderer(theme);

    // 이 클래스가 넣은 렌더러일 때만 기본(Professional)으로 되돌린다 — 앱이 직접 넣은 렌더러는 건드리지 않는다
    internal static void ResetMenus()
    {
        if (ToolStripManager.Renderer is MenuRenderer)
            ToolStripManager.RenderMode = ToolStripManagerRenderMode.Professional;
    }

    // 메뉴 막대는 타이틀바와 같은 background 로 이어 붙이고, 펼친 메뉴는 surface 패널, 가리킨 항목은 surface-raised
    internal sealed class MenuRenderer : ToolStripProfessionalRenderer
    {
        private readonly Color _text;
        private readonly Color _disabled;
        private readonly Color _check;

        public MenuRenderer(DvTheme theme) : base(new Palette(theme))
        {
            RoundedEdges = false;
            _text = Get(theme, "text", SystemColors.MenuText);
            _disabled = Get(theme, "text-subtle", SystemColors.GrayText);
            _check = Get(theme, "primary", SystemColors.Highlight);
        }

        protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
        {
            e.TextColor = e.Item.Enabled ? _text : _disabled; // 단축키 글자(Ctrl+N)도 이 색으로 그려진다
            base.OnRenderItemText(e);
        }

        protected override void OnRenderArrow(ToolStripArrowRenderEventArgs e)
        {
            e.ArrowColor = e.Item?.Enabled != false ? _text : _disabled;
            base.OnRenderArrow(e);
        }

        // 기본 체크 표시는 검은 그림이라 어두운 테마에서 안 보인다 — 바탕 위에 브랜드 색 체크 선을 직접 그린다
        protected override void OnRenderItemCheck(ToolStripItemImageRenderEventArgs e)
        {
            var r = e.ImageRectangle;
            if (r.Width <= 0 || r.Height <= 0)
                return;
            using (var bg = new SolidBrush(ColorTable.CheckBackground))
                e.Graphics.FillRectangle(bg, r);
            var s = Math.Min(r.Width, r.Height);
            var x = r.Left + (r.Width - s) / 2f;
            var y = r.Top + (r.Height - s) / 2f;
            var mode = e.Graphics.SmoothingMode;
            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            using (var pen = new Pen(e.Item?.Enabled != false ? _check : _disabled, Math.Max(1.5f, s / 9f)))
                e.Graphics.DrawLines(pen, [new PointF(x + s * 0.22f, y + s * 0.52f), new PointF(x + s * 0.42f, y + s * 0.72f), new PointF(x + s * 0.78f, y + s * 0.3f)]);
            e.Graphics.SmoothingMode = mode;
        }
    }

    private sealed class Palette : ProfessionalColorTable
    {
        private readonly Color _bar;      // 메뉴 막대·도구 막대 = 타이틀바와 같은 바탕
        private readonly Color _panel;    // 펼친 메뉴 바탕
        private readonly Color _hover;    // 가리킨 항목
        private readonly Color _press;    // 열린 맨 위 메뉴 항목
        private readonly Color _border;
        private readonly Color _status;
        private readonly Color _checkBg;

        public Palette(DvTheme theme)
        {
            UseSystemColors = false;
            _bar = Get(theme, "background", SystemColors.Menu);
            _panel = Get(theme, "surface", SystemColors.Menu);
            _hover = Get(theme, "surface-raised", SystemColors.MenuHighlight);
            _press = Get(theme, "surface", SystemColors.Menu);
            _border = Get(theme, "border", SystemColors.ActiveBorder);
            _status = Get(theme, "background-alt", SystemColors.Control);
            _checkBg = Get(theme, "surface-overlay", SystemColors.ControlLight);
        }

        public override Color MenuStripGradientBegin => _bar;
        public override Color MenuStripGradientEnd => _bar;
        public override Color ToolStripGradientBegin => _bar;
        public override Color ToolStripGradientMiddle => _bar;
        public override Color ToolStripGradientEnd => _bar;
        public override Color ToolStripBorder => _border;
        public override Color ToolStripPanelGradientBegin => _bar;
        public override Color ToolStripPanelGradientEnd => _bar;
        public override Color ToolStripContentPanelGradientBegin => _bar;
        public override Color ToolStripContentPanelGradientEnd => _bar;
        public override Color OverflowButtonGradientBegin => _bar;
        public override Color OverflowButtonGradientMiddle => _bar;
        public override Color OverflowButtonGradientEnd => _bar;
        public override Color GripDark => _border;
        public override Color GripLight => _bar;
        public override Color StatusStripGradientBegin => _status;
        public override Color StatusStripGradientEnd => _status;

        public override Color ToolStripDropDownBackground => _panel;
        public override Color ImageMarginGradientBegin => _panel;
        public override Color ImageMarginGradientMiddle => _panel;
        public override Color ImageMarginGradientEnd => _panel;
        public override Color MenuBorder => _border;
        public override Color SeparatorDark => _border;
        public override Color SeparatorLight => _panel;

        public override Color MenuItemSelected => _hover;
        public override Color MenuItemSelectedGradientBegin => _hover;
        public override Color MenuItemSelectedGradientEnd => _hover;
        public override Color MenuItemBorder => _hover;
        public override Color MenuItemPressedGradientBegin => _press;
        public override Color MenuItemPressedGradientMiddle => _press;
        public override Color MenuItemPressedGradientEnd => _press;

        public override Color ButtonSelectedHighlight => _hover;
        public override Color ButtonSelectedGradientBegin => _hover;
        public override Color ButtonSelectedGradientMiddle => _hover;
        public override Color ButtonSelectedGradientEnd => _hover;
        public override Color ButtonSelectedBorder => _hover;
        public override Color ButtonPressedHighlight => _checkBg;
        public override Color ButtonPressedGradientBegin => _checkBg;
        public override Color ButtonPressedGradientMiddle => _checkBg;
        public override Color ButtonPressedGradientEnd => _checkBg;
        public override Color ButtonPressedBorder => _checkBg;
        public override Color ButtonCheckedHighlight => _checkBg;
        public override Color ButtonCheckedGradientBegin => _checkBg;
        public override Color ButtonCheckedGradientMiddle => _checkBg;
        public override Color ButtonCheckedGradientEnd => _checkBg;

        public override Color CheckBackground => _checkBg;
        public override Color CheckSelectedBackground => _checkBg;
        public override Color CheckPressedBackground => _checkBg;
    }
}
