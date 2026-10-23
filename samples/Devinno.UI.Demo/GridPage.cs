using Devinno.UI.App;
// 데이터 그리드(DvDataGrid) 데모 — 30만 행, 고정 열 2개 + 가로 스크롤, 다중 선택(row-key), 행 클릭,
// 헤더 탭 정렬(탭 순서 누적), 헤더 그룹, 요약 행(평균·최대·건수 — Repaint 마다 다시 계산),
// 조건부 색(row-class: 상한 행, cell-class: 값 셀 상한·하한), 즉시 셀(사용 체크·경보 램프 점멸·확인 버튼),
// 편집 셀(이름·설정값 input, 모드 select, 예약 datetime, 색 color — 셀 탭 = 바로 편집, 네이티브 편집기는 Enter/Tab 연속 입력).
// 값은 타이머가 0.5초마다 일부 행을 바꾸고 Repaint() 만 요청한다(재빌드 없음).
public sealed partial class GridPage : DvPage
{
    public const int Width = 1100;
    public const int Height = 680;
    public const int RowCount = 300_000;

    public sealed class Equipment
    {
        public string Name { get; init; } = "";
    }

    public sealed class Row
    {
        public int Id { get; init; }
        public string Tag { get; init; } = "";
        public string Desc { get; init; } = "";
        public double Value { get; set; }
        public string Unit { get; init; } = "";
        public string State { get; set; } = "Normal";
        public DateTime Time { get; set; }
        public Equipment? Equip { get; init; }

        public bool Use { get; set; } = true;
        public bool Acked { get; set; }

        // 편집 셀
        public string Name { get; set; } = "";
        public double SetPoint { get; set; }
        public string Mode { get; set; } = "auto";
        public DateTime ReserveAt { get; set; }
        public string Pen { get; set; } = "#3B82F6";

        // 즉시 셀 — 경보 램프(상한 fault·하한 warn, 점멸), 미확인 경보만 확인 버튼 활성
        public string? AlarmLamp => Acked ? null : State == "High" ? "fault" : State == "Low" ? "warn" : null;
        public bool CanAck => !Acked && State != "Normal";

        // 조건부 색 — 조건은 앱 코드(그리드는 그릴 때 읽는다)
        public string? RowClass => State == "High" ? "alarm-row" : null;
        public string? ValueClass => Value > 90 ? "over" : Value < 5 ? "under" : null;
    }

    private static readonly string[] Kinds = { "Temperature", "Pressure", "Flow", "Level", "Current" };
    private static readonly string[] Units = { "℃", "bar", "m³/h", "%", "A" };
    private static readonly Equipment[] Equips = Enumerable.Range(1, 40).Select(i => new Equipment { Name = $"Treatment Unit {i:00}" }).ToArray();
    private static readonly string[] PenColors = { "#3B82F6", "#22C55E", "#F59E0B", "#EF4444", "#A855F7" };
    private readonly Random _rnd = new(7);

    public GridPage()
    {
        var now = DateTime.Now;
        Rows = new List<Row>(RowCount);
        for (var i = 0; i < RowCount; i++)
        {
            var k = i % Kinds.Length;
            Rows.Add(new Row
            {
                Id = i + 1,
                Tag = $"{"TPFLI"[k]}T-{i / Kinds.Length + 1:00000}",
                Desc = $"{Equips[i % Equips.Length].Name} {Kinds[k]} sensor #{i % 12 + 1}",
                Value = Math.Round(_rnd.NextDouble() * 100, 2),
                Unit = Units[k],
                Time = now,
                Name = $"{Kinds[k]} loop {i % 12 + 1}",
                SetPoint = Math.Round(_rnd.NextDouble() * 100, 1),
                Mode = i % 3 == 0 ? "manual" : "auto",
                ReserveAt = now.Date.AddHours(8 + i % 10),
                Pen = PenColors[i % PenColors.Length],
                // 1호기는 일부러 설비 정보 없음 — 점 경로 중간 null 은 빈 셀
                Equip = i % Equips.Length == 0 ? null : Equips[i % Equips.Length]
            });
            Rows[^1].State = StateOf(Rows[^1].Value);
        }
    }

    public List<Row> Rows { get; }

    private static string StateOf(double v) => v > 90 ? "High" : v < 5 ? "Low" : "Normal";
    public List<Row> Picked { get; set; } = new();
    public Row? Last { get; private set; }

    public string CountText => $"{Rows.Count:N0} rows";
    public string SelectionText => Last is null
        ? $"Selected {Picked.Count} — tap a row to select/clear"
        : $"Selected {Picked.Count} · last: {Last.Tag} ({Last.Desc})";

    public void Inspect(Row r) => Last = r;

    public bool CheckSetPoint(Row r, double v) => v <= 100; // 데모: 100 초과 거절(입력 상자에 사유)

    public void Ack(Row r)
    {
        r.Acked = true;
        Last = r;
    }

    public void ClearSelection()
    {
        Picked = new List<Row>();
        Last = null;
    }

    /// <summary>타이머 — 행 2,000 개의 값·시각을 바꾼다(앞쪽 100 행은 항상 포함해 첫 화면에서 보이게).</summary>
    public void Tick()
    {
        var now = DateTime.Now;
        for (var n = 0; n < 2000; n++)
        {
            var r = Rows[n < 100 ? n : _rnd.Next(Rows.Count)];
            r.Value = Math.Round(Math.Clamp(r.Value + (_rnd.NextDouble() - 0.5) * 4, 0, 100), 2);
            r.State = StateOf(r.Value);
            r.Time = now;
        }
    }
}
