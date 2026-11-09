# Instruments, charts, trends and alarms

Covers: the display instruments (`DvGauge` `DvRing` `DvProgress` `DvLamp` `DvTank`, `alarm-zones`), the charts (`DvSparkline` `DvBarChart` `DvPieChart`), the trend (`DvTrend` `DvTrendChannel`, `TrendBuffer`) and the alarm widgets (`DvAlarmBanner` `DvAlarmList`, `AlarmLog`) — one section per tag, plus the value text, unit, range and size rules they share.
Read first: [markup basics](dvml-language.md#markup-basics) · [common attributes every tag takes](dvml-language.md#common-attributes) · [how attribute values are read](dvml-language.md#value-rules) · [updates and threads](llm-rules.md#updates-threads)
Not here: [knob and slider (input)](dvml-widgets.md#dvknob) · [DvStatusItem and other semantic components](dvml-widgets.md#semantic-components) · [data grid](dvml-grid.md#dvdatagrid) · [process diagram vessels and instrument bubbles](dvml-process.md#process-overview) · [theme tokens](dvml-style.md#theme-tokens)

How to read the tables: `bound` = the attribute takes a binding (`{X}`). Every tag except `DvTrendChannel` also takes the [common attributes](dvml-language.md#common-attributes); they are not repeated. Bool, number, word and color values follow the [value rules](dvml-language.md#value-rules): an unreadable value gives the default + a Trace warning. All instruments here are one-way (display only).

<a id="instrument-common"></a>
## Rules shared by instruments

- **Value text**: integers as integers, otherwise one decimal place (gauge, ring, progress bar, tank, chart numbers; the slider uses the decimals of `step`). A live value then switches between "6" and "6.2" and the text changes width.
- **`format`** fixes the digits: a .NET format string (InvariantCulture, bindable) — `format="0.0"` → "6.0". It means the same everywhere a number is shown as text (gauge, ring, progress bar, tank, slider, bar/pie chart numbers, `DvInput`, grid columns, summaries). A format not valid for the value (`X` on a real number) shows the default text + Trace warning.
- **`unit`** is drawn after the value text with one space (`40 %`) on the gauge, ring, progress bar, tank, knob and slider. `DvInput` keeps it at the right edge of the box; trends show it in the legend.
- **`show-value`** is honored by every tag that has it: `DvGauge` and `DvRing` show the value by default (`show-value="false"` hides it); `DvProgress`, `DvSlider`, `DvBarChart` and `DvPieChart` hide it by default.
- **Size scaling**: the internals of gauges, rings, knobs and tanks (arc thickness, needle, value text, chip, zone marks) scale with the widget size (default size = 1×; gauges, rings and knobs use the shorter side; tanks the smaller of width/80 and height/160). Proportions stay right when filling a cell with `grow="1"` or `align="stretch"`.
- **Range** (`min`/`max` on knob, slider, gauge, ring, progress bar, tank, numeric `DvInput`, grid input cells): user input never produces a value outside — drag and the stepper stop at the bound; a typed or keypad value outside is rejected (error shown). A value from code or the VM is not changed; instruments only draw it clamped.
- `min >= max` is not a range: `0`–`100` is used + Trace warning (for `DvInput` and grid cells only when both are given).
- `tone` (ring, progress bar, sparkline, bar chart) = a `tone-{tone}` theme token (built-in: `neutral` `info` `success` `warning` `danger`). Empty = `info`; a word without that token = `info` + Trace warning ([theme words](dvml-language.md#value-theme-word)).
- Values are read at layout time: after changing VM data from outside a handler, call `Invalidate()` ([updates and threads](llm-rules.md#updates-threads)).

<a id="dvgauge"></a>
## DvGauge

A 270° needle instrument.

**Attributes**

| attribute | values | default | bound | notes |
|---|---|---|---|---|
| `value` | number | `0` | yes | One-way. Drawn clamped to `min`..`max` (the value is not changed) |
| `min` | number | `0` | yes | `min >= max` = 0..100 + warning |
| `max` | number | `100` | yes | As `min` |
| `alarm-zones` | zone list | none | yes | Colored bands ([alarm-zones](#alarm-zones)) |
| `ticks` | number | `0` | yes | Major tick **interval** in value units (`20` → 0, 20, 40 …). `0` = no scale |
| `minor-ticks` | number | `0` | yes | Minor tick interval. Drawn only when `ticks` is set |
| `show-tick-labels` | bool | `true` | yes | `false` hides tick numbers |
| `format` | .NET format string | none | yes | Value text digits |
| `unit` | text | empty | yes | After the value text with one space |
| `show-value` | bool | `true` | yes | Value text (+ unit) below the needle |

**Behavior**
- Default size: 120×120.
- The value text (+ `unit`) and the needle tip use the current zone color.
- The value text sits in the open bottom of the arc and scales with the gauge, so a larger gauge gives it no more room. It is fitted between the arc ends: a text that does not fit at the normal size (`1480 rpm`, `10000 kPa`) is drawn smaller, down to 60% of the normal size. Up to about 13 characters with the unit (`10000 m3/hour`) fit; a longer text stays at 60% and reaches the arc ends (no ellipsis). With `ticks` the text sits lower and its normal size is smaller. For a longer text, leave `unit` out and put the unit in a `DvLabel` under the gauge, or set `show-value="false"` and show the value in your own label.
- The gauge has no `tone`, `direction`, `label`, `sweep` or `thickness` (those belong to the ring and progress bar); written on a gauge they are [DVML019](dvml-diagnostics.md#dvml019).

**Example**
```xml
<DvGauge value="{Pressure}" max="10" ticks="2" minor-ticks="0.5" unit="bar" alarm-zones="0:8:token(tone-success) 8:10:token(tone-danger)"/>
```

See also: [DvRing](#dvring) · [shared rules](#instrument-common)

<a id="dvring"></a>
## DvRing

A thick arc fill.

**Attributes**

| attribute | values | default | bound | notes |
|---|---|---|---|---|
| `value` | number | `0` | yes | One-way. Drawn clamped to `min`..`max` |
| `min` | number | `0` | yes | `min >= max` = 0..100 + warning |
| `max` | number | `100` | yes | As `min` |
| `alarm-zones` | zone list | none | yes | A zone color wins over `tone` ([alarm-zones](#alarm-zones)) |
| `tone` | theme word | `info` | yes | Fill `tone-{tone}`. Unknown = `info` + warning |
| `unit` | text | empty | yes | After the value text with one space |
| `label` | text | empty | yes | Drawn below the value |
| `sweep` | number (degrees) | `270` | yes | Clamped to 1..360. `360` = a full circle starting at 12 o'clock |
| `thickness` | number (px) | 11% of size | yes | Absent or `<= 0` = 11% of the size |
| `format` | .NET format string | none | yes | Value text digits |
| `show-value` | bool | `true` | yes | Value (+ unit) in the center. `false` hides it; `label` stays |

**Behavior**
- Default size: 120×120.
- Fill color = current zone color, otherwise `tone-{tone}`.

**Example**
```xml
<DvRing value="{Load}" unit="%" label="Load" tone="success"/>
```

See also: [DvGauge](#dvgauge) · [DvProgress](#dvprogress)

<a id="dvprogress"></a>
## DvProgress

A progress bar.

**Attributes**

| attribute | values | default | bound | notes |
|---|---|---|---|---|
| `value` | number | `0` | yes | One-way. Drawn clamped to `min`..`max` |
| `min` | number | `0` | yes | `min >= max` = 0..100 + warning |
| `max` | number | `100` | yes | As `min` |
| `tone` | theme word | `info` | yes | Fill `tone-{tone}`. Unknown = `info` + warning |
| `direction` | `right` `left` `up` `down` | `right` | yes | Fill direction; `up`/`down` = vertical. Unknown = `right` + warning |
| `show-value` | bool | `false` | yes | Value (+ unit) in the center |
| `unit` | text | empty | yes | After the value text with one space |
| `format` | .NET format string | none | yes | Value text digits |
| `alarm-zones` | zone list | none | yes | A zone color wins over `tone` ([alarm-zones](#alarm-zones)) |

**Behavior**
- Default size: 200×12 (vertical 12×120).
- `show-value="true"`: horizontal = shown when the height is 14 or more; vertical = only when the text fits in the bar's width.
- Fill color = current zone color, otherwise `tone-{tone}`.
- Corners follow `border-radius` (default pill shape).

**Example**
```xml
<DvProgress value="{BatchPercent}" show-value="true" unit="%" style="height:16"/>
```

See also: [DvSlider](dvml-widgets.md#dvslider) · [DvStepBar](dvml-widgets.md#dvstepbar)

<a id="alarm-zones"></a>
## alarm-zones

Colored value bands on `DvGauge`, `DvRing`, `DvProgress` and `DvTank`.

**Behavior**
- Format: a space-separated list of `from:to:color`, with `from < to` and `from` in ascending order.
- Colors: `#RGB` / `#RRGGBB` / `#RRGGBBAA`, or a theme color `token(name)` / `var(--name)` (`0:85:token(tone-success) 85:100:token(tone-danger)`). An unknown token draws magenta + warning ([magenta](dvml-diagnostics.md#magenta)).
- The whole attribute can be bound (`alarm-zones="{Zones}"`, a string in the same format). A null or empty bound value = no zones.
- A zone that cannot be read (not three parts, not a number, `from >= to`, a `from` below the previous zone, not a color), literal or bound, is dropped + Trace warning; the other zones stay and the screen appears.
- Custom widgets: `GaugeZone.ParseZones` throws `InvalidOperationException` on the first bad zone and does not resolve `token(...)` — run `ThemeResolver.ResolveVars` first ([custom widgets](dvml-components.md#dvwidget)).

**Example**
```xml
<DvTank value="{Level}" alarm-zones="0:70:#3b82f6 70:88:#f59e0b 88:100:#ef4444"/>
```

See also: [DvGauge](#dvgauge) · [DvTank](#dvtank) · [color values](dvml-language.md#value-color)

<a id="dvlamp"></a>
## DvLamp

An indicator lamp.

**Attributes**

| attribute | values | default | bound | notes |
|---|---|---|---|---|
| `on` | bool | `false` | yes | Lamp on |
| `blink` | bool | `false` | yes | Blinks while on |
| `blink-interval` | number (ms) | `500` | yes | Rounded. `<= 0` = 500 |
| `color` | `#RRGGBB`, `token(name)`, `var(--name)` | none | yes | On color. Wins over `state`. Unreadable = ignored + warning |
| `state` | theme word | none | yes | On color `state-{state}` (built-in: `run` `stop` `fault` `warn` `manual` `off`). No such token = no state color + warning |

**Behavior**
- Default size: 24×24.
- On when `on="{Running}"` is true. With `blink="true"` it blinks while on, every `blink-interval` ms; all blinking on the screen shares the same phase.
- On-color priority: `color` > `state` (`state-{state}` token — the built-in words, or any `state-*` token of your theme) > `color` in `style`.
- An unreadable `color`, or a `state` the theme has no `state-*` token for, is ignored + Trace warning (the lamp does not draw magenta).
- Off color = the `lamp-off` token (dark gray; light gray in `Graphite`).
- **Multi-state lamp** (equipment state number → color, text, blinking): no separate widget. The VM converts the number into a `state` key, a text and a blink flag and binds them (the state table is app data that differs per equipment). Lamp + name + status chip = [DvStatusItem](dvml-widgets.md#dvstatusitem). A color not in the theme: `DvLamp color="{PumpColor}"`.

**Example**
```xml
<DvLamp on="{Running}" state="run"/>
<DvStatusItem label="Pump 2" state="{Pump2.StateKey}" status="{Pump2.StateText}" blink="{Pump2.IsFault}"/>
```
```csharp
public int State { get; set; }   // state number sent by the equipment
public string StateKey => State switch { 1 => "run", 2 => "fault", 3 => "manual", 4 => "warn", _ => "stop" };   // state-* token
public string StateText => State switch { 1 => "Running", 2 => "Fault", 3 => "Manual", 4 => "Warning", _ => "Stopped" };
public bool IsFault => State == 2;
```

See also: [DvStatusItem](dvml-widgets.md#dvstatusitem) · [theme tokens](dvml-style.md#theme-tokens)

<a id="dvtank"></a>
## DvTank

A cylindrical vessel with a liquid level.

**Attributes**

| attribute | values | default | bound | notes |
|---|---|---|---|---|
| `value` | number | `0` | yes | One-way. Drawn clamped to `min`..`max` |
| `min` | number | `0` | yes | `min >= max` = 0..100 + warning |
| `max` | number | `100` | yes | As `min` |
| `alarm-zones` | zone list | none | yes | Liquid color per zone ([alarm-zones](#alarm-zones)) |
| `unit` | text | empty | yes | After the value text with one space |
| `format` | .NET format string | none | yes | Value chip digits. The chip is always drawn (no `show-value`) |
| `show-zone-marks` | bool | `true` | yes | `false` hides the zone boundary ticks and numbers on the right; zone colors stay |

**Behavior**
- Default size: 80×160.
- Value chip in the center (+ `unit`).
- Liquid color = current zone color; no zone = the `tank-fill` token.
- A process-diagram vessel is a different tag: [DvVessel](dvml-process-equipment.md#equipment).

**Example**
```xml
<DvTank value="{Level}" unit="%" alarm-zones="0:70:#3b82f6 70:88:#f59e0b 88:100:#ef4444"/>
```

See also: [alarm-zones](#alarm-zones) · [shared rules](#instrument-common)

<a id="charts"></a>
## Charts

`DvSparkline`, `DvBarChart` and `DvPieChart` are display-only widgets that draw one series. For values over time use [DvTrend](#dvtrend).
- Values are read at layout time — after changing a VM list, call `Invalidate()` (automatic inside a click handler).
- Pressing does nothing.

<a id="dvsparkline"></a>
### DvSparkline

A small line with no axes or ticks (a trend in a table cell or card header).

**Attributes**

| attribute | values | default | bound | notes |
|---|---|---|---|---|
| `values` | number list or `"3,5,4,8"` | none | yes | Anything else = no values. An unreadable number = 0 |
| `tone` | theme word | `info` | yes | Line color `tone-{tone}`. Unknown = `info` + warning |
| `y-min` | number | automatic | yes | Absent = data minimum |
| `y-max` | number | automatic | yes | Absent = data maximum |
| `area` | bool | `false` | yes | Translucent fill under the line |

**Behavior**
- Default size: 120×32.
- A dot on the last point.

**Example**
```xml
<DvSparkline values="{RecentFlow}" area="true" tone="success"/>
<DvSparkline values="3,5,4,8"/>
```

See also: [DvTrend](#dvtrend)

<a id="dvbarchart"></a>
### DvBarChart

Vertical bars.

**Attributes**

| attribute | values | default | bound | notes |
|---|---|---|---|---|
| `items` | list | none | yes | A string or a non-collection = no items |
| `item-value` | property path | the item | no | Read raw. Number path; an unreadable number = 0 |
| `item-text` | property path | the item as text | no | Read raw. Name below the bar (ellipsis if longer than the cell) |
| `tone` | theme word | `info` | yes | Bar color `tone-{tone}`. Unknown = `info` + warning |
| `y-min` | number | automatic | yes | Absent = includes 0 and the data |
| `y-max` | number | automatic | yes | As `y-min` |
| `show-value` | bool | `false` | yes | Value above each bar |
| `format` | .NET format string | none | yes | The `show-value` numbers |

**Behavior**
- Default size: 320×200.
- Automatic range includes 0 and the data; negative values go below the baseline.
- `show-value="true"` numbers: integers as integers, otherwise one decimal place, unless `format`.

**Example**
```xml
<DvBarChart items="{DailyOutput}" item-value="Count" item-text="Day" show-value="true"/>
```

See also: [DvPieChart](#dvpiechart)

<a id="dvpiechart"></a>
### DvPieChart

A circle (or donut) + legend.

**Attributes**

| attribute | values | default | bound | notes |
|---|---|---|---|---|
| `items` | list | none | yes | A string or a non-collection = no items |
| `item-value` | property path | the item | no | Read raw. An unreadable number = 0 |
| `item-text` | property path | the item as text | no | Read raw. Legend name |
| `show-value` | bool | `false` | yes | Legend shows the values (and the donut total) instead of percentages only |
| `format` | .NET format string | none | yes | The `show-value` numbers and the donut total (not the percentages) |
| `inner-radius` | number (ratio) | `0` | yes | Center hole, clamped to 0..0.9 (`0.55` → donut) |

**Behavior**
- Default size: 320×180.
- Legend on the right: color dot, name, percentage (+ value with `show-value="true"`).
- Slices go clockwise from 12 o'clock, colored with the `chart-1` … `chart-6` tokens in order (the trend default colors). No `tone`.
- Values of 0 or less are not drawn.
- With `show-value` on a donut, the total is shown in the center.
- The legend is drawn only when width − height ≥ 100; otherwise only the circle.

**Example**
```xml
<DvPieChart items="{EnergyShare}" item-value="Kwh" item-text="Line" inner-radius="0.55" show-value="true"/>
```

See also: [DvBarChart](#dvbarchart) · [theme tokens](dvml-style.md#theme-tokens)

<a id="dvtrend"></a>
## DvTrend

A time-series chart for real-time or historical values. Children are `DvTrendChannel` elements (one line each).

**Attributes**

| attribute | values | default | bound | notes |
|---|---|---|---|---|
| `source` | `TrendBuffer` | none | yes | Real-time data. Any other type = no buffer data, silently |
| `window` | number (seconds) | `60` | yes | Visible time span. Less than 1 second allowed (`0.1`). `<= 0` = 60 |
| `end` | `DateTime` binding | latest sample | yes | Time at the right edge. Any other type (`DateTimeOffset`, text) is ignored silently |
| `y-min` | number | automatic | yes | Vertical range |
| `y-max` | number | automatic | yes | Vertical range |
| `area` | bool | `false` | yes | Fill below the line |
| `smooth` | bool | `true` | yes | Curve interpolation. Empty = off |

**Behavior**
- Default size: 480×240. Grid line color: the `trend-grid` token.
- **One vertical axis is shared by all channels.** For values on very different scales (40 ℃ and 4 bar) use two `DvTrend`s, or scale the values when adding them.
- Real-time: the VM holds a [TrendBuffer](#trendbuffer), adds samples, then calls `Invalidate()`.
- History: bind an `IEnumerable<TrendPoint>` to a channel's `points` and pin the right edge with `end="{EndTime}"`.
- `double.NaN` breaks the line.
- No data → "No data" (`dv.trend.empty`, [localization](dvml-app.md#localization)).

**Example**
```xml
<DvTrend source="{Trend}" window="120" area="true">
  <DvTrendChannel name="temp" label="Temperature" unit="℃" color="#22d3ee"/>
  <DvTrendChannel name="press" label="Pressure" points="{PressHistory}"/>
</DvTrend>
```

See also: [DvTrendChannel](#dvtrendchannel) · [TrendBuffer](#trendbuffer) · [DvSparkline](#dvsparkline)

<a id="dvtrendchannel"></a>
### DvTrendChannel

One line of a `DvTrend` (child element; takes only these attributes + `id`).

**Attributes**

| attribute | values | default | bound | notes |
|---|---|---|---|---|
| `name` | text | `ch0`, `ch1` … | yes | Channel name looked up in the `source` buffer |
| `label` | text | `name` | yes | Legend name |
| `unit` | text | empty | yes | Legend only: shown after the latest value. The legend value is rounded to 2 decimals (`1191.03`); there is no `format` here — round the values you add to the buffer for fewer digits |
| `color` | `#RRGGBB`, `token(name)`, `var(--name)` | `chart-1` … `chart-6` in channel order | yes | Unknown token = magenta + warning. Unparsable = that palette color + warning |
| `points` | `IEnumerable<TrendPoint>` | none | yes | Wins over `source`. Any other type = from `source` by `name` |

**Behavior**
- The legend shows `label` (absent = `name`) and the latest value + `unit`.

**Example**
```xml
<DvTrend source="{Trend}">
  <DvTrendChannel name="flow" label="Flow" unit="m3/h" color="token(chart-2)"/>
</DvTrend>
```

See also: [DvTrend](#dvtrend)

<a id="trendbuffer"></a>
### TrendBuffer

The in-memory real-time store bound to `DvTrend source`.

**Behavior**
- `new TrendBuffer(capacity)` (default 3600 samples per channel).
- `TrendBuffer.Add(channel, time, value)` is thread-safe; call `Invalidate()` after adding ([threads](dvml-app.md#threads)).
- Samples are `TrendPoint(DateTime Time, double Value)`; the same type is used for `points` history.

**Example**
```csharp
using Devinno.UI.Trends;

public TrendBuffer Trend { get; } = new();
public void OnSample(double temp) { Trend.Add("temp", DateTime.Now, temp); }   // then Invalidate()
```

See also: [DvTrend](#dvtrend)

<a id="dvalarmbanner"></a>
## DvAlarmBanner

A one-line bar showing the top unacknowledged alarm and the unacknowledged count. The whole bar is the ACK target; it draws no ACK button. Raising, storing and ACKing alarms is the app's (server's) job; the alarm widgets only display them and pass ACK requests to the VM.

**Attributes**

| attribute | values | default | bound | notes |
|---|---|---|---|---|
| `items` | `IEnumerable<AlarmRow>` | none | yes | Any other type = no rows, silently |
| `onack` | method name | none | no | `M(AlarmRow)` or `M(long id)`. A missing method is silent |
| `show-ack` | bool | on when `onack` is set | yes | Whether a press on the bar acknowledges. Present = the [bool rule](dvml-language.md#value-bool) with default on: empty, `false` or `0` = presses do nothing. The drawing is the same either way |
| `comm-alarm` | text | empty | yes | Non-empty = a blinking communication-fault strip with this text above the banner |

**Behavior**
- Default size: 480 × 40 (+24 with `comm-alarm`).
- `AlarmRow` = `Id` `Time` `Priority` (`Critical` `Major` `Minor`) `Message` `Source` `Acked` `AckTime`.
- `items` is read at layout time — call `Invalidate()` when it changes.
- Order: unacknowledged → priority → newest. The banner shows the first such alarm and the unacknowledged count.
- **ACK = pressing the bar.** With `onack` set (and `show-ack` not off), a press anywhere on the banner, the communication-fault strip included, calls `onack` with that representative alarm. There is no ACK button and nothing on the bar marks it as pressable; if operators need a visible ACK control, put a `DvAlarmList` (one ACK button per row) or your own button beside it.
- With no unacknowledged alarm, a press does nothing.
- Color tokens: `alarm-critical` `alarm-major` `alarm-minor` (+ `-fg`), `comm-alarm` (+ `comm-alarm-fg` text) ([theme tokens](dvml-style.md#theme-tokens)).
- Asking for confirmation of an ACK is done in the `onack` handler with a message box, not by an attribute ([confirm-before-ACK pattern](dvml-dialogs.md#dialog-patterns)).

**Example**
```xml
<DvAlarmBanner items="{Alarms}" onack="Ack" comm-alarm="{CommText}"/>
```

See also: [DvAlarmList](#dvalarmlist) · [AlarmLog](#alarmlog)

<a id="dvalarmlist"></a>
## DvAlarmList

A list of alarms with an ACK button per row.

**Attributes**

| attribute | values | default | bound | notes |
|---|---|---|---|---|
| `items` | `IEnumerable<AlarmRow>` | none | yes | Any other type = no rows, silently |
| `onack` | method name | none | no | `M(AlarmRow)` or `M(long id)`. A missing method is silent |
| `show-ack` | bool | shown when `onack` is set | yes | See Behavior |
| `row-height` | number (px) | `36` | yes | `<= 0` = 36. The geometry keeps at least 24 |

**Behavior**
- Default size: 560 × (30 + 5 × `row-height`).
- Order and `AlarmRow` fields: as in [DvAlarmBanner](#dvalarmbanner).
- `onack="Ack"` → `public void Ack(AlarmRow row)` or `public void Ack(long id)`; the list calls it with the row whose ACK button was tapped.
- `show-ack` = show the ACK button. Omitted = shown when `onack` is present. Given = the [bool rule](dvml-language.md#value-bool) with default shown: empty, `false` or `0` = hidden; any other non-bool text = shown + Trace warning. Bindable (show per permission).
- After an `await` inside a handler the screen redraws automatically.

**Example**
```xml
<DvAlarmList items="{Alarms}" onack="Ack" show-ack="{CanAck}"/>
```
```csharp
using Devinno.UI.Alarms;

public IReadOnlyList<AlarmRow> Alarms { get; set; } = [];
public bool CanAck { get; set; } = true;
public async void Ack(AlarmRow row) { await api.AckAsync(row.Id); Alarms = await api.GetAsync(); } // after await in a handler the screen redraws automatically
```

See also: [DvAlarmBanner](#dvalarmbanner) · [handler errors](dvml-language.md#handler-error)

<a id="alarmlog"></a>
## AlarmLog

An in-memory alarm store for standalone HMIs and demos.

**Behavior**
- Bind its rows: `items="{Alarms}"` with `public IReadOnlyList<AlarmRow> Alarms => Log.Rows;`.
- Thread-safe: `Raise`/`Ack` from any thread, then `Invalidate()`.
- `Raise(priority, message, source)` adds a row; `Ack(id)` acknowledges one; `AckAll()` acknowledges all; `UnAckedCount` counts the unacknowledged.
- `new AlarmLog(maxRows)` (default 500). When unacknowledged alarms alone overflow `maxRows`, it adds one Critical alarm shown with the `dv.alarm.saturated` text ([localization](dvml-app.md#localization)).

**Example**
```csharp
using Devinno.UI.Alarms;

public AlarmLog Log { get; } = new(maxRows: 200);
public IReadOnlyList<AlarmRow> Alarms => Log.Rows;
public void Ack(AlarmRow row) => Log.Ack(row.Id);
public void Trip() => Log.Raise(AlarmPriority.Critical, "Intake pump #2 overcurrent trip", "PLC1");
```

See also: [DvAlarmBanner](#dvalarmbanner) · [DvAlarmList](#dvalarmlist)
