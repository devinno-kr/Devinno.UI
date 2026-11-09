using System.Collections.ObjectModel;

namespace Devinno.UI.Styling;

/// <summary>
/// A theme made of a set of design tokens (name → value string) and a default class stylesheet.
/// <c>token(name)</c> in markup styles is replaced with this theme's value at render time.
/// </summary>
/// <remarks>
/// <para><b>Built-in themes</b>: <see cref="Light"/>, <see cref="Dark"/>, <see cref="Graphite"/> (ISA-101 style, bright control rooms) and <see cref="Carbon"/> (night control rooms). When no theme is passed (<see langword="null"/>), most paths use <see cref="Default"/> (= <see cref="Dark"/>).
/// The built-in themes have the same token names and differ only in colors. The spacing, corner and font-size scales (<c>spacing.*</c>, <c>radius.*</c>, <c>font.*</c>) are shared.</para>
/// <para><b>Token names</b>: the canonical form is <c>token(spacing.md)</c>. <c>var(--name)</c> is also accepted as an alias, and a dot (<c>.</c>) in a name can also be looked up as a dash (<c>-</c>), so
/// <c>var(--spacing-md)</c> is the same value as <c>token(spacing.md)</c>. Names are case-insensitive, and a leading <c>--</c> on a key is stripped when stored.
/// Values are substituted as plain strings, so color tokens use the <see cref="DvColor.TryParse"/> format, size tokens are numbers, and shadow tokens use the <c>dx dy blur color</c> format.</para>
/// <para><b>Changing only some tokens</b>: pass <see cref="With(IReadOnlyDictionary{string, string})"/> a dictionary containing only the tokens to change, and a new theme is created with the rest unchanged. The original theme is not changed (immutable).</para>
/// <para><b>Missing tokens</b>: a name not in the theme writes a warning (<see cref="System.Diagnostics.Trace"/>) and is replaced with magenta <c>#FF00FF</c> (<see cref="ThemeResolver.MissingColor"/>), so
/// it stands out on screen immediately. Built-in widgets draw their own parts (lamp, tank, chart, grid, keyboard …) from tokens the same way — there is no hidden fallback color,
/// so a custom theme built from scratch must define every built-in token. In non-color positions (e.g. <c>padding</c>) the declaration cannot be resolved and is ignored.</para>
/// <para>The token list can be checked in <see cref="Tokens"/>. Main groups: surfaces (<c>background</c>, <c>surface</c>, <c>border</c> …), text (<c>text</c>, <c>text-muted</c> …),
/// brand (<c>primary</c> …), tones (<c>tone-info</c>, <c>-fg</c>, <c>-soft</c> …), equipment states (<c>state-run</c> …), alarms (<c>alarm-critical</c> …), chart colors (<c>chart-1</c>…<c>chart-6</c>), widget colors (<c>gauge-*</c>, <c>lamp-off</c>, <c>tank-*</c>, <c>grid-*</c>, <c>kbd-*</c> …).</para>
/// </remarks>
/// <example>
/// <code>
/// // a theme that changes only the brand color and card corners of the default Dark theme
/// var theme = DvTheme.Dark.With(new Dictionary&lt;string, string&gt;
/// {
///     ["primary"] = "#0ea5e9",
///     ["radius.lg"] = "12",
/// });
/// byte[] png = DvRenderer.RenderToPng(
///     "&lt;DvBox style=\"background: token(primary); border-radius: token(radius.lg)\"/&gt;", null, 200, 100, theme);
/// </code>
/// </example>
public sealed class DvTheme
{
    // 테마 공통 스케일 (간격·모서리·글자 크기)
    private static readonly Dictionary<string, string> Scales = new()
    {
        ["spacing.xs"] = "4",
        ["spacing.sm"] = "8",
        ["spacing.md"] = "12",
        ["spacing.lg"] = "16",
        ["spacing.xl"] = "24",
        ["spacing.2xl"] = "32",
        ["radius.sm"] = "6",
        ["radius.md"] = "10",
        ["radius.lg"] = "16",
        ["radius.full"] = "999",
        ["font.display"] = "32",
        ["font.heading"] = "21",
        ["font.title"] = "15",
        ["font.body"] = "14",
        ["font.label"] = "13",
        ["font.caption"] = "11",
        ["font.mono"] = "14"
    };

