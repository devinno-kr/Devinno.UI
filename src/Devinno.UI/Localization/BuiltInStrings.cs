namespace Devinno.UI.Localization;

/// <summary>
/// The library's built-in on-screen text (<c>dv.</c> keys) in English and Korean. Lookup order: the app's table in the current language →
/// the built-in table in the current language → the app's table in <see cref="DvStrings.FallbackLanguage"/> → built-in English.
/// Without a <see cref="DvStrings"/>, built-in English is used.
/// </summary>
internal static class BuiltInStrings
{
    internal static readonly IReadOnlyDictionary<string, string> En = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["dv.dialog.ok"] = "OK",
        ["dv.dialog.cancel"] = "Cancel",
        ["dv.dialog.yes"] = "Yes",
        ["dv.dialog.no"] = "No",

        ["dv.input.rejected"] = "This value is not allowed",
        ["dv.input.write-failed"] = "Could not save the value",
        ["dv.input.required"] = "Enter a value",
        ["dv.input.unsigned"] = "Negative numbers are not allowed",
        ["dv.input.integer"] = "Only whole numbers are allowed",
        ["dv.input.number"] = "Not a number",
        ["dv.input.overflow"] = "Out of the allowed range",
        ["dv.input.range"] = "Allowed range: {0} to {1}",

        ["dv.picker.ok"] = "OK",
        ["dv.picker.cancel"] = "Cancel",
        ["dv.picker.month"] = "{0}-{1:00}",
        ["dv.picker.dow"] = "Sun,Mon,Tue,Wed,Thu,Fri,Sat",
        ["dv.picker.hex"] = "Not in #RRGGBB format",

        ["dv.kbd.done"] = "Done",

        ["dv.filter.all"] = "Select all",
        ["dv.filter.empty"] = "(Empty)",
        ["dv.filter.true"] = "Yes",
        ["dv.filter.false"] = "No",
        ["dv.filter.more"] = "Too many values; showing the first {0}",
        ["dv.filter.from"] = "From",
        ["dv.filter.to"] = "To",
        ["dv.filter.min"] = "Min",
        ["dv.filter.max"] = "Max",
        ["dv.filter.ok"] = "OK",
        ["dv.filter.cancel"] = "Cancel",
        ["dv.filter.clear"] = "Clear",

        ["dv.alarm.comm"] = "Communication fault",
        ["dv.alarm.saturated"] = "Alarm buffer saturated: acknowledge (ACK) the unacknowledged alarms",
        ["dv.alarm.none"] = "No active alarms",
        ["dv.alarm.unacked"] = "{0} unacknowledged",
        ["dv.alarm.empty"] = "No alarms to show",
        ["dv.alarm.acked"] = "Acked {0}",
        ["dv.alarm.critical"] = "Critical",
        ["dv.alarm.major"] = "Major",
        ["dv.alarm.minor"] = "Minor",
        ["dv.alarm.col.time"] = "Time",
        ["dv.alarm.col.priority"] = "Priority",
        ["dv.alarm.col.message"] = "Message",
        ["dv.alarm.col.ack"] = "Ack",

        ["dv.trend.empty"] = "No data",
    };

    internal static readonly IReadOnlyDictionary<string, string> Ko = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["dv.dialog.ok"] = "확인",
        ["dv.dialog.cancel"] = "취소",
        ["dv.dialog.yes"] = "예",
        ["dv.dialog.no"] = "아니요",

        ["dv.input.rejected"] = "허용되지 않는 값입니다",
        ["dv.input.write-failed"] = "값을 저장하지 못했습니다",
        ["dv.input.required"] = "값을 입력하세요",
        ["dv.input.unsigned"] = "음수는 입력할 수 없습니다",
        ["dv.input.integer"] = "정수만 입력할 수 있습니다",
        ["dv.input.number"] = "숫자 형식이 아닙니다",
        ["dv.input.overflow"] = "입력할 수 있는 범위를 벗어났습니다",
        ["dv.input.range"] = "허용 범위: {0} ~ {1}",

        ["dv.picker.ok"] = "확인",
        ["dv.picker.cancel"] = "취소",
        ["dv.picker.month"] = "{0}년 {1}월",
        ["dv.picker.dow"] = "일,월,화,수,목,금,토",
        ["dv.picker.hex"] = "#RRGGBB 형식이 아닙니다",

        ["dv.kbd.done"] = "완료",

        ["dv.filter.all"] = "전체 선택",
        ["dv.filter.empty"] = "(빈 값)",
        ["dv.filter.true"] = "예",
        ["dv.filter.false"] = "아니오",
        ["dv.filter.more"] = "값이 많아 처음 {0}개만 표시",
        ["dv.filter.from"] = "시작",
        ["dv.filter.to"] = "끝",
        ["dv.filter.min"] = "최소",
        ["dv.filter.max"] = "최대",
        ["dv.filter.ok"] = "확인",
        ["dv.filter.cancel"] = "취소",
        ["dv.filter.clear"] = "지우기",

        ["dv.alarm.comm"] = "통신 이상",
        ["dv.alarm.saturated"] = "알람 버퍼 포화 — 미확인 알람을 확인(ACK)하세요",
        ["dv.alarm.none"] = "활성 알람 없음",
        ["dv.alarm.unacked"] = "미확인 {0}건",
        ["dv.alarm.empty"] = "표시할 알람이 없습니다",
        ["dv.alarm.acked"] = "확인 {0}",
        ["dv.alarm.critical"] = "긴급",
        ["dv.alarm.major"] = "경고",
        ["dv.alarm.minor"] = "주의",
        ["dv.alarm.col.time"] = "시간",
        ["dv.alarm.col.priority"] = "등급",
        ["dv.alarm.col.message"] = "메시지",
        ["dv.alarm.col.ack"] = "확인",

        ["dv.trend.empty"] = "데이터 없음",
    };

    private static IReadOnlyDictionary<string, string>? Table(string? language) =>
        string.Equals(language, "en", StringComparison.OrdinalIgnoreCase) ? En
        : string.Equals(language, "ko", StringComparison.OrdinalIgnoreCase) ? Ko
        : null;

    /// <summary>Built-in text for a <c>dv.</c> key, overridable per app through <paramref name="strings"/>.</summary>
    internal static string Get(DvStrings? strings, string key)
    {
        if (strings is not null)
        {
            if (strings.LookupIn(strings.Language, key, out var v))
                return v;
            if (Table(strings.Language) is { } own && own.TryGetValue(key, out v))
                return v;
            if (strings.FallbackLanguage is { } fb)
            {
                if (strings.LookupIn(fb, key, out v))
                    return v;
                if (Table(fb) is { } fbTable && fbTable.TryGetValue(key, out v))
                    return v;
            }
        }

        return En.TryGetValue(key, out var en) ? en : "[" + key + "]";
    }
}
