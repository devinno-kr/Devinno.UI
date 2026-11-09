using System.Globalization;
using Devinno.UI.Binding;
using Devinno.UI.Interaction;

namespace Devinno.UI.Layout;

// 계기·차트·알람 태그 빌더 (DvGauge DvRing DvProgress DvLamp DvTank DvSparkline DvBarChart DvPieChart DvTrend/DvTrendChannel
// DvAlarmBanner DvAlarmList). 속성은 모두 AttrReader 로 읽는다 — 정의는 TagSchema.
public sealed partial class LayoutEngine
{
    // BuildSelf 에서 공통 속성·입력 태그 다음에 부른다. 태그마다 읽는 순서는 옮기기 전과 같다.
    private void BuildInstrumentTag(LayoutBox box, AttrReader r)
    {
        switch (r.Node.Tag)
        {
            case "DvSparkline" or "DvBarChart" or "DvPieChart":
                BuildChart(box, r);
                break;
            case "DvLamp":
                BuildLamp(box, r);
                break;
            case "DvGauge" or "DvRing" or "DvProgress":
                BuildDial(box, r);
                break;
            case "DvTrend":
                box.Trend = BuildTrend(r);
                break;
            case "DvAlarmBanner" or "DvAlarmList":
                BuildAlarms(box, r);
                break;
            case "DvTank":
                BuildTank(box, r);
                break;
        }
    }

    private void BuildLamp(LayoutBox box, AttrReader r)
    {
        box.LampOn = ToBool(r.Value("on"));
        var blinkExpr = r.Raw("blink-when") ?? r.Raw("blink");
        box.LampBlink = ToBool(BindingResolver.ResolveValueOrLiteral(r.Context, blinkExpr));
        if (r.Text("blink-interval") is { } bi &&
            int.TryParse(bi, out var bint) && bint > 0)
            box.BlinkIntervalMs = bint;
        if (ParseColor(r.Text("color")) is { } lampColor)
            box.LampColor = lampColor;
        box.LampState = r.Text("state");
    }

    // DvGauge·DvRing·DvProgress 가 같은 상자 필드(Gauge*·Tone·Unit…)를 쓴다. 셋 다 모든 속성을 읽는다 —
    // 태그가 그리지 않는 속성도(TagSchema Note "dead read"; 읽기를 빼는 건 3단계).
    private void BuildDial(LayoutBox box, AttrReader r)
    {
        box.GaugeValue = ToFloat(r.Value("value"), 0f);
        box.GaugeMin = ToFloat(r.Value("min"), 0f);
        box.GaugeMax = ToFloat(r.Value("max"), 100f);
        box.GaugeZones = ParseZones(r);
        box.GaugeTicks = ToFloat(r.Value("ticks"), 0f);
        box.GaugeMinorTicks = ToFloat(r.Value("minor-ticks"), 0f);
        if (r.Has("show-tick-labels"))
            box.GaugeTickLabels = !string.Equals(
                r.Text("show-tick-labels"), "false", StringComparison.OrdinalIgnoreCase);

        box.Tone = r.Text("tone") is { Length: > 0 } tone ? tone : "info";
        box.Direction = r.Text("direction") is { Length: > 0 } dir ? dir : "right";
        box.ShowValue = ToBool(r.Text("show-value"));
        box.Unit = r.Text("unit");
        box.ValueFormat = r.Text("format");
        box.Label = r.Text("label");
        var sweep = ToFloat(r.Text("sweep"), 270f);
        box.RingSweep = Math.Clamp(sweep, 1f, 360f);
        if (r.Text("thickness") is { Length: > 0 } th)
            box.RingThickness = ToFloat(th, 0f) is > 0 and var t ? t : null;
    }

    private void BuildTank(LayoutBox box, AttrReader r)
    {
        box.TankValue = ToFloat(r.Value("value"), 0f);
        box.TankMin = ToFloat(r.Value("min"), 0f);
        box.TankMax = ToFloat(r.Value("max"), 100f);
        box.TankUnit = r.Text("unit");
        box.ValueFormat = r.Text("format");
        box.TankZones = ParseZones(r);
        // show-zone-marks="false" → 구간 색은 유지하고 경계 눈금·숫자만 숨김 (없으면 true)
        if (r.Has("show-zone-marks"))
            box.TankZoneMarks = !string.Equals(
                r.Text("show-zone-marks"), "false", StringComparison.OrdinalIgnoreCase);
    }

    private static void BuildAlarms(LayoutBox box, AttrReader r)
    {
        var node = r.Node;
        // 레이아웃 시점 스냅샷 — VM 변경은 host 의 Invalidate 후 반영
        var alarmsRaw = r.Raw("items");
        box.AlarmItems = BindingResolver.ResolveValueOrLiteral(r.Context, alarmsRaw)
            is IEnumerable<Alarms.AlarmRow> items ? HandlerErrors.Snapshot(items, PathOf(alarmsRaw), node.Tag) : Array.Empty<Alarms.AlarmRow>();
        box.CommAlarm = r.Text("comm-alarm");
        box.OnAck = r.Raw("onack");
        // show-ack: 지정 시 그 값(바인딩 가능 — 권한별 표시), 미지정 시 onack 이 있을 때만 표시
        box.AlarmShowAck = r.Has("show-ack")
            ? !string.Equals(r.Text("show-ack"), "false", StringComparison.OrdinalIgnoreCase)
            : !string.IsNullOrEmpty(box.OnAck);
        var rh = ToFloat(r.Value("row-height"), 36f);
        box.AlarmRowHeight = rh > 0 ? rh : 36f;
    }

