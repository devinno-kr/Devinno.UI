# Devinno.UI process equipment reference

Covers: every process equipment tag (`DvVessel`, `DvPump`, `DvValve`, `DvValve3`, `DvInline`, `DvExchanger`, `DvTerminal`) with its kinds, ports, sizes, state and attributes; the common equipment attributes, labels and `id`; terminal direction; user symbols (`DvSymbol`) — definition, drawing, marks for copying a drawing, and definition errors.
Read first: [process diagrams at a glance](dvml-process.md#process-overview) · [connections](dvml-process.md#connections)
Not here: [`DvLine`](dvml-process.md#dvline) · [attachments (`DvAttach`, `DvAgitator`, `DvMotor`)](dvml-process.md#attachments) · [`DvInstrument`](dvml-process.md#dvinstrument) · [`DvSignal`](dvml-process.md#dvsignal) · [units (`DvUnit`, `DvPort`)](dvml-process.md#units) · [hints `row`·`rank`·`rotate`·`flip`](dvml-process-layout.md#hints) · [coordinates `x`·`y`](dvml-process-layout.md#fixed-placement) · [the level widget `DvTank`](dvml-instruments.md#dvtank)

<a id="equipment"></a>
## Equipment and common attributes

| Tag | kinds (first = default) | Ports | State |
|---|---|---|---|
| [`DvVessel`](#dvvessel) | `tank` `reactor` `silo` `hopper` `drum` `column` `open` | `top` `bottom` `side` | `level` (0–100 %) |
| [`DvPump`](#dvpump) | `centrifugal` `gear` `compressor` `blower` `isa` | `in` (left) `out` (right) | `running` |
| [`DvValve`](#dvvalve) | `gate` `ball` `butterfly` `control` `check` `globe` `plug` `diaphragm` `angle` `relief` | `in` `out` (`angle`·`relief`: `in` at the bottom) | `open` |
| [`DvValve3`](#dvvalve3) | `ball` `control` | `a` (common, left) `b` (straight, right) `c` (branch, bottom) | `open-b` (`a`↔`b`), `open-c` (`a`↔`c`) |
| [`DvInline`](#dvinline) | `flowmeter` `filter` `strainer` `sight` `reducer` `orifice` `mixer` `vent` `blind` | `in` `out` (`vent`·`blind`: `in` only) | none |
| [`DvExchanger`](#dvexchanger) | `shell` `plate` | `in` `out` (process, left → right) `in2` (top) `out2` (bottom) (heating/cooling medium) | none |
| [`DvTerminal`](#dvterminal) | none | one unnamed port | none (`text` is its tag text) |

A registered user symbol ([`DvSymbol`](#dvsymbol)) is equipment too. Equipment goes directly under `DvProcess` or a `DvUnit`.

**Common attributes.** Usable on every equipment tag, including user symbols. Terminals (`DvTerminal`) have no `kind`, `label` or `rotate` (writing them is build warning [DVML019](dvml-diagnostics.md#dvml019)) and `flip` has no effect on them; `text` belongs to terminals only.

| attribute | values | default | bound | notes |
|---|---|---|---|---|
| `name` | text | required | no | Unique within the process diagram (or unit). Without it the equipment cannot be connected (Trace error); if duplicated, the later one cannot be connected (Trace error; [DVML010](dvml-diagnostics.md#dvml010) at build). Written literally. Short ASCII (`T1`, `P101`); no dot `.` |
| `label` | text | — | yes | One-line name tag ([label](#equipment-label)). Not on terminals |
| `kind` | the tag's kinds | the first kind | yes | An unknown value: the first kind + Trace warning ([DVML011](dvml-diagnostics.md#dvml011) at build) |
| `rotate` | `0` `90` `180` `270` | `0` | yes | Clockwise. The drawing and the port faces rotate together. Other values: 0 + Trace warning. Not on terminals |
| `flip` | `h` · `v` · `hv` | no flip | yes | `h` = left-right, `v` = up-down, `vh` = `hv`. Flip is applied, then rotation. Other values: no flip + Trace warning |
| `row` · `rank` | integer | — | yes | Placement hints ([hints](dvml-process-layout.md#hints)) |
| `x` · `y` | px | — | yes | Only with `placement="fixed"`: the symbol center ([coordinate placement](dvml-process-layout.md#fixed-placement)). Ignored with a Trace warning in automatic placement |
| `visible` | true/false | true | yes | false = the equipment is not created. **Lines connected to it become "equipment does not exist" errors**, so they cannot be hidden along with it — do not hide equipment that has lines; express it with state instead |
| `onclick` · `hold` · `disabled` | — | — | — | Same as an ordinary button. Pressing the equipment body (the symbol and accessory area) triggers `onclick`; its label and `DvAttach` boxes do not |
| `class` · `style` · `hover-style` · `press-style` | — | — | — | Applied to the equipment body rectangle (a background paints a rectangle behind the symbol — not normally used) |
| `id` | identifier | — | no | Code handle ([equipment `id`](#equipment-id)) |

Children allowed inside equipment: `DvAttach`, `DvAgitator`, `DvMotor` only ([attachments](dvml-process.md#attachments)).

<a id="equipment-size"></a>
### Sizes

- Each kind has a fixed size in px (the kind tables below); it is the **symbol rectangle**. Sizes on these pages are at `font-size` 14; `font-size` on `DvProcess` scales text, not symbols ([text size](dvml-process-layout.md#text-size)).
- Only `DvVessel` takes `width`·`height` to resize its body ([`DvVessel`](#dvvessel)).
- The footprint used in placement adds accessories, the label, attached boxes and instruments ([automatic placement](dvml-process-layout.md#auto-layout)).

<a id="equipment-state"></a>
### State

| Tag | State attribute | Values | If omitted |
|---|---|---|---|
| `DvVessel` | `level` | 0–100 (%), number or numeric string; out of range clamped to 0 or 100 | 0 (empty) |
| `DvPump` | `running` | true/false, or a number (non-zero = running) | Stopped |
| `DvValve` | `open` | true/false, or an opening number (non-zero = open) | **Closed** |
| `DvValve3` | `open-b` · `open-c` | true/false, or a number (non-zero = open) | Closed |
| `DvAgitator` · `DvMotor` | `running` | true/false | Stopped |

- State attributes are bound: `running="{P1Run}"`, `open="{V1Open}"`, `level="{T1Level}"`. They follow the common boolean and number rules ([booleans](dvml-language.md#value-bool), [numbers](dvml-language.md#value-number)).
- They are booleans, not the UI state words (`state="run|stop|fault"`); link the two in the view model ([state words and process booleans](dvml-process.md#when-to-use)).
- Give `open="true"` even to an always-open valve. Without it, the valve is drawn closed.
- Look colors per state: [look](dvml-process.md#look-flow-medium).

<a id="equipment-label"></a>
### Label

- **Vessels**: a rounded chip in the middle of the body (readable over the liquid too), 14px text.
- **Everything else**: on a **face not used by lines** — the first free face in the order bottom → top → right → left.
  - The actuator side of a control valve is not free (signal lines end there).
  - If all four faces are used: bottom; if it would cover the front of a port there, it shifts along the face to stay at least 12px from the port (the exchanger's label, for example).
- 4px from the symbol (6px left/right). Font 13 bold (17px tall at `font-size` 14); color is the theme `--text`.
- One line only: `&#10;` does not break it and draws a box character.
- Terminals have no `label`; they use `text` ([`DvTerminal`](#dvterminal)).

<a id="equipment-id"></a>
### `name` and `id`

- `name` connects lines and instruments; it must not be duplicated within a process diagram. Lines never connect by `id`.
- `id` is separate: it gives a `DvControl` handle (`Visible`, `Enabled`, `Bounds`, `Click`) like any box ([handles](dvml-code.md#handles), [`id` and `vm`](dvml-code.md#code-id-vm)). An `id` on a unit component instance gives a handle that acts on that unit.
- Prefer VM binding for state and `onclick` for actions.

<a id="ports"></a>
## Ports

Port names are fixed: vessels `top` `bottom` `side`; `DvValve3` `a` (common) `b` `c`; `DvExchanger` `in` `out` (process) `in2` `out2` (heat medium); inline `vent`·`blind` only `in`; everything else `in` `out`; terminals have no port, only `name`. User symbols use the names they define. A line writes `equipment.port`; a terminal or a one-port symbol (or `vent`·`blind`) by name only. A point you choose on a face: [face position](dvml-process.md#face-position).

**Vessel ports** (the canonical rule for nozzle merging):

| Port | Position | When several lines connect |
|---|---|---|
| `top` | Top face (face port) | **Incoming lines merge into one nozzle and outgoing lines merge into one nozzle** (they gather into a T or cross in front of the tank). Nozzles lie in the 10–90 % horizontal span of the top face, avoiding the spot of an agitator or a box attached on top. If there are both an incoming and an outgoing nozzle, they sit side by side ordered by the direction of their counterparts |
| `bottom` | One point at the bottom center | Splits from that one point (branch) |
| `side` | Left or right face (face port) | For each line, picks **the face toward the counterpart equipment** (left if the counterpart is to the left). If that face is mostly covered by an attached box, the opposite face. Lines on the same face in the same direction (in/out) merge into one nozzle |

- Incoming and outgoing lines are never merged (merging would make the inlet and outlet look connected outside the tank). To show separate nozzles, split the connections between `top` and `side`, or use face positions.
- Fixed ports (`bottom`, `in`, `out` …): several lines split from that single point.
- With an accessory (`DvAgitator`, `DvMotor`), the vessel's `top` port leaves the center empty and takes positions on both sides.
- Where each port sits on each kind: the tag sections below. Port line offsets from the symbol center for coordinate placement: [port offsets](dvml-process-layout.md#port-offsets).

<a id="dvvessel"></a>
## DvVessel — vessel

A tank, reactor, silo, hopper, drum, column or open basin. Not the level widget `DvTank`.

| attribute | values | default | bound | notes |
|---|---|---|---|---|
| `kind` | `tank` · `reactor` · `silo` · `hopper` · `drum` · `column` · `open` | `tank` | yes | Shapes and sizes below |
| `level` | 0–100 | 0 | yes | Fill in %; clamped; number or numeric string |
| `height` | px | the kind's height | yes | Only the straight body grows or shrinks — the heads keep their shape and the width stays the same. 0.6–4 × the kind's height (outside: clamped + Trace warning) |
| `width` | px | the kind's width | yes | Upright kinds widen evenly (the heads become wider or narrower ellipses); the horizontal `drum` grows only its straight body, keeping its left and right heads. Height stays the same. 0.6–4 × the kind's width (outside: clamped + Trace warning). Ports, the level fill and accessories follow |
| `name` · `label` · `rotate` · `flip` · `row` · `rank` · `x` · `y` · `visible` | — | — | — | [Common attributes](#equipment) |

| kind | Size (px) | Shape |
|---|---|---|
| `tank` (default) | 100 × 150 | Vertical cylinder rounded at top and bottom |
| `reactor` | 100 × 160 | Reactor with a jacket (an added outer wall) |
| `silo` | 100 × 160 | Conical roof + bottom funnel + legs |
| `hopper` | 100 × 110 | Funnel open at the top |
| `drum` | 160 × 80 | Horizontal drum (receiver, knock-out drum) with saddles |
| `column` | 60 × 220 | Tall tower with dashed trays |
| `open` | 120 × 90 | Open-top tank or basin (no roof line) |

- Ports: `top`, `bottom`, `side` ([vessel ports](#ports)).
- `width`·`height` are the symbol rectangle. On a `tank`, the body outline is 88 % of the width and 8px less than the height, with a small nozzle stub at top and bottom center reaching the rectangle. To match a drawn body W px wide, set `width` = W ÷ 0.88.
- The level fills from the bottom following the body shape (rounded bottoms are filled round).
- The label is a rounded chip in the middle of the body ([label](#equipment-label)).
- `DvAgitator`·`DvMotor`·`DvAttach` can be children ([attachments](dvml-process.md#attachments)).

```xml
<DvProcess>
  <DvTerminal name="IN" text="FEED"/>
  <DvVessel name="T1" kind="tank" level="{T1Level}" label="T-101"/>
  <DvTerminal name="OUT" text="TO PUMP"/>
  <DvLine from="IN" to="T1.top" arrow="end"/>
  <DvLine from="T1.bottom" to="OUT"/>
</DvProcess>
```

<a id="dvpump"></a>
## DvPump — rotating equipment

Pumps, compressors and blowers.

| attribute | values | default | bound | notes |
|---|---|---|---|---|
| `kind` | `centrifugal` · `gear` · `compressor` · `blower` · `isa` | `centrifugal` | yes | Shapes, sizes and port heights below |
| `running` | true/false, or a number | stopped | yes | Non-zero = running |
| `name` · `label` · `rotate` · `flip` · `row` · `rank` · `x` · `y` · `visible` | — | — | — | [Common attributes](#equipment) |

| kind | Size (px) | `in` | `out` | Shape |
|---|---|---|---|---|
| `centrifugal` (default) | 64 × 64 | Left, 36px from top | Right, 20px from top | Round casing + tangential discharge at top + base |
| `gear` | 72 × 52 | Left center | Right center | Rounded rectangle + two gears |
| `compressor` | 72 × 64 | Left center | Right center | Circle + two converging lines |
| `blower` | 72 × 64 | Left, 36px from top | Right, 19px from top | Round casing + blades |
| `isa` | 64 × 64 | Left center | Right center | ISA-5.1 pump: circle + centerline + `>` toward the outlet (as on P&ID drawings); pair with `DvMotor kind="isa"` |

- **For a pump whose flow is right → left**, use `flip="h"` to put `in` on the right.
- In a top → bottom (`tb`) process, pumps are drawn horizontal by default (inlet left, outlet right). Parallel pumps stack vertically ([automatic placement](dvml-process-layout.md#auto-layout)).
- The drive motor is part of the symbol; put a `DvMotor` inside only when the drawing shows one ([attachments](dvml-process.md#attachments)).

```xml
<DvProcess>
  <DvVessel name="T1" level="40" label="T-101"/>
  <DvPump name="P1" kind="gear" running="{P1Run}" label="P-101"/>
  <DvTerminal name="OUT" text="PRODUCT"/>
  <DvLine from="T1.bottom" to="P1.in"/>
  <DvLine from="P1.out" to="OUT"/>
</DvProcess>
```

<a id="dvvalve"></a>
## DvValve — valve

Straight kinds are 60 × 48px, with `in` on the left and `out` on the right, both 32px from the top (the pipe centerline). `angle` and `relief` are angle bodies: `in` at the bottom, `out` on the right (`flip="h"` puts `out` on the left).

| attribute | values | default | bound | notes |
|---|---|---|---|---|
| `kind` | `gate` · `ball` · `butterfly` · `control` · `check` · `globe` · `plug` · `diaphragm` · `angle` · `relief` | `gate` | yes | Shapes below |
| `open` | true/false, or an opening number | **closed** | yes | Non-zero = open. `check` has no state |
| `name` · `label` · `rotate` · `flip` · `row` · `rank` · `x` · `y` · `visible` | — | — | — | [Common attributes](#equipment) |

| kind | Shape |
|---|---|
| `gate` (default) | Bowtie + handle on top |
| `ball` | Bowtie + ball in the middle + lever |
| `butterfly` | Body + tilted disc + actuator on top |
| `control` | Bowtie + dome-shaped actuator on top (control valve; signal lines end on top of the actuator) |
| `check` | Body + triangle (non-return, no state; flow one-way `in` → `out`) |
| `globe` | Bowtie + filled dot + handwheel (throttling) |
| `plug` | Bowtie + hollow plug + lever |
| `diaphragm` | Bowtie + dome (diaphragm) + handwheel |
| `angle` | 48 × 48. Two triangles meeting at a right angle + handwheel. `in` bottom, `out` right (20px from top) |
| `relief` | 48 × 64. Angle body + spring (pressure safety / relief valve, PSV). `in` bottom, `out` right (36px from top). `open` = relieving |

- Give `open="true"` even to an always-open valve. Without it, the valve is drawn closed.
- A valve on a vertical pipe uses `rotate="90"` (enters from the top, leaves at the bottom).

```xml
<DvProcess>
  <DvTerminal name="IN" text="FEED"/>
  <DvValve name="V1" kind="ball" open="{V1Open}" label="XV-101"/>
  <DvTerminal name="OUT" text="PRODUCT"/>
  <DvLine from="IN" to="V1.in"/>
  <DvLine from="V1.out" to="OUT"/>
</DvProcess>
```

<a id="dvvalve3"></a>
## DvValve3 — three-way valve

Splits one pipe into two (branch) or gathers two into one (mixing). Both kinds are 60 × 64px.

| attribute | values | default | bound | notes |
|---|---|---|---|---|
| `kind` | `ball` · `control` | `ball` | yes | `ball` = three-way bowtie + ball in the middle + lever; `control` = three-way bowtie + dome-shaped actuator on top (the signal line reaches the top of the actuator) |
| `open-b` | true/false, or a number | closed | yes | Non-zero = open: `a`↔`b` passes |
| `open-c` | true/false, or a number | closed | yes | Non-zero = open: `a`↔`c` passes |
| `name` · `label` · `rotate` · `flip` · `row` · `rank` · `x` · `y` · `visible` | — | — | — | [Common attributes](#equipment) |

| Port | Position | Meaning |
|---|---|---|
| `a` | Left, 32px from top | Common port |
| `b` | Right, 32px from top | Straight branch |
| `c` | Bottom center | Turning branch |

- The heights of `a`·`b` match `in`·`out` of `DvValve` (in line with ordinary valves).
- Both true: both paths are open (mixing or modulating); both false: blocked. Only the triangles of open branches are painted in the state color (the common `a` side is always the same color).
- Paths pass in both directions. The same tag serves a **branch** (`from="P1.out" to="V3.a"`, `from="V3.b" …`, `from="V3.c" …`) and **mixing** (`to="V3.b"`, `to="V3.c"`, `from="V3.a" …`).
- **For mixing in a left → right process, use `flip="h"`** to put the common `a` on the right (the side where flow leaves). Otherwise the incoming pipes wrap around the valve to get in.
- In a top → bottom (`tb`) process, use `rotate="90"` (`a` top, `b` bottom, `c` left).

```xml
<DvProcess>
  <DvVessel name="T1" level="{T1Level}" label="T-1"/>
  <DvPump name="P1" running="{P1Run}" label="P-1"/>
  <DvValve3 name="V3" kind="control" open-b="{ToCooler}" open-c="{ToDrain}" label="TV-301"/>
  <DvExchanger name="E1" label="E-101"/>
  <DvTerminal name="CWS" text="CW SUPPLY"/>
  <DvTerminal name="CWR" text="CW RETURN"/>
  <DvVessel name="T2" level="{T2Level}" label="T-2"/>
  <DvTerminal name="DR" text="DRAIN"/>
  <DvLine from="T1.bottom" to="P1.in"/>
  <DvLine from="P1.out" to="V3.a"/>
  <DvLine from="V3.b" to="E1.in"/>
  <DvLine from="V3.c" to="DR"/>
  <DvLine from="CWS" to="E1.in2"/>
  <DvLine from="E1.out2" to="CWR"/>
  <DvLine from="E1.out" to="T2.top"/>
</DvProcess>
```

<a id="dvinline"></a>
## DvInline — inline equipment

Flowmeters, filters, strainers and other pieces sitting in a pipe. No state.

| attribute | values | default | bound | notes |
|---|---|---|---|---|
| `kind` | `flowmeter` · `filter` · `strainer` · `sight` · `reducer` · `orifice` · `mixer` · `vent` · `blind` | `flowmeter` | yes | Shapes, sizes and port heights below |
| `name` · `label` · `rotate` · `flip` · `row` · `rank` · `x` · `y` · `visible` | — | — | — | [Common attributes](#equipment) |

| kind | Size (px) | `in` / `out` height | Shape |
|---|---|---|---|
| `flowmeter` (default) | 64 × 48 | 32px from top | Pipe + display on top (flowmeter) |
| `filter` | 60 × 60 | Center (30px) | Diamond + dashed line |
| `strainer` | 64 × 56 | 20px from top | Y-type strainer |
| `sight` | 64 × 48 | Center (24px) | Pipe + round sight glass |
| `reducer` | 44 × 40 | Center (20px) | Concentric reducer, wide `in` → narrow `out` (`flip="h"` for an expander) |
| `orifice` | 40 × 44 | Center (22px) | Orifice plate between two flanges |
| `mixer` | 72 × 40 | Center (20px) | Static mixer (zig-zag elements) |
| `vent` | 32 × 40 | **One port `in`, at the bottom** | Open vent to atmosphere (hood). Connect by name only: `to="VT1"` |
| `blind` | 16 × 44 | **One port `in`, on the left** | Blind flange / manhole (closed nozzle). Connect by name only: `to="M1"` |

- Inline equipment always passes flow.
- Put flow numbers in `<DvAttach side="bottom">` or `side="top"` (the sides hold the ports).

```xml
<DvProcess>
  <DvTerminal name="IN" text="FEED"/>
  <DvInline name="F1" kind="flowmeter" label="FT-101">
    <DvAttach side="bottom"><DvLabel class="label" text="{FlowText}"/></DvAttach>
  </DvInline>
  <DvTerminal name="OUT" text="PRODUCT"/>
  <DvLine from="IN" to="F1.in"/>
  <DvLine from="F1.out" to="OUT"/>
</DvProcess>
```

<a id="dvexchanger"></a>
## DvExchanger — heat exchanger

No state.

| attribute | values | default | bound | notes |
|---|---|---|---|---|
| `kind` | `shell` · `plate` | `shell` | yes | `shell` = 100 × 48, horizontal cylinder + tube bundle (shell and tube); `plate` = 64 × 80, stacked plates |
| `name` · `label` · `rotate` · `flip` · `row` · `rank` · `x` · `y` · `visible` | — | — | — | [Common attributes](#equipment) |

| Port | `shell` position | `plate` position | Meaning |
|---|---|---|---|
| `in` | Left center | Left, 24px from top | Main process inlet |
| `out` | Right center | Right, 24px from top | Main process outlet |
| `in2` | Top, 30 % from left | Top, 30 % from left | Heat medium (steam, cooling water) inlet |
| `out2` | Bottom, 70 % from left | Bottom, 70 % from left | Heat medium outlet |

- **`in`·`out` = the process fluid being heated or cooled**, **`in2`·`out2` = the medium that exchanges heat**. Which is shell side and which is tube side differs by design, so it is not in the names.
- The two paths (`in`→`out`, `in2`→`out2`) always pass and never mix. Even if the process side stops, flow on the medium side is judged separately.
- Medium supply and return usually connect to terminals. A terminal connected to the top port (`in2`) is placed above the exchanger, and one connected to the bottom port (`out2`) below it ([automatic placement](dvml-process-layout.md#auto-layout)).
- All four faces are used, so its label goes at the bottom, shifted sideways if it would cover the bottom port ([label](#equipment-label)). An attached box has no free face; show numbers on an instrument `value` instead.

```xml
<DvProcess>
  <DvTerminal name="IN" text="FEED"/>
  <DvExchanger name="E1" kind="shell" label="E-101"/>
  <DvTerminal name="OUT" text="PRODUCT"/>
  <DvTerminal name="CWS" text="CWS"/>
  <DvTerminal name="CWR" text="CWR"/>
  <DvLine from="IN" to="E1.in"/>
  <DvLine from="E1.out" to="OUT"/>
  <DvLine from="CWS" to="E1.in2"/>
  <DvLine from="E1.out2" to="CWR"/>
</DvProcess>
```

<a id="dvterminal"></a>
## DvTerminal — terminal

A tag (off-page connector) for a pipe that continues outside the screen.

| attribute | values | default | bound | notes |
|---|---|---|---|---|
| `text` | text | — | yes | Tag text (font 12 bold). The width fits the longest line. For two or more lines, put `&#10;` at each line break (`text="SOLVENT&#10;FROM UNIT 1"`); the tag grows one line height taller per extra line |
| `name` · `flip` · `row` · `rank` · `x` · `y` · `visible` | — | — | — | [Common attributes](#equipment); `flip` has no effect |

- No `kind`, `label` or `rotate` ([DVML019](dvml-diagnostics.md#dvml019)).
- Port: only one, and it has no name. In a line, write just the name, like `from="IN"`, `to="OUT"` (adding a dot gives a Trace warning and is ignored).
- A terminal cannot be a signal-line end or take a face position.

```xml
<DvProcess>
  <DvTerminal name="IN" text="SOLVENT&#10;FROM UNIT 1"/>
  <DvPump name="P1" running="true" label="P-1"/>
  <DvTerminal name="OUT" text="TO UNIT 2"/>
  <DvLine from="IN" to="P1.in"/>
  <DvLine from="P1.out" to="OUT"/>
</DvProcess>
```

<a id="terminal"></a>
### Terminal direction and shape

- **Used in a line's `from`, it means "coming in from outside"** (the pipe leaves from the tag's tip); used only in `to`, it means "going out". The arrow shape points in the flow direction.
- Use each terminal in one direction only. Incoming and outgoing pipes get separate terminals.
- Shape in `lr`: an arrow tag pointing right (height 28, width = text + 34, minimum 64). Incoming connects at the pointed end (right); outgoing at the flat left side.
- Shape in `tb`: a tag pointing down (height 38, width = text + 20, minimum 60). Incoming connects at the bottom tip, outgoing at the top.
- **Terminals cannot be rotated or mirrored.** So place them by the side they take their line on:
  - `direction="lr"`: a receiving terminal (a product, `TO FLARE`, `CWR`) takes its line on its left side; put it **downstream (to the right)** of the equipment that feeds it. A supplying terminal takes its line on its right side; put it upstream (to the left). A receiving terminal placed to the left makes the pipe loop around it.
  - `direction="tb"`: a receiving terminal takes its line on top and a supplying one at the bottom, so put them below and above their equipment.
- This matters in automatic placement too, and for `via` routes in coordinate placement.

<a id="dvsymbol"></a>
## DvSymbol — user symbols

Equipment that is not built in (cyclones, conveyors …) is used by defining a symbol. Built-in equipment is defined in exactly the same format. Never invent kinds: register a symbol instead, also when a built-in kind looks different from a drawing's symbol.

**Definition and registration**

```xml
<DvSymbol name="Cyclone" view-box="0 0 60 100">
  <DvParam name="running" type="bool"/>
  <DvShape class="state" when="running" path="M10 0 H50 V40 L35 90 H25 L10 40 Z"/>
  <DvShape class="detail" path="M14 12 H46"/>
  <DvPort name="in" side="left" at="0.2" role="in"/>
  <DvPort name="gas" side="top" at="0.5" role="out"/>
  <DvPort name="dust" side="bottom" at="0.5" role="out"/>
  <DvPass from="in" to="gas"/>
  <DvPass from="in" to="dust"/>
</DvSymbol>
```

```csharp
using Devinno.UI.Components;

var registry = new ComponentRegistry();
registry.Register(CycloneMarkup);   // the same Register as components: a root DvSymbol registers a symbol
// With DvApp use app.Registry; DvRenderer and DvSurface take the registry as an argument.
```

```xml
<DvProcess>
  <DvPump name="P1" running="true"/>
  <Cyclone name="CY1" running="{Cy1Run}" label="CY-1"/>
  <DvTerminal name="GAS" text="TO STACK"/>
  <DvTerminal name="DUST" text="TO BIN"/>
  <DvLine from="P1.out" to="CY1.in"/>
  <DvLine from="CY1.gas" to="GAS"/>
  <DvLine from="CY1.dust" to="DUST"/>
</DvProcess>
```

- Inside a process diagram, **the symbol `name` is the tag** (`<Cyclone …>`). All [common attributes](#equipment) are available, plus one attribute per `DvParam` (bound like any state attribute).
- Built-in tags win over a registered symbol of the same name (`DvVessel`, `DvPump`, `DvValve`, `DvValve3`, `DvInline`, `DvExchanger`, `DvTerminal`, or any other process tag) — do not start a symbol name with `Dv`.
- Registering the same name again replaces the definition. It is not applied to screens that are already built, so register first, then build screens.
- A tag that is not registered is not drawn and leaves the Trace error `<Tag> is not process equipment (…)`.

**Elements**

| Element · attribute | Description |
|---|---|
| `DvSymbol name` | Required. Name used as the tag |
| `DvSymbol view-box` | Required. `"0 0 W H"`: four numbers; the first two are ignored (write `0 0`); W and H (positive) are the on-screen size in px |
| `DvParam name type default` | A state value received from outside, bound via an instance attribute (`running="{…}"`). `type` is descriptive (not checked); `default` is the value when the attribute is not given |
| `DvShape class path when` | One drawing layer, drawn top to bottom (the one written first is behind). `path` = SVG path syntax; `class` = fill (table below), never a color in the path; `when` = name of the parameter a `state` layer follows |
| `DvPort name side at role` | Port: a point on the view-box border. `side` = `left`·`top`·`right`·`bottom`, `at` = position along that side 0–1 (left/top = 0), default 0.5. `role` = `in`·`out`·`inout` (default). Ports are written as side + position, not coordinates, because the side decides which way the pipe leaves |
| `DvPass from to when` | Flow rule: this port passes to that port, when the `when` parameter is true (always, without `when`). Flow display and `medium` follow these rules. Without any `DvPass`, flow stops at this symbol |
| `DvFill clip value` | Level fill, and the symbol counts as a vessel for flow. `clip` = the `class` used as the fill shape (the union of that class's layers), `value` = name of a 0–100 parameter. The liquid is painted right after the first layer of that class, so layers written later (agitator shaft, etc.) show on top of the liquid |

**`DvShape class`** — colors are set by class, so the same symbol is drawn in both looks, `plain` and `rich`.

| class | Fill | Used for |
|---|---|---|
| `vessel` | Body (no state) — plain gray, rich metal | Vessels, tubes, main bodies |
| `state` | Body; running color when the `when` parameter is true | Pump casings, valve bodies, motors |
| `accent` | Mid gray; a metal gradient in `rich` | Flanges, bases, handles, nozzles, small parts |
| `detail` | Stroke only (1.6px, round caps) | Blades, patterns, axis lines |
| `dash` | Dashed stroke only | Filter mesh, internal boundaries |
| `screen` | Display window: light gray in `plain`, near-black in `rich` | Instrument screens |
| `glass` | Light glass | Sight glasses |

- Filled layers (`vessel` `state` `accent` `screen` `glass`) are all drawn with an outline stroke.
- While a `state` layer is running, `detail` and `dash` strokes over that layer switch to a color that stays visible on the running body. The part outside it keeps the normal color.
- A `state` layer without `when` is always the stopped color.
- A symbol needs neither ports nor shapes. Without `DvPort`, no pipe connects to a port of it (face positions such as `N.left@0.5` and signal lines do connect). Without `DvShape`, it draws nothing.
- Paths may run outside the view-box. They are drawn, not clipped, but only the view-box counts for ports, pipes and overlaps. Equipment is drawn in the order written, so what is written later lies on top: write a tank's legs ahead of the tank to put their tops behind it.

<a id="symbol-drawing-marks"></a>
### Small symbols for copying a drawing

For coordinate placement. Do not put them over equipment, where they get an `overlap` warning.

- **Caption or number where the drawing has it**: an empty symbol (`<DvSymbol name="Mark" view-box="0 0 2 2"/>`) with a `label` or a `DvAttach`.
- **Corner or branch of a signal line**: an empty symbol at that point, with signal lines ending and starting on it (`<DvSignal from="A" to="J1"/>`, `<DvSignal from="J1" to="B"/>`, `<DvSignal from="J1" to="C"/>`). This also sets a signal line's route, and the face it leaves on.
- **Dashed line starting on a pipe**: a small symbol on the pipe, with `in`·`out` ports and a `DvPass` so flow continues, and a signal line from it (`<DvSignal from="Tap1" to="PI1"/>`).
- **Pipe starting or ending in a bare arrow and text, without a terminal tag**: a symbol that draws nothing, with a `DvFill` and a `label`. Flow ends at it, and with `level` above 0 also starts from it. Put its port on the side the pipe is on: `<DvSymbol name="PipeEnd" view-box="0 0 2 2"><DvParam name="level" default="0"/><DvPort name="p" side="left"/><DvShape class="vessel" path="M1 1 Z"/><DvFill clip="vessel" value="level"/></DvSymbol>`. The arrow is `DvLine arrow="end"`, or a small symbol of its own where the drawing's arrow stands apart from the pipe.
- **Leader line or note arrow**: a small symbol whose path runs outside its view-box. It is gray, like every symbol (colors come from the classes).
- **A number beside equipment whose faces all hold ports**: a `DvAttach` on an empty symbol placed beside it.

<a id="symbol-drawing"></a>
### How to draw a symbol (when an AI writes the path)

1. **Decide the size first.** Match proportions with built-in equipment: rotating equipment and valves 60–72 × 48–64, vessels 100 × 110–160. Too small (under 40) gets lost next to labels and pipes. To copy a drawing, take the drawing's proportions instead.
2. **Coordinate system**: top-left is (0, 0), bottom-right is (width, height). Use absolute commands (`M L H V Q C A Z`). Two decimal places are enough.
3. **Draw the body touching the port positions.** A port is a point on the border, and pipe ends tuck 6px under the body to be hidden. If the space in front of a port is empty (the body does not reach the border), the pipe end appears to float, so draw a nozzle (a small `accent` rectangle) reaching the border. Example: left-center port → `M0 26 H8 V38 H0 Z`.
4. **Layer order**: base/flanges (`accent`) → body (`vessel`/`state`) → pattern (`detail`) → small parts (`accent`). Layers written first lie underneath.
5. **Match port sides to the flow direction**: regular equipment has inlet `left`, outlet `right`. Equipment that receives from above and discharges below (cyclone dust, hoppers) uses `top`/`bottom`.
6. **Do not put rotation in the definition.** For a different orientation, use `rotate`·`flip` on the instance.
7. **Always render to check**: both looks (`plain`·`rich`), both running and stopped.

Circles and rectangles are paths too. Circle (center cx, cy, radius r): `M{cx-r} {cy} A{r} {r} 0 1 0 {cx+r} {cy} A{r} {r} 0 1 0 {cx-r} {cy} Z` → for center (30, 30), radius 10: `M20 30 A10 10 0 1 0 40 30 A10 10 0 1 0 20 30 Z`.

<a id="symbol-errors"></a>
### Definition errors

If a definition is wrong, `Register` throws an `ArgumentException` describing what is wrong (the screen does not open — it surfaces immediately at startup). Invalid XML gives the parser's exception instead.

| Words in the message | Cause |
|---|---|
| `requires a name` | `DvSymbol` has no `name`, or a `DvPort` has no `name` |
| `view-box` | Format is not `"0 0 W H"`, or W·H is 0 or less |
| `side` | Port side is not `left`·`top`·`right`·`bottom` |
| `at` | Port position outside 0–1 |
| `role` | Not `in`·`out`·`inout` |
| `duplicate port` | Two ports with the same name |
| `DvPass` | Flow rule points to a nonexistent port |
| `class` | Unknown `DvShape` class |
| `path` | SVG path syntax error |
| `clip` | `DvFill clip` matches no `DvShape` class |
| `unknown child` | Unknown child element (e.g., `DvCircle`) — circles and rectangles are written as paths |
