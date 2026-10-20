using System;
using System.Xml.Linq;
using Microsoft.CodeAnalysis;

namespace Devinno.UI.Generators;

/// <summary>
/// 기본 컨트롤 마크업 검사(DVML015~017) — 실행 때 조용히 무시되거나 기본값으로 바뀌는 흔한 실수. 바인딩한 값(<c>{…}</c>)은 건너뛴다.
/// </summary>
internal static class WidgetCheck
{
    public static void Check(XElement root, Action<DiagnosticDescriptor, XObject, object[]> report)
    {
        foreach (var e in root.DescendantsAndSelf())
        {
            switch (e.Name.LocalName)
            {
                case "DvRadio" when e.Attribute("option") is null:
                    report(Diagnostics.RadioNoOption, e, Array.Empty<object>());
                    break;

                case "DvListBox":
                {
                    var mode = e.Attribute("selection-mode");
                    if (mode is not null && !IsBinding(mode.Value) && mode.Value is not ("single" or "multi"))
                        report(Diagnostics.WidgetUnknownValue, mode, new object[] { "DvListBox", "selection-mode", mode.Value, "single·multi", "single" });
                    var multi = mode?.Value == "multi";
                    if (multi && e.Attribute("value") is { } v)
                        report(Diagnostics.ListBoxValueAttr, v, new object[] { "selection-mode=\"multi\" 인 DvListBox 의 value — 여럿 고르기는 selected-items 에 바인딩합니다" });
                    else if (!multi && mode is not null && IsBinding(mode.Value))
                        break; // 모드가 바인딩이면 어느 쪽인지 모른다
                    else if (!multi && e.Attribute("selected-items") is { } si)
                        report(Diagnostics.ListBoxValueAttr, si, new object[] { "한 개 고르기 DvListBox 의 selected-items — 여럿 고르기면 selection-mode=\"multi\" 를 주고, 한 개면 value 에 바인딩합니다" });
                    break;
                }

                case "DvSlider" when e.Attribute("direction") is { } d && !IsBinding(d.Value) && d.Value is not ("right" or "left" or "up" or "down"):
                    report(Diagnostics.WidgetUnknownValue, d, new object[] { "DvSlider", "direction", d.Value, "right·left·up·down", "right" });
                    break;
            }
        }
    }

    private static bool IsBinding(string v) => v.TrimStart().StartsWith("{", StringComparison.Ordinal);
}