    /// <summary>The built-in theme for light backgrounds. Uses white and light-gray surfaces, dark text and a blue (<c>#2563eb</c>) brand color.</summary>
    public static readonly DvTheme Light = new(With(Scales, new Dictionary<string, string>
    {
        // 기존 키(호환 유지)
        ["--bg"] = "#f8fafc",
        ["--bg2"] = "#eef2f7",
        ["--surface"] = "#ffffff",
        ["--surface-alt"] = "#f1f5f9",
        ["--text"] = "#0f172a",
        ["--muted"] = "#64748b",
        ["--primary"] = "#2563eb",
        ["--primary-hover"] = "#1d4ed8",
        ["--primary-press"] = "#1e40af",
        ["--border"] = "#cbd5e1",
        ["--border-warn"] = "#f59e0b",
        ["--success"] = "#15803d",
        ["--success-bg"] = "#dcfce7",
        ["--success-border"] = "#86efac",
        ["--warn-text"] = "#92400e",
        ["--warn-bg"] = "#fef3c7",
        ["--shadow"] = "#00000022",
        ["--toggle-on"] = "#3a7bd5",
        ["--toggle-off"] = "#c7cdd5",
        ["--toggle-knob"] = "#ffffff",

        // 의미 토큰 카탈로그
        ["background"] = "#f1f5f9",
        ["background-alt"] = "#e2e8f0",
        ["surface-raised"] = "#ffffff",
        ["surface-overlay"] = "#ffffff",
        ["border-strong"] = "#94a3b8",
        ["text-muted"] = "#64748b",
        ["text-subtle"] = "#94a3b8",
        ["focus"] = "#2563eb66",
        ["primary-fg"] = "#ffffff",
        ["shadow.sm"] = "0 1 3 #0000001a",
        ["shadow.md"] = "0 6 18 #0000001f",
        ["shadow.lg"] = "0 12 30 #00000029",
        ["scrim"] = "#0f172a66", // 다이얼로그 막(맨 위 다이얼로그 바로 아래 창 전체)

        ["tone-neutral"] = "#64748b", ["tone-neutral-fg"] = "#334155", ["tone-neutral-soft"] = "#e2e8f0",
        ["tone-info"] = "#2563eb", ["tone-info-fg"] = "#1d4ed8", ["tone-info-soft"] = "#dbeafe",
        ["tone-success"] = "#16a34a", ["tone-success-fg"] = "#15803d", ["tone-success-soft"] = "#dcfce7",
        ["tone-warning"] = "#d97706", ["tone-warning-fg"] = "#92400e", ["tone-warning-soft"] = "#fef3c7",
        ["tone-danger"] = "#dc2626", ["tone-danger-fg"] = "#b91c1c", ["tone-danger-soft"] = "#fee2e2",

        ["state-run"] = "#16a34a", ["state-run-fg"] = "#15803d", ["state-run-soft"] = "#dcfce7",
        ["state-stop"] = "#64748b", ["state-stop-fg"] = "#334155", ["state-stop-soft"] = "#e2e8f0",
        ["state-fault"] = "#dc2626", ["state-fault-fg"] = "#b91c1c", ["state-fault-soft"] = "#fee2e2",
        ["state-warn"] = "#d97706", ["state-warn-fg"] = "#92400e", ["state-warn-soft"] = "#fef3c7",
        ["state-manual"] = "#7c3aed", ["state-manual-fg"] = "#6d28d9", ["state-manual-soft"] = "#ede9fe",
        ["state-off"] = "#94a3b8", ["state-off-fg"] = "#475569", ["state-off-soft"] = "#f1f5f9",

        ["alarm-critical"] = "#dc2626", ["alarm-critical-fg"] = "#ffffff",
        ["alarm-major"] = "#f59e0b", ["alarm-major-fg"] = "#111827",
        ["alarm-minor"] = "#3b82f6", ["alarm-minor-fg"] = "#ffffff",
        ["comm-alarm"] = "#7a1fa2", ["comm-alarm-fg"] = "#ffffff",

        // 위젯(Painter) 색
        ["gauge-track"] = "#e2e8f0", ["gauge-needle"] = "#0f172a",
        ["tank-empty-top"] = "#f5f8fc", ["tank-empty-bottom"] = "#e6ecf3", ["tank-frame"] = "#cbd5e1", ["tank-fill"] = "#3a7bd5",
        ["tank-chip-bg"] = "#ffffffdd", ["tank-chip-fg"] = "#0f172a",
        ["knob-face"] = "#f8fafc", ["knob-fill"] = "#2563eb", ["knob-indicator"] = "#0f172a",
        ["gauge-tick"] = "#94a3b8", ["gauge-needle-tip"] = "#ef4444",
        ["trend-grid"] = "#e2e8f0",
        ["chart-1"] = "#3b82f6", ["chart-2"] = "#22d3ee", ["chart-3"] = "#a78bfa",
        ["chart-4"] = "#f59e0b", ["chart-5"] = "#10b981", ["chart-6"] = "#ef4444",
        ["lamp-off"] = "#3a3f47",
        ["scrollbar-thumb"] = "#64748b80",
        ["scrollbar-track"] = "#0f172a0f",
        ["grid-header"] = "#eef2f6", ["grid-header-fg"] = "#475569",
        ["grid-row"] = "#ffffff", ["grid-row-alt"] = "#f8fafc",
        ["grid-row-hover"] = "#eff6ff", ["grid-row-selected"] = "#dbeafe", ["grid-line"] = "#e2e8f0",
        ["grid-summary"] = "#e2e8f0", ["grid-summary-fg"] = "#0f172a",
        ["grid-filter"] = "#f8fafc", ["grid-filter-fg"] = "#94a3b8", ["grid-filter-active"] = "#2563eb",
        ["kbd-bg"] = "#d5dbe3", ["kbd-key"] = "#ffffff", ["kbd-key-fg"] = "#0f172a",
        ["kbd-key-special"] = "#b8c2cf", ["kbd-key-special-fg"] = "#0f172a"
    }));