    /// <summary>
    /// Charts (single series): DvSparkline uses <c>values</c> (a list of numbers or "1,2,3"); DvBarChart and DvPieChart use <c>items</c> + <c>item-value</c> (number) + <c>item-text</c> (name).
    /// Common: <c>tone</c> (info), <c>show-value</c>; sparkline and bar: <c>y-min</c>/<c>y-max</c> (omitted = automatic); sparkline: <c>area</c>; pie: <c>inner-radius</c> (ratio 0 to 0.9, donut).
    /// Values that cannot be read as numbers are 0 (pie charts do not draw negative or zero slices).
    /// </summary>
    private static void BuildChart(LayoutBox box, AttrReader r)
    {
        var node = r.Node;
        double? Num(string name) => double.TryParse(r.Text(name), NumberStyles.Float, CultureInfo.InvariantCulture, out var d) ? d : null;
        static double ToNum(object? v) => v switch
        {
            null => 0,
            IConvertible c when v is not string => SafeDouble(c),
            _ => double.TryParse(v.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var d) ? d : 0,
        };

        // 모든 차트 태그가 공용 속성을 다 읽는다(차트가 쓰지 않는 것도 — TagSchema Note "dead read")
        box.Tone = r.Text("tone") is { Length: > 0 } tone ? tone : "info";
        box.ShowValue = ToBool(r.Text("show-value"));
        box.ChartYMin = Num("y-min");
        box.ChartYMax = Num("y-max");
        box.ChartArea = ToBool(r.Text("area"));
        box.ChartInner = Math.Clamp((float)(Num("inner-radius") ?? 0), 0f, 0.9f);

        if (node.Tag == "DvSparkline")
        {
            var raw = r.Raw("values");
            box.ChartValues = BindingResolver.ResolveValueOrLiteral(r.Context, raw) switch
            {
                string text => text.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).Select(t => ToNum(t)).ToArray(),
                System.Collections.IEnumerable list => HandlerErrors.Snapshot(list.Cast<object?>(), PathOf(raw), node.Tag).Select(ToNum).ToArray(),
                _ => Array.Empty<double>(),
            };
            box.ChartLabels = Array.Empty<string>();
            return;
        }

        var itemsRaw = r.Raw("items");
        var items = BindingResolver.ResolveValueOrLiteral(r.Context, itemsRaw) is System.Collections.IEnumerable e and not string
            ? HandlerErrors.Snapshot(e.Cast<object?>(), PathOf(itemsRaw), node.Tag)
            : new List<object?>();
        var valuePath = r.Raw("item-value");
        var textPath = r.Raw("item-text");
        box.ChartValues = items.Select(it => ToNum(valuePath is null ? it : BindingResolver.Resolve(it, $"{{{valuePath}}}"))).ToArray();
        box.ChartLabels = items.Select(it => HandlerErrors.Text(textPath is null ? it : BindingResolver.Resolve(it, $"{{{textPath}}}"), textPath, node.Tag) ?? "").ToArray();
    }

    private static double SafeDouble(IConvertible c)
    {
        try { return c.ToDouble(CultureInfo.InvariantCulture); }
        catch (Exception e) when (e is FormatException or InvalidCastException or OverflowException) { return 0; }
    }

    // DvTrend 설정 + 채널 데이터 스냅샷 (points 바인딩 우선, 없으면 source 버퍼에서 채널 이름으로). 채널은 그 노드의 리더로 읽는다.
    private Trends.TrendSpec BuildTrend(AttrReader r)
    {
        static double? Num(AttrReader x, string name) => x.Value(name) is { } v &&
            double.TryParse(BindingResolver.ToText(v, null), NumberStyles.Float, CultureInfo.InvariantCulture, out var d) ? d : null;
        bool Flag(string name, bool dflt) => r.Text(name) is { Length: > 0 } s
            ? !string.Equals(s, "false", StringComparison.OrdinalIgnoreCase) : dflt;

        var spec = new Trends.TrendSpec
        {
            WindowSeconds = Num(r, "window") is > 0 and var w ? w : 60,
            End = r.Value("end") switch { DateTime dt => dt, _ => null },
            YMin = Num(r, "y-min"),
            YMax = Num(r, "y-max"),
            Area = Flag("area", false),
            Smooth = Flag("smooth", true)
        };

        var source = r.Value("source") as Trends.TrendBuffer;
        var index = 0;
        foreach (var ch in r.Node.Children.Where(c => c.Tag == "DvTrendChannel"))
        {
            var cr = r.Child(ch);
            var name = cr.Text("name") ?? $"ch{index}";
            var points = cr.Value("points") is IEnumerable<Trends.TrendPoint> bound
                ? HandlerErrors.Snapshot(bound, PathOf(cr.Raw("points")), ch.Tag).ToArray()
                : source?.Snapshot(name) ?? Array.Empty<Trends.TrendPoint>();
            var color = ParseColor(cr.Text("color"))?.ToSK()
                        ?? Trends.TrendSpec.Palette[index % Trends.TrendSpec.Palette.Length];
            spec.Channels.Add(new Trends.TrendChannelData
            {
                Name = name,
                Label = cr.Text("label"),
                Unit = cr.Text("unit"),
                Color = color,
                Points = points
            });
            index++;
        }

        return spec;
    }
}
