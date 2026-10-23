using Devinno.UI.App;
using Devinno.UI.Alarms;
using Devinno.UI.Localization;

// 다국어 데모 — 버튼으로 언어 전환(문자열 테이블 Language 변경 → 화면 자동 재레이아웃)
public sealed partial class LangPage : DemoPage
{
    public const int Width = 1100;
    public const int Height = 560;

    public static DvStrings CreateStrings()
    {
        var s = new DvStrings { Language = "ko", FallbackLanguage = "en" };
        s.LoadJson("ko", """
            { "title": "정수장 2공정", "subtitle": "다국어 데모 · 버튼으로 언어 전환",
              "lang": { "current": "현재 언어" },
              "state": { "running": "정상 운전", "fault": "경보" },
              "kpi": { "flow": "처리 유량", "power": "전력 사용", "uptime": "가동률" },
              "card": { "equipment": "설비 상태", "alarms": "알람" },
              "eq": { "pump": "취수 펌프", "filter": "여과기" },
              "auto": "자동 운전 모드" }
            """);
        s.LoadJson("en", """
            { "title": "Water Plant · Line 2", "subtitle": "Localization demo · switch with the buttons",
              "lang": { "current": "Language" },
              "state": { "running": "Running", "fault": "Fault" },
              "kpi": { "flow": "Flow rate", "power": "Power", "uptime": "Uptime" },
              "card": { "equipment": "Equipment", "alarms": "Alarms" },
              "eq": { "pump": "Intake pump", "filter": "Filter" },
              "auto": "Auto mode",
              "dv": { "alarm": { "critical": "CRIT", "major": "MAJOR", "minor": "MINOR",
                                 "none": "No active alarms", "unacked": "{0} unacknowledged", "comm": "Comm fault" } } }
            """);
        s.LoadJson("ja", """
            { "title": "浄水場 第2工程", "subtitle": "多言語デモ · ボタンで言語切替",
              "lang": { "current": "言語" },
              "state": { "running": "運転中", "fault": "警報" },
              "kpi": { "flow": "処理流量", "power": "電力使用", "uptime": "稼働率" },
              "card": { "equipment": "設備状態", "alarms": "アラーム" },
              "eq": { "pump": "取水ポンプ", "filter": "ろ過器" },
              "auto": "自動運転モード",
              "dv": { "alarm": { "critical": "重大", "major": "警告", "minor": "注意",
                                 "none": "アクティブなアラームなし", "unacked": "未確認 {0}件", "comm": "通信異常" } } }
            """);
        return s;
    }

    private readonly AlarmLog _log = new();

    public LangPage(DvStrings strings)
    {
        Strings = strings;
        _log.Raise(AlarmPriority.Critical, "PLC1 — Filter B ΔP high");
    }

    public DvStrings Strings { get; }
    public bool Auto { get; set; } = true;
    public string Lang => Strings.Language;
    public IReadOnlyList<AlarmRow> Alarms => _log.Rows;

    public void Ko() => Strings.Language = "ko";
    public void En() => Strings.Language = "en";
    public void Ja() => Strings.Language = "ja";
}