    /// <summary>The built-in theme for dark backgrounds, and the <see cref="Default"/>. Uses navy-toned surfaces, light text and a light-blue (<c>#60a5fa</c>) brand color.</summary>
    public static readonly DvTheme Dark = new(With(Scales, new Dictionary<string, string>
    {
        // 기존 키(호환 유지)
        ["--bg"] = "#0f172a",
        ["--bg2"] = "#111827",
        ["--surface"] = "#1e293b",
        ["--surface-alt"] = "#273449",
        ["--text"] = "#f8fafc",
        ["--muted"] = "#94a3b8",
        ["--primary"] = "#60a5fa",
        ["--primary-hover"] = "#3b82f6",
        ["--primary-press"] = "#2563eb",
        ["--border"] = "#334155",
        ["--border-warn"] = "#fbbf24",
        ["--success"] = "#86efac",
        ["--success-bg"] = "#123524",
        ["--success-border"] = "#166534",
        ["--warn-text"] = "#fde68a",
        ["--warn-bg"] = "#3b2f13",
        ["--shadow"] = "#00000066",
        ["--toggle-on"] = "#60a5fa",
        ["--toggle-off"] = "#475569",
        ["--toggle-knob"] = "#e2e8f0",

        // 의미 토큰 카탈로그
        ["background"] = "#0b1020",
        ["background-alt"] = "#111827",
        ["surface-raised"] = "#273449",
        ["surface-overlay"] = "#334155",
        ["border-strong"] = "#475569",
        ["text-muted"] = "#94a3b8",
        ["text-subtle"] = "#64748b",
        ["focus"] = "#60a5fa80",
        ["primary-fg"] = "#ffffff",
        ["shadow.sm"] = "0 2 6 #00000033",
        ["shadow.md"] = "0 8 24 #00000040",
        ["shadow.lg"] = "0 14 36 #00000066",
        ["scrim"] = "#00000099", // 다이얼로그 막(맨 위 다이얼로그 바로 아래 창 전체)

        ["tone-neutral"] = "#64748b", ["tone-neutral-fg"] = "#cbd5e1", ["tone-neutral-soft"] = "#94a3b81f",
        ["tone-info"] = "#3b82f6", ["tone-info-fg"] = "#93c5fd", ["tone-info-soft"] = "#3b82f61f",
        ["tone-success"] = "#10b981", ["tone-success-fg"] = "#6ee7b7", ["tone-success-soft"] = "#10b9811f",
        ["tone-warning"] = "#f59e0b", ["tone-warning-fg"] = "#fcd34d", ["tone-warning-soft"] = "#f59e0b1f",
        ["tone-danger"] = "#ef4444", ["tone-danger-fg"] = "#fca5a5", ["tone-danger-soft"] = "#ef44441f",

        ["state-run"] = "#10b981", ["state-run-fg"] = "#6ee7b7", ["state-run-soft"] = "#10b9811f",
        ["state-stop"] = "#64748b", ["state-stop-fg"] = "#cbd5e1", ["state-stop-soft"] = "#64748b26",
        ["state-fault"] = "#ef4444", ["state-fault-fg"] = "#fca5a5", ["state-fault-soft"] = "#ef44441f",
        ["state-warn"] = "#f59e0b", ["state-warn-fg"] = "#fcd34d", ["state-warn-soft"] = "#f59e0b1f",
        ["state-manual"] = "#8b5cf6", ["state-manual-fg"] = "#c4b5fd", ["state-manual-soft"] = "#8b5cf61f",
        ["state-off"] = "#475569", ["state-off-fg"] = "#94a3b8", ["state-off-soft"] = "#47556926",

        ["alarm-critical"] = "#dc2626", ["alarm-critical-fg"] = "#ffffff",
        ["alarm-major"] = "#f59e0b", ["alarm-major-fg"] = "#111827",
        ["alarm-minor"] = "#3b82f6", ["alarm-minor-fg"] = "#ffffff",
        ["comm-alarm"] = "#7a1fa2", ["comm-alarm-fg"] = "#ffffff",

        // 위젯(Painter) 색 — 어두운 배경에서 흰 판이 튀지 않게
        ["gauge-track"] = "#334155", ["gauge-needle"] = "#e2e8f0",
        ["tank-empty-top"] = "#1e293b", ["tank-empty-bottom"] = "#0f172a", ["tank-frame"] = "#475569", ["tank-fill"] = "#3a7bd5",
        ["tank-chip-bg"] = "#0f172acc", ["tank-chip-fg"] = "#f1f5f9",
        ["knob-face"] = "#273449", ["knob-fill"] = "#3b82f6", ["knob-indicator"] = "#f1f5f9",
        ["gauge-tick"] = "#64748b", ["gauge-needle-tip"] = "#f87171",
        ["trend-grid"] = "#263247",
        ["chart-1"] = "#3b82f6", ["chart-2"] = "#22d3ee", ["chart-3"] = "#a78bfa",
        ["chart-4"] = "#f59e0b", ["chart-5"] = "#10b981", ["chart-6"] = "#ef4444",
        ["lamp-off"] = "#3a3f47",
        ["scrollbar-thumb"] = "#94a3b880",
        ["scrollbar-track"] = "#ffffff0f",
        ["grid-header"] = "#273449", ["grid-header-fg"] = "#cbd5e1",
        ["grid-row"] = "#111827", ["grid-row-alt"] = "#151e2e",
        ["grid-row-hover"] = "#1c2a42", ["grid-row-selected"] = "#1d3557", ["grid-line"] = "#263247",
        ["grid-summary"] = "#253247", ["grid-summary-fg"] = "#f1f5f9",
        ["grid-filter"] = "#1a2436", ["grid-filter-fg"] = "#64748b", ["grid-filter-active"] = "#60a5fa",
        ["kbd-bg"] = "#0b1020", ["kbd-key"] = "#273449", ["kbd-key-fg"] = "#f1f5f9",
        ["kbd-key-special"] = "#1a2436", ["kbd-key-special-fg"] = "#cbd5e1"
    }));

    /// <summary>
    /// The built-in high-performance HMI theme for bright, daytime control rooms, in the style of ISA-101.
    /// Neutral gray surfaces and dark text keep the normal screen calm, and saturated color is reserved for abnormal states (alarms, faults, warnings).
    /// </summary>
    /// <remarks>
    /// Running equipment is a dark gray fill (<c>state-run</c>) and stopped equipment a light gray (<c>state-stop</c>), not green and red.
    /// Faults and warnings stay red and orange, and alarm priorities are red (critical), orange (major) and dark yellow (minor), dark enough that the alarm list can use them as text on light gray.
    /// The brand color is a muted steel blue (<c>#4A6278</c>), and chart colors are low-saturation grays, steel blues and teals.
    /// Process diagrams (<c>DvProcess</c>) keep the colors of their <c>look</c>.
    /// </remarks>
    public static readonly DvTheme Graphite = new(With(Scales, new Dictionary<string, string>
    {
        // 기존 키(호환 유지)
        ["--bg"] = "#d5d8dc",
        ["--bg2"] = "#cbcfd4",
        ["--surface"] = "#e6e8eb",
        ["--surface-alt"] = "#dde0e4",
        ["--text"] = "#1e2329",
        ["--muted"] = "#4f5761",
        ["--primary"] = "#4a6278",
        ["--primary-hover"] = "#3f5569",
        ["--primary-press"] = "#34475a",
        ["--border"] = "#b8bdc4",
        ["--border-warn"] = "#f08c00",
        ["--success"] = "#355742",
        ["--success-bg"] = "#d6e3da",
        ["--success-border"] = "#9db8a6",
        ["--warn-text"] = "#7a4300",
        ["--warn-bg"] = "#fbe6c4",
        ["--shadow"] = "#0000001f",
        ["--toggle-on"] = "#4a6278",
        ["--toggle-off"] = "#aab0b8",
        ["--toggle-knob"] = "#ffffff",

        // 의미 토큰 카탈로그
        ["background"] = "#d5d8dc",
        ["background-alt"] = "#cbcfd4",
        ["surface-raised"] = "#f2f3f5",
        ["surface-overlay"] = "#f7f8f9",
        ["border-strong"] = "#8e959e",
        ["text-muted"] = "#4f5761",
        ["text-subtle"] = "#7d858f",
        ["focus"] = "#4a627866",
        ["primary-fg"] = "#ffffff",
        ["shadow.sm"] = "0 1 2 #00000014",
        ["shadow.md"] = "0 3 10 #00000017",
        ["shadow.lg"] = "0 10 26 #00000026",
        ["scrim"] = "#1e232966", // 다이얼로그 막(맨 위 다이얼로그 바로 아래 창 전체)

        ["tone-neutral"] = "#6b737d", ["tone-neutral-fg"] = "#3f4750", ["tone-neutral-soft"] = "#dadde1",
        ["tone-info"] = "#4a6278", ["tone-info-fg"] = "#3a4e62", ["tone-info-soft"] = "#d8e0e8",
        ["tone-success"] = "#4f7a5f", ["tone-success-fg"] = "#355742", ["tone-success-soft"] = "#d6e3da",
        ["tone-warning"] = "#f08c00", ["tone-warning-fg"] = "#7a4300", ["tone-warning-soft"] = "#fbe6c4",
        ["tone-danger"] = "#d92d20", ["tone-danger-fg"] = "#a8231a", ["tone-danger-soft"] = "#f9dad6",

        // 정상 상태는 무채색(운전 = 진한 회색, 정지 = 밝은 회색), 이상 상태만 채도 — ISA-101
        ["state-run"] = "#2f3a44", ["state-run-fg"] = "#2f3a44", ["state-run-soft"] = "#d0d5db",
        ["state-stop"] = "#9aa1aa", ["state-stop-fg"] = "#4a525c", ["state-stop-soft"] = "#eceef0",
        ["state-fault"] = "#d92d20", ["state-fault-fg"] = "#a8231a", ["state-fault-soft"] = "#f9dad6",
        ["state-warn"] = "#f08c00", ["state-warn-fg"] = "#7a4300", ["state-warn-soft"] = "#fbe6c4",
        ["state-manual"] = "#5a7590", ["state-manual-fg"] = "#3a4e62", ["state-manual-soft"] = "#d8e0e8",
        ["state-off"] = "#c4c9cf", ["state-off-fg"] = "#5b636d", ["state-off-soft"] = "#e6e8eb",

        ["alarm-critical"] = "#d92d20", ["alarm-critical-fg"] = "#ffffff",
        ["alarm-major"] = "#c86e00", ["alarm-major-fg"] = "#111111",
        ["alarm-minor"] = "#9c8000", ["alarm-minor-fg"] = "#111111",
        ["comm-alarm"] = "#6b2c91", ["comm-alarm-fg"] = "#ffffff",

        // 위젯(Painter) 색 — 회색 판, 강조는 강철 청색 하나
        ["gauge-track"] = "#c9ced4", ["gauge-needle"] = "#1e2329",
        ["tank-empty-top"] = "#eef0f2", ["tank-empty-bottom"] = "#dde0e4", ["tank-frame"] = "#8e959e", ["tank-fill"] = "#6f8aa3",
        ["tank-chip-bg"] = "#f2f3f5dd", ["tank-chip-fg"] = "#1e2329",
        ["knob-face"] = "#f2f3f5", ["knob-fill"] = "#4a6278", ["knob-indicator"] = "#1e2329",
        ["gauge-tick"] = "#8e959e", ["gauge-needle-tip"] = "#2f3a44",
        ["trend-grid"] = "#c9ced4",
        ["chart-1"] = "#4a6278", ["chart-2"] = "#4f8a8b", ["chart-3"] = "#2f3a44",
        ["chart-4"] = "#9a8455", ["chart-5"] = "#7a7396", ["chart-6"] = "#8fa6ba",
        ["lamp-off"] = "#b4bac2",
        ["scrollbar-thumb"] = "#5b636d80",
        ["scrollbar-track"] = "#1e23290f",
        ["grid-header"] = "#d5d9de", ["grid-header-fg"] = "#3f4750",
        ["grid-row"] = "#f2f3f5", ["grid-row-alt"] = "#eaecef",
        ["grid-row-hover"] = "#e1e6ec", ["grid-row-selected"] = "#d3dce5", ["grid-line"] = "#cbd0d6",
        ["grid-summary"] = "#dadde1", ["grid-summary-fg"] = "#1e2329",
        ["grid-filter"] = "#eaecef", ["grid-filter-fg"] = "#7d858f", ["grid-filter-active"] = "#4a6278",
        ["kbd-bg"] = "#c4c9cf", ["kbd-key"] = "#f2f3f5", ["kbd-key-fg"] = "#1e2329",
        ["kbd-key-special"] = "#a9b0b8", ["kbd-key-special-fg"] = "#1e2329"
    }));

    /// <summary>
    /// The built-in theme for night-time control rooms. Low-luminance warm charcoal surfaces without a blue cast, warm off-white text and a single brass (<c>#BDA67E</c>) accent.
    /// </summary>
    /// <remarks>
    /// Overall brightness and contrast are lower than <see cref="Dark"/> to cut glare in a dim room, while body text stays at 4.5:1 or more against its background.
    /// Red and amber are kept for alarms and faults only, so the brass accent never reads as an alarm. Running equipment is a muted green, and chart colors are warm and muted.
    /// Process diagrams (<c>DvProcess</c>) keep the colors of their <c>look</c>.
    /// </remarks>
    public static readonly DvTheme Carbon = new(With(Scales, new Dictionary<string, string>
    {
        // 기존 키(호환 유지)
        ["--bg"] = "#121314",
        ["--bg2"] = "#0e0f10",
        ["--surface"] = "#1b1c1e",
        ["--surface-alt"] = "#202124",
        ["--text"] = "#e8e4dc",
        ["--muted"] = "#9a958c",
        ["--primary"] = "#bda67e",
        ["--primary-hover"] = "#c9b48f",
        ["--primary-press"] = "#a8916a",
        ["--border"] = "#33363a",
        ["--border-warn"] = "#f5a524",
        ["--success"] = "#8fc4a3",
        ["--success-bg"] = "#1a2a21",
        ["--success-border"] = "#2f5a41",
        ["--warn-text"] = "#f5c878",
        ["--warn-bg"] = "#33281a",
        ["--shadow"] = "#00000080",
        ["--toggle-on"] = "#bda67e",
        ["--toggle-off"] = "#3d4045",
        ["--toggle-knob"] = "#d8d3ca",

        // 의미 토큰 카탈로그
        ["background"] = "#121314",
        ["background-alt"] = "#0e0f10",
        ["surface-raised"] = "#242629",
        ["surface-overlay"] = "#2c2e32",
        ["border-strong"] = "#46494e",
        ["text-muted"] = "#9a958c",
        ["text-subtle"] = "#66625c",
        ["focus"] = "#bda67e80",
        ["primary-fg"] = "#17140f",
        ["shadow.sm"] = "0 2 6 #00000040",
        ["shadow.md"] = "0 8 24 #0000004d",
        ["shadow.lg"] = "0 14 36 #00000073",
        ["scrim"] = "#000000a6", // 다이얼로그 막(맨 위 다이얼로그 바로 아래 창 전체)

        ["tone-neutral"] = "#77736c", ["tone-neutral-fg"] = "#c4bfb5", ["tone-neutral-soft"] = "#8e8a821f",
        ["tone-info"] = "#7f96ad", ["tone-info-fg"] = "#a9bacb", ["tone-info-soft"] = "#7f96ad1f",
        ["tone-success"] = "#5fa77f", ["tone-success-fg"] = "#8fc4a3", ["tone-success-soft"] = "#5fa77f1f",
        ["tone-warning"] = "#f5a524", ["tone-warning-fg"] = "#f5c878", ["tone-warning-soft"] = "#f5a5241f",
        ["tone-danger"] = "#e5484d", ["tone-danger-fg"] = "#f2898c", ["tone-danger-soft"] = "#e5484d1f",

        ["state-run"] = "#5fa77f", ["state-run-fg"] = "#8fc4a3", ["state-run-soft"] = "#5fa77f1f",
        ["state-stop"] = "#5c5f65", ["state-stop-fg"] = "#a8a39a", ["state-stop-soft"] = "#8e8a8226",
        ["state-fault"] = "#e5484d", ["state-fault-fg"] = "#f2898c", ["state-fault-soft"] = "#e5484d1f",
        ["state-warn"] = "#f5a524", ["state-warn-fg"] = "#f5c878", ["state-warn-soft"] = "#f5a5241f",
        ["state-manual"] = "#9c8ac4", ["state-manual-fg"] = "#beb0dd", ["state-manual-soft"] = "#9c8ac41f",
        ["state-off"] = "#3a3c40", ["state-off-fg"] = "#9a958c", ["state-off-soft"] = "#8e8a821a",

        ["alarm-critical"] = "#d03a40", ["alarm-critical-fg"] = "#ffffff",
        ["alarm-major"] = "#f5a524", ["alarm-major-fg"] = "#121314",
        ["alarm-minor"] = "#7f96ad", ["alarm-minor-fg"] = "#121314",
        ["comm-alarm"] = "#7d4a9e", ["comm-alarm-fg"] = "#ffffff",

        // 위젯(Painter) 색 — 낮은 밝기, 푸른 기 없는 숯색
        ["gauge-track"] = "#2c2e32", ["gauge-needle"] = "#e8e4dc",
        ["tank-empty-top"] = "#202124", ["tank-empty-bottom"] = "#141516", ["tank-frame"] = "#46494e", ["tank-fill"] = "#6e8a9a",
        ["tank-chip-bg"] = "#121314cc", ["tank-chip-fg"] = "#e8e4dc",
        ["knob-face"] = "#242629", ["knob-fill"] = "#bda67e", ["knob-indicator"] = "#e8e4dc",
        ["gauge-tick"] = "#5a5750", ["gauge-needle-tip"] = "#bda67e",
        ["trend-grid"] = "#2a2c30",
        ["chart-1"] = "#bda67e", ["chart-2"] = "#7fa7a0", ["chart-3"] = "#c4876a",
        ["chart-4"] = "#9daa7a", ["chart-5"] = "#a58cb8", ["chart-6"] = "#8fa3b8",
        ["lamp-off"] = "#3a3c40",
        ["scrollbar-thumb"] = "#8e8a8266",
        ["scrollbar-track"] = "#ffffff0a",
        ["grid-header"] = "#242629", ["grid-header-fg"] = "#c4bfb5",
        ["grid-row"] = "#18191b", ["grid-row-alt"] = "#1c1d20",
        ["grid-row-hover"] = "#26282b", ["grid-row-selected"] = "#3a3324", ["grid-line"] = "#2a2c30",
        ["grid-summary"] = "#26282b", ["grid-summary-fg"] = "#e8e4dc",
        ["grid-filter"] = "#1f2023", ["grid-filter-fg"] = "#66625c", ["grid-filter-active"] = "#bda67e",
        ["kbd-bg"] = "#0e0f10", ["kbd-key"] = "#242629", ["kbd-key-fg"] = "#e8e4dc",
        ["kbd-key-special"] = "#1b1c1e", ["kbd-key-special-fg"] = "#c4bfb5"
    }));

    /// <summary>
    /// The theme used for token resolution and the built-in stylesheet when no theme is specified (<see langword="null"/>). Currently returns <see cref="Dark"/>.
    /// </summary>
    public static DvTheme Default => Dark;

    /// <summary>
    /// Creates a theme directly from a token dictionary and a stylesheet. To change only part of a built-in theme, <see cref="With(IReadOnlyDictionary{string, string})"/> is simpler.
    /// </summary>
    /// <remarks>
    /// A leading <c>--</c> on a key is stripped when stored, and names are case-insensitive. For keys containing a dot (<c>spacing.md</c>), a dash alias (<c>spacing-md</c>) is registered as well
    /// (if a dash key with the same name already exists, its value is kept). <paramref name="tokens"/> is copied, so later changes to the original dictionary do not affect the theme.
    /// </remarks>
    /// <param name="tokens">Token name → value string. If tokens used by built-in widgets are missing, those parts are drawn in magenta (<c>#FF00FF</c>) with a warning.</param>
    /// <param name="stylesheet">
    /// The class stylesheet (CSS-like; see <see cref="StyleSheet"/> for the format). If <see langword="null"/>, <see cref="BuiltInStyles.Sheet"/> is used.
    /// A value <b>replaces</b> the built-in stylesheet, so to keep the built-in classes and add to them, concatenate like <c>BuiltInStyles.Sheet + 내 CSS</c> (your CSS).
    /// </param>
    public DvTheme(IReadOnlyDictionary<string, string> tokens, string? stylesheet = null)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (key, value) in tokens)
            map[Normalize(key)] = value;

        // 별칭: spacing.md ↔ spacing-md (var(--spacing-md) 표기 지원)
        foreach (var key in map.Keys.Where(k => k.Contains('.')).ToList())
            map.TryAdd(key.Replace('.', '-'), map[key]);

        Tokens = new ReadOnlyDictionary<string, string>(map);
        Stylesheet = stylesheet ?? BuiltInStyles.Sheet;
    }

    /// <summary>
    /// All tokens of this theme (read-only, case-insensitive). Keys are names without <c>--</c>, and the dash aliases of keys containing a dot are included as well.
    /// </summary>
    public IReadOnlyDictionary<string, string> Tokens { get; }

    /// <summary>
    /// The default stylesheet provided by the theme (class-definition CSS text). It is read first at render time, and the markup's <c>&lt;DvStyle&gt;</c> rules are appended after it,
    /// overriding properties of the same class and state.
    /// </summary>
    public string Stylesheet { get; }

    /// <summary>
    /// Creates a new theme with only some tokens changed. This theme is not changed, and the stylesheet is inherited as is.
    /// </summary>
    /// <remarks>
    /// Changing a token containing a dot (<c>spacing.md</c>) also changes its dash alias (<c>spacing-md</c>) to the same value, so the <c>var(--spacing-md)</c> form follows the new value too.
    /// If the same dictionary also gives the dash key (<c>spacing-md</c>) separately, that alias uses the given value.
    /// </remarks>
    /// <param name="overrides">Tokens to change or add (name → value). A leading <c>--</c> on a key is allowed.</param>
    /// <returns>A new <see cref="DvTheme"/> with only the specified tokens changed.</returns>
    public DvTheme With(IReadOnlyDictionary<string, string> overrides)
        => new(With(Tokens, overrides), Stylesheet);

    /// <summary>
    /// Looks up a token value. Use it with <see cref="DvColor.TryParse"/> when a custom widget uses theme colors directly.
    /// </summary>
    /// <param name="token">The token name (e.g. <c>primary</c>, <c>spacing.md</c>, <c>--primary</c>). Case is ignored and a leading <c>--</c> is stripped before lookup.</param>
    /// <param name="value">The value string if found; otherwise <see langword="null"/>.</param>
    /// <returns><see langword="true"/> if the token exists.</returns>
    public bool TryGet(string token, out string value) => Tokens.TryGetValue(Normalize(token), out value!);

    private static string Normalize(string key) => key.StartsWith("--", StringComparison.Ordinal) ? key[2..] : key;

    private static Dictionary<string, string> With(
        IReadOnlyDictionary<string, string> a, IReadOnlyDictionary<string, string> b)
    {
        var d = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (k, v) in a)
            d[Normalize(k)] = v;
        foreach (var (k, v) in b)
            d[Normalize(k)] = v;

        // 바뀐 점 토큰의 대시 별칭도 함께 갱신(b 에 대시 키를 직접 준 경우는 그 값 유지)
        var given = new HashSet<string>(b.Keys.Select(Normalize), StringComparer.OrdinalIgnoreCase);
        foreach (var (k, v) in b)
        {
            var key = Normalize(k);
            if (key.Contains('.') && !given.Contains(key.Replace('.', '-')))
                d[key.Replace('.', '-')] = v;
        }

        return d;
    }
}
