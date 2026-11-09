# Devinno.UI process diagrams (DvProcess)

Covers: process (P&ID-style) screens of tanks, pumps, valves and pipes with `<DvProcess>`: when to use it, choosing the placement mode, the authoring procedure, `DvProcess` attributes, connections (`DvLine`, face positions), attachments, instruments and signal lines, units, look, flow and medium, the view model, the checklist, what is not supported, and the FAQ.
Read first: [the rules every screen follows](llm-rules.md#principles) · [markup basics](dvml-language.md#markup-basics) · [binding syntax](dvml-language.md#binding)
Not here: [equipment tags, ports, sizes and user symbols](dvml-process-equipment.md#equipment) · [diagram size, `fit` and text size](dvml-process-layout.md#size-fit) · [how automatic placement and pipe routes are computed](dvml-process-layout.md#auto-layout) · [coordinate placement details](dvml-process-layout.md#fixed-placement) · [process Trace messages](dvml-process-layout.md#process-diagnostics) · [build diagnostics DVML008–011](dvml-diagnostics.md#build-diagnostics) · [complete examples and copying a given drawing](dvml-process-examples.md#process-examples) · [the level widget `DvTank`](dvml-instruments.md#dvtank)

These four pages (this one, [equipment](dvml-process-equipment.md#equipment), [layout](dvml-process-layout.md#size-fit) and [examples](dvml-process-examples.md#process-examples)) hold every process rule with exact numbers, so an AI can write a correct process screen from them alone. Any tag, attribute or value not on them **does not exist**. Do not invent them. A small diagram needs only the [fast path](#process-fast-path) below.

<a id="process-fast-path"></a>
## Small diagram fast path

For a **small diagram** (one line of up to about 8 pieces of equipment, flow in one direction, no lines back to earlier equipment, no units, no drawing to copy) the rules below are enough. Open a linked section only when you use that feature. For any other diagram, read this whole page.

1. Put `<DvProcess grow="1" style="height:0">` in a vertical layout (`DvPageFrame`, `DvStack`, `DvCard`) to fill the space below the other content. Do not write `placement`, `fit` or `align`.
2. Directly inside it go only equipment, `DvLine` and `DvInstrument`. Equipment: `DvVessel` (a process tank; `DvTank` is a dashboard widget and is not drawn here), `DvPump`, `DvValve kind="…"`, `DvInline kind="…"` (flowmeter and other in-line items), and a `DvTerminal text="…"` for each pipe entering or leaving the screen ([equipment and kinds](dvml-process-equipment.md#equipment)).
3. Give every piece of equipment a unique `name` of ASCII letters and digits without `.` (`T1`, `P1`); the text shown goes in `label`.
4. Write one `<DvLine from="A.port" to="B.port"/>` per pipe, `from` upstream. Ports: pump, valve, inline `in` and `out`; vessel `top` (into the top), `side` (in or out at the side), `bottom` (out of the bottom); a terminal by its name alone. **A tank feeding a horizontal line leaves from `side`** (`from="T1.side"`): the tank then stands on the line. A `bottom` outlet puts the next equipment at another height, and a taller area stretches that step ([why](#process-faq)).
5. Bind the state: pump `running="{…}"`, valve `open="{…}"`, vessel `level="{…}"` (0–100). Left out, they are drawn stopped, closed and empty.
6. A reading on a piece of equipment: `<DvInstrument name="FT1" tag="FT" number="101" on="F1" value="{FlowText}"/>` ([`DvInstrument`](#dvinstrument)). Buttons or other dvml beside equipment: `<DvAttach side="…">` on a face with no pipe ([attachments](#attachments)).
7. Optional look: `look="rich"`, `flow-animation="true"`, `medium="water"` on a line ([look, flow and medium](#look-flow-medium)).
8. Build (no DVML008–011), render a PNG at the smallest real screen size, and read the Trace warnings ([render and check](#procedure)). Any warning, or a picture that looks wrong, means the diagram needs the full [authoring procedure](#procedure).

```xml
<DvProcess grow="1" style="height:0" flow-animation="true">
  <DvVessel name="T1" level="{T1Level}" label="T-101"/>
  <DvPump name="P1" running="{P1Run}" label="P-101"/>
  <DvValve name="V1" kind="ball" open="{V1Open}" label="XV-101"/>
  <DvInline name="F1" kind="flowmeter" label="FE-101"/>
  <DvTerminal name="OUT" text="TO PROCESS"/>

  <DvLine from="T1.side" to="P1.in" medium="water"/>
  <DvLine from="P1.out" to="V1.in"/>
  <DvLine from="V1.out" to="F1.in"/>
  <DvLine from="F1.out" to="OUT" arrow="end"/>

  <DvInstrument name="FT1" tag="FT" number="101" on="F1" value="{FlowText}"/>
</DvProcess>
```

The view model side: [view model and updates](#process-vm).

<a id="process-overview"></a>
## At a glance — connections only, no coordinates

```xml
<DvProcess direction="lr" look="plain">
  <DvTerminal name="IN" text="RAW WATER"/>
  <DvVessel name="T1" level="{T1Level}" label="T-101"/>
  <DvPump name="P1" running="{P1Run}" label="P-101"/>
  <DvPump name="P2" running="{P2Run}" label="P-102"/>
  <DvValve name="V1" kind="control" open="{V1Open}" label="FCV-101"/>
  <DvTerminal name="OUT" text="TO DRAIN"/>

  <DvLine from="IN" to="T1.top" arrow="end"/>
  <DvLine from="T1.bottom" to="P1.in"/>
  <DvLine from="T1.bottom" to="P2.in"/>
  <DvLine from="P1.out" to="V1.in"/>
  <DvLine from="P2.out" to="V1.in"/>
  <DvLine from="V1.out" to="OUT"/>
</DvProcess>
```

This markup contains no coordinates. You write the equipment and what connects to what; the library does the rest:

1. It places equipment in columns from left to right in flow order (`IN` → `T1` → `P1`·`P2` → `V1` → `OUT`).
2. It stacks the parallel pumps (`P1`, `P2`) one above the other in the same column.
3. It draws pipes at right angles (orthogonally). Each pipe leaves straight out of the face its port is on and bends only in the empty corridors between equipment.
4. Where `T1.bottom` splits to the two pumps (a branch) and where the two pumps join into `V1.in` (a merge), it draws T-junctions.

Adding or removing a piece of equipment recomputes the placement and the pipes. There are no coordinates for a person to touch, so pipes through equipment and misaligned lines are avoided automatically; when no clean route exists, a Trace warning says so ([process diagnostics](dvml-process-layout.md#process-diagnostics)).

- Everything outside `DvProcess` (title, buttons, tabs) is ordinary dvml.
- Automatic placement is the default. When to place equipment by coordinates instead: [choose the placement mode](#placement-mode).

<a id="process-glossary"></a>
## Glossary

| Term | Meaning |
|---|---|
| equipment | A tag drawn as a P&ID symbol inside `DvProcess`: `DvVessel`, `DvPump`, `DvValve`, `DvValve3`, `DvInline`, `DvExchanger`, `DvTerminal`, or a registered user symbol ([equipment](dvml-process-equipment.md#equipment)) |
| symbol rectangle | The equipment's own drawing box (its kind's size, or `width`·`height` on a vessel). Faces, face positions and `x`·`y` refer to it |
| footprint | The rectangle one piece of equipment occupies in placement: symbol + accessories + label + attached boxes (+ instruments on it) |
| fixed port | A port at one point (`bottom`, `in`, `out`, `a`, `in2` …). Several lines on it split from that point |
| face port | A vessel port spread over a face (`top`, `side`). Lines on it get nozzles chosen by the library ([ports](dvml-process-equipment.md#ports)) |
| face position | `equipment.face@position`, a point on a face you choose ([face position](#face-position)) |
| attached box | A `DvAttach` box beside equipment ([attachments](#attachments)) |
| label | The equipment's one-line name tag (`label`); terminals use `text` |
| column | The rank along the flow: a column in `lr`, a layer (row of equipment) in `tb` |
| bundle | Lines joined by branches and merges; they share a trunk |
| natural size | The diagram size at spacing scale 1, decided once ([size and `fit`](dvml-process-layout.md#size-fit)) |
| spacing scale | The factor on equipment spacing (1 down to 0.5) used when the area is small |
| Trace warning | A run-time message through `System.Diagnostics.Trace` ([process diagnostics](dvml-process-layout.md#process-diagnostics)); a DVML diagnostic is a build-time message ([build diagnostics](dvml-diagnostics.md#build-diagnostics)) |

Booleans, numbers and word values follow the common value rules ([value rules](dvml-language.md#value-rules)).

<a id="when-to-use"></a>
## When to use DvProcess

| Screen | Use |
|---|---|
| A process picture of tanks, pumps and valves connected by pipes | **`DvProcess`** |
| A gauge showing just one tank's level in large size | `DvTank` ([level widget](dvml-instruments.md#dvtank)) |
| An equipment list or status table | `DvRepeat` + `DvStatusItem`, or `DvDataGrid` |
| A panel of numbers and gauges | General widgets such as `DvGauge`·`DvStat` |

- The process vessel is **`DvVessel`**. Its name differs from the level widget `DvTank`. A `DvTank` inside `DvProcess` is not equipment and is not drawn.
- Do not draw a process picture yourself with `position:absolute`, `DvImage` or line-shaped boxes. The pipes will not follow the equipment, and things drift out of place when the resolution changes.

**`DvTank` or `DvVessel`?** Two tags draw a tank because they are used in different places:

| | `DvTank` | `DvVessel` |
|---|---|---|
| What it is | An instrument widget (dashboard gauge) | A P&ID equipment symbol |
| Where it goes | Anywhere in ordinary dvml | Only directly inside `DvProcess` (or a `DvUnit`) |
| Value | `value` between `min`·`max` (default 0–100) + `unit` | `level` 0–100 % (fixed range) |
| Extras | `alarm-zones` (colored bands), value chip, `format` | Pipes connect to its ports; `kind`, `width`·`height` body size |
| Reference | [`DvTank`](dvml-instruments.md#dvtank) | [`DvVessel`](dvml-process-equipment.md#dvvessel) |

**State words and process booleans.** UI widgets show a state with a **word** that picks a theme color (`state="run|stop|fault|warn|manual|off"`, `DvLamp`, `DvStatusItem`); process equipment is moved by **booleans** that drive the drawing and animation (`running`, `open`, `level`). They are two systems on purpose — link them in the view model, not in markup:

```xml
<DvPump name="P1" running="{IsRun}" label="P-101">
  <DvAttach side="top"><DvStatusItem label="P-101" state="{RunState}"/></DvAttach>
</DvPump>
```
```csharp
public bool IsRun { get; set; }
public string RunState => IsFault ? "fault" : IsRun ? "run" : "stop"; // the word for the theme color
```

The attached box goes on `top` because the pump's `out` port is on its right face ([keep attached boxes off port faces](#attachments)).

<a id="placement-mode"></a>
## Choose the placement mode

Decide this first, ahead of writing any equipment. A `DvProcess` uses one mode for the whole diagram.

| The screen is … | Use | Size attributes |
|---|---|---|
| A new screen, flow mostly goes one way (feed → equipment → product), up to about 15 pieces of equipment, at most one or two lines back to earlier equipment | Automatic placement (default, `placement="auto"`) | `grow="1" style="height:0"`; add `fit="scale-down"` only when a render shows `Not enough space` ([sizing procedure](dvml-process-layout.md#sizing-procedure)) |
| Many lines go **back to earlier equipment** (reflux, reboiler loops, recirculation, heat recovery from product to feed), or about 25 pieces of equipment or more | Coordinate placement `placement="fixed"` | `base-size="W H"` + `fit="contain"` + `grow="1" style="height:0"` |
| The user gave a drawing the screen must look like (a customer's P&ID) | Coordinate placement `placement="fixed"` | `base-size` = the size of the drawing image (or the size you took the coordinates at) + `fit="contain"` |

- **Default is automatic.** It is right for almost every screen. Never choose `fixed` for a small new screen.
- Why the second row: automatic placement puts equipment in columns along the flow and cannot reproduce a specific drawing. Every line that goes back to an earlier column has to cross the drawing, so a process with many such lines comes out tangled and its layout time grows quickly (a distillation unit in automatic placement: 17 crossings, 3 pipes with no free path, and 1–2 minutes of layout time). Hints (`row`·`rank`) do not fix that.
- **If the first render in automatic placement shows more than about 5 crossing warnings or any `no free path`, switch to coordinate placement instead of adding hints.**
- In coordinate placement every piece of equipment (and every unit) has `x`·`y`; there is no mixing. Pipes are routed automatically unless you write a route with `via`. Details: [coordinate placement](dvml-process-layout.md#fixed-placement), [`via`](dvml-process-layout.md#via).

<a id="procedure"></a>
## Authoring procedure (in this order)

AI MUST follow the order below exactly. Each step says what to decide.

**⓪ Choose the placement mode** ([placement mode](#placement-mode)). For a given drawing, follow [copying a given drawing](dvml-process-examples.md#copy-drawing) instead of steps ①–⑧.

**① Choose the flow direction.** If the process is long horizontally, use `direction="lr"` (default, left → right); if it is long vertically (blender or reactor units fed at the top and discharged at the bottom), use `direction="tb"`.

**② List the equipment.** For each piece of equipment, pick a tag and a `kind` ([equipment](dvml-process-equipment.md#equipment)). Add one `DvTerminal` for each pipe that comes in from or goes out to outside the screen.

**③ Assign names.** Give every piece of equipment a `name`. Names must be unique within the process diagram and are used to connect lines. Short ASCII letters and digits (`T1`, `P101`, `FCV1`) are safe (do not use a dot `.` — it is the port separator). Put the name shown on screen (`P-101`) in `label`.

**④ Choose ports and connect.** Write one `<DvLine from="upstreamEquipment.port" to="downstreamEquipment.port"/>` for each segment of flow. **`from` is the front of the flow (upstream), `to` is the back (downstream).** This determines the placement order.
- Pumps, valves and inline equipment: `in` on the incoming side, `out` on the outgoing side.
- Vessels: `top` when entering from above, `side` when entering or leaving from the side, `bottom` when leaving from the bottom. A tank that feeds a horizontal line (tank → pump → valve → out) leaves from `side`: the tank stands on the line and the pipe is straight. From `bottom`, the next equipment is placed at another height than the tank, and a taller area stretches that difference ([staircase FAQ](#process-faq)).
- Terminals: the name only, with no port (`from="IN"`, `to="OUT"`).
- For a branch, write several lines from the same `from` port; for a merge, write several lines to the same `to` port.

**⑤ Bind the state.** Pump `running="{…}"`, valve `open="{…}"`, vessel `level="{…}"` (0–100 %). **If omitted, they are drawn stopped, closed and empty.** For a valve that is always open, use a literal such as `open="true"`.

**⑥ Attach value displays and buttons.** Temperature or flow numbers next to equipment and Reset buttons go in ordinary dvml inside `<DvAttach side="…">` on a face without a connected port ([attachments](#attachments)). A reading that belongs to an instrument can be its `value` instead ([`DvInstrument`](#dvinstrument)): it takes no room in the layout, but its size is tied to `font-size`. For an agitator, put `<DvAgitator running="{…}"/>` inside the vessel.

**⑦ Render and check.** Produce a PNG with `DvRenderer.RenderToFile` at the smallest size the screen is really shown at, look at it yourself, and check the Trace warnings ([process diagnostics](dvml-process-layout.md#process-diagnostics), [verify with a PNG](llm-rules.md#verify-png)). For `Not enough space` or `Shrunk to …`, follow the [sizing procedure](dvml-process-layout.md#sizing-procedure). If there are no warnings and the picture looks natural, you are done.

**⑧ Refine with hints only when needed.** If lines cross, use `row`; to change an equipment's column, use `rank`; for equipment orientation, use `rotate`·`flip` ([hints](dvml-process-layout.md#hints)). If the result is clean without hints, do not use hints.

<a id="dvprocess-attributes"></a>
<a id="dvprocess"></a>
## DvProcess attributes

| attribute | values | default | bound | notes |
|---|---|---|---|---|
| `direction` | `lr` · `tb` | `lr` | yes | Flow direction: `lr` = left → right, `tb` = top → bottom |
| `look` | `plain` · `rich` | `plain` | yes | Appearance ([look](#look-flow-medium)); units follow it |
| `flow-animation` | true/false | false | yes | Moving dashes over pipes that have flow ([flow display](#look-flow-medium)); units follow the outer value |
| `align` | `auto` · `step` · `row` | `auto` | yes | Row alignment of the next equipment connected to a port that leaves perpendicular to the flow (the bottom port in `lr`, the right port in `tb`): `step` = staircase (the next equipment below the tank), `row` = the next equipment's inlet at the tank's mid height (the pipe climbs back up in a U), `auto` = staircase, or same row when the staircase cannot shrink into the area ([automatic placement](dvml-process-layout.md#auto-layout)). It shapes the natural-size drawing only: a taller area stretches the height difference either way ([staircase FAQ](#process-faq)). Ignored with a Trace warning in `fixed` |
| `placement` | `auto` · `fixed` | `auto` | yes | `auto` = automatic placement; `fixed` = every piece of equipment sits at its `x`·`y` ([placement mode](#placement-mode)) |
| `base-size` | `"W H"` (px) | — | yes | `fixed` only: the size the coordinates were taken at ([size and `fit`](dvml-process-layout.md#size-fit)). Otherwise ignored with a warning |
| `fit` | `none` · `scale-down` · `contain` | `none` | yes | How the drawing meets the area ([size and `fit`](dvml-process-layout.md#size-fit)). `contain` is for `fixed`; in automatic placement it acts as `scale-down` with a Trace warning |
| `style` | general style | — | yes | `padding`, `background`, `width`, `height`, `border`, `font-size`, etc. `font-size` sets the size of all diagram text ([text size](dvml-process-layout.md#text-size)) |
| `grow` and other layout attributes | — | — | — | Same as for an ordinary box. To fill the area: `grow="1" style="height:0"` in a vertical layout |

- Word values on these pages (`direction`, `look`, `align`, `kind`, `rotate`, `flip`, `arrow`, `medium`, `side` …) ignore case and surrounding spaces; any other word gives the default + a Trace warning ([word values](dvml-language.md#value-word)). Exception: `DvPort side` missing or unknown = `right` + a Trace **error** ([`DvPort`](#dvport)).
- Usually you do not write `align`. For a straight line from a tank, connect from the tank's `side` instead of its `bottom` ([staircase FAQ](#process-faq)).
- **Names and references are written literally, never bound**: equipment, unit, `DvPort` and `DvInstrument` `name`, `DvLine`/`DvSignal` `from`·`to`, and `DvInstrument` `on`·`tap`. The placement is cached by the diagram's structure, so names are fixed text. A `{` in one of them writes a Trace warning and the text is used as written (`name="{N}"` is the name `{N}`); at build time it is [DVML011](dvml-diagnostics.md#dvml011).
- **The only allowed children are** equipment tags (including registered user symbols), units (`DvUnit`, or a component whose template root is `DvUnit`), `DvLine`, `DvInstrument`, `DvSignal` and `DvStyle`. Any other tag (`DvLabel`, `DvButton`, `DvRepeat` …) placed directly under `DvProcess` is not drawn and leaves a Trace error. Put value displays and buttons inside `DvAttach`.
- The background is not painted ([look](#look-flow-medium)).

<a id="connections"></a>
## Connections

Equipment is referred to by `name`. A line connects `equipment.port` to `equipment.port`; terminals (`DvTerminal`) are written with the equipment name only, without a port.

- `from` → `to` is the flow direction. It determines the column order of the placement, so **writing it backwards places the equipment backwards.**
- Do not write a line twice between the same two ports.
- **Branches and merges**: lines touching the same port form one pipe group (a bundle) that shares a trunk and splits at T-junctions. There is no branch-point equipment or tag (no `DvTee`, `DvJunction`).
  - Several lines on a fixed port (`bottom`, `in`, `out`, etc.) → they split from that single point.
  - Several lines on a face port (`top`, `side`) → lines in the same direction merge into one nozzle ([vessel ports](dvml-process-equipment.md#ports)). Several supply lines into one tank: connect them all to `to="T.top"` (or `T.side`).
- Pipes of different groups never overlap when a clean route exists, and when running in parallel they keep a minimum spacing (plain 12px, rich 16px). Where they must cross, a semicircular jump is drawn on one of them ([pipe routing](dvml-process-layout.md#pipe-routing)).
- In automatic placement there are **no** attributes for bend points: change the shape with [hints](dvml-process-layout.md#hints). Bend points exist only in coordinate placement, as [`via`](dvml-process-layout.md#via), where the equipment does not move on its own.
- Names and references are literal ([`DvProcess` attributes](#dvprocess-attributes)).

**Invalid connections**

| Mistake | Drawing | Trace |
|---|---|---|
| Nonexistent equipment name | Not drawn | Error "no equipment named 'X'" |
| Nonexistent port name | Red dashed line connecting the centers of the two pieces of equipment + a red border on that equipment | Error "'X' has no port 'p' (available ports)" |
| No port given on equipment with several ports (`from="P1"`) | Red dashed line + red border | Error "'X' requires a port (…)" |

All other lines and equipment are drawn normally. The available port names appear in the parentheses, so fix it with one of those. At build time the same mistakes are [DVML008](dvml-diagnostics.md#dvml008) and [DVML009](dvml-diagnostics.md#dvml009).

<a id="dvline"></a>
### DvLine

A pipe between two ports, drawn orthogonally.

| attribute | values | default | bound | notes |
|---|---|---|---|---|
| `from` | `equipment.port`, `equipment.face@position`, `equipment`, `unit.port`, or a port name inside a unit | required | no | The upstream end. Just `equipment` for a terminal or a user symbol with only one port. `unit.port` = the name of that unit's `DvPort`; inside a unit, its own ports by name only ([units](#units)). Face positions: [face position](#face-position) |
| `to` | same as `from` | required | no | The downstream end |
| `arrow` | `end` · `start` · `both` · `none` | `none` | yes | `end` = arrow into the `to` port, `start` = into the `from` port. Other values: `none` + Trace warning |
| `flow` | true/false | automatic | yes | The app decides this line's flow; absent or a null binding = automatic ([flow display](#look-flow-medium)) |
| `medium` | `water` · `steam` · `air` · `gas` · `oil` · `acid` | none (gray) | yes | Medium color; propagates in the flow direction ([medium](#look-flow-medium)) |
| `via` | `"x,y x,y …"` | — | yes | Only with `placement="fixed"`: the points the pipe runs through ([`via`](dvml-process-layout.md#via)). Absent = routed automatically |
| `label` | text | — | yes | One line of small text beside the pipe — the line number from the drawing (`01-100-PE-N`). At the middle of the pipe's longest straight segment (below it if horizontal, to the right if vertical), theme text color. Does not affect layout, so give the pipe a segment long enough for the text |
| `visible` | true/false | true | yes | If false, the pipe is not created — like hidden equipment: not drawn, no flow passes through it, and automatic placement is computed without it (equipment may move when it toggles) |

- Lines are always orthogonal: they leave straight out of the port's face and bend only in empty corridors.
- `DvLine` has no `class`·`style`·`onclick` or other common attributes ([DVML019](dvml-diagnostics.md#dvml019)); only `visible` (and `id`).

<a id="face-position"></a>
### Face position (`equipment.face@position`) — a nozzle at a set point

Instead of a port name, a line end (and an instrument `tap`) can name **a point on a face of the equipment**: `T1.left@0.15` = the left face, 15 % of the way down.

```xml
<DvLine from="IN" to="T1.left@0.15"/>       <!-- nozzle N1: upper left -->
<DvLine from="PSV.out" to="T1.right@0.15"/> <!-- nozzle N2: upper right -->
```

- Face: `top` · `bottom` · `left` · `right` — the faces of the symbol rectangle **as seen on screen** (after `rotate`/`flip`).
- The point is on that rectangle, not on the drawn outline. On a round body or a rounded end, away from the middle of the face, the pipe or connection line stops short of the shape (a connection line on an 80px circle: 11px short at `left@0.1`, 5px at `left@0.25`, touching at `left@0.5`). Stay near the middle of the face there or, in your own symbol, draw a nozzle reaching the border ([drawing a symbol](dvml-process-equipment.md#symbol-drawing)).
- Position: 0–1 along the face — `top`/`bottom` from left to right, `left`/`right` from top to bottom. Outside 0–1 is clamped with a Trace warning; a wrong face or a non-number is an error.
- The pipe leaves straight out of that face. Lines naming the same point share one nozzle (a branch there).
- Works on any equipment except terminals and units (units connect only at their `DvPort`s). On a vessel the point counts as a vessel port for flow ([flow display](#look-flow-medium)). On other equipment the pipe ends there: flow and `medium` stop at that point, and the symbol's own nozzles stay drawn. Where flow has to pass through at a drawn point, write a [`DvSymbol`](dvml-process-equipment.md#dvsymbol) with its ports there and a `DvPass`.
- Use it when a drawing fixes where nozzles are (mostly with `placement="fixed"`), or to choose a vessel face yourself (`to="T1.right@0.5"`). Otherwise prefer the ports, which place nozzles automatically.

<a id="attachments"></a>
## Attachments (DvAttach, DvAgitator, DvMotor)

`DvAttach`·`DvAgitator`·`DvMotor` go **inside** an equipment tag. No other children may be written inside equipment (Trace error). The instrument `DvInstrument` and the signal line `DvSignal` go not inside equipment but **directly under `DvProcess` (or `DvUnit`)**; an instrument names its equipment with `on` ([`DvInstrument`](#dvinstrument), [`DvSignal`](#dvsignal)).

<a id="dvattach"></a>
### DvAttach — ordinary dvml beside equipment

| attribute | values | default | bound | notes |
|---|---|---|---|---|
| `side` | `left` · `right` · `top` · `bottom` | `right` | yes | Other values: `right` + Trace warning |
| `visible` | true/false | true | yes | The common visible rule |
| content | any dvml | — | — | Value displays, buttons, lamps, badges. Stacked vertically (change with e.g. `style="display:flex; flex-direction:row"`). The box takes the common attributes like any box |

- Attached 8px from the symbol. `left`/`right` at the symbol's vertical center, `top` above the accessories and label, `bottom` below the label (both centered on the symbol). Several on the same face: `top`/`bottom` stack in order but `left`/`right` overlap, so use only one per face.
- The attached box is included in the equipment's footprint for placement, and **pipes avoid it.** Vessel `side`·`top` ports also position themselves to avoid the span covered by an attached box.
- **Keep it off a face with a connected fixed port**: an exchanger's `in2` (top) or `out2` (bottom), the `in`·`out` of a pump or valve (sides), or a port turned there by `rotate`. The box stands in front of the port, and the pipe gets a `no free path` warning. Use a free face. If none is free (an exchanger with all four ports connected), show the number as an instrument's `value` ([`DvInstrument`](#dvinstrument)) or in a `DvAttach` on an empty symbol beside the equipment ([small symbols](dvml-process-equipment.md#symbol-drawing-marks)).
- Buttons and inputs inside work exactly as on an ordinary screen (`onclick` is a screen VM method).
- **Set text color with classes** (`label`, `caption`, `title`, `muted`, etc. — they follow the theme). Text without a class is dark regardless of the theme and is invisible on a dark background.
- Do not overlay absolutely positioned boxes on the diagram instead: pipes cannot route around them.

```xml
<DvVessel name="BL" level="{BlLevel}" label="BL-1">
  <DvAttach side="right">
    <DvLabel class="caption muted" text="TIA 1001"/>
    <DvLabel class="title" text="{BlTempText}"/>
  </DvAttach>
  <DvAttach side="left">
    <DvButton variant="secondary" size="sm" text="Reset" onclick="ResetBl"/>
  </DvAttach>
</DvVessel>
```

<a id="dvagitator"></a>
<a id="dvmotor"></a>
### DvAgitator · DvMotor — accessories mounted on top

| attribute | values | default | bound | notes |
|---|---|---|---|---|
| `running` | true/false | false | yes | Running state (a boolean, not a UI state word) |
| `kind` | `DvMotor`: `isa`; `DvAgitator`: none | default shape | yes | `DvMotor kind="isa"` = ISA-5.1 motor. On `DvAgitator` any non-empty value draws the default shape + warning |
| `visible` | true/false | true | yes | The common visible rule |

| Tag | Size (px) | Placement |
|---|---|---|
| `DvAgitator` | 40 × 100 | Top center of the equipment. The motor and gearbox (30px) stick out above, and the shaft and blades go into the vessel. In a vessel resized with `width`·`height`, the blades widen or narrow with the vessel and the shaft reaches the same relative depth; the drive on top keeps its size |
| `DvMotor` | 76 × 46 | Mounted whole on the top center of the equipment |

- Mainly used inside `DvVessel` (agitator). When an accessory is present, the vessel's `top` port leaves the center empty and takes positions on both sides.
- Pump drive motors are usually not drawn (they are part of the pump symbol). When the drawing shows one, put `DvMotor` inside `DvPump`: a shaft line joins the motor to the pump.
- `DvMotor kind="isa"` draws an `M` in a circle (36 × 52, stem included). Pair it with `DvPump kind="isa"` to match a P&ID drawing.
- A signal line (`DvSignal to="P1"`) to equipment with an accessory ends on the accessory (the motor), as drawings show it.
- They are not boxes: no `class`, `style` or `onclick`.

<a id="dvinstrument"></a>
## DvInstrument — instrument bubble

Draws field instruments (flow transmitter FT, level transmitter LT …) and controllers (FIC, LIC …) as ISA-5.1-style circles (bubbles): the instrument letters (`tag`) in the upper part, the number (`number`) in the lower part.

| attribute | values | default | bound | notes |
|---|---|---|---|---|
| `name` | text | required | no | Signal lines connect to it. Unique together with equipment names (one each within the diagram) |
| `on` | equipment name | required unless `tap` | no | The equipment to attach to (same diagram, same unit). Missing or wrong: not drawn + Trace error |
| `tap` | `equipment.face@position` or `equipment.port` | — | no | A thin solid connection line from the bubble to a point on equipment: a face position (`T1.left@0.85`) or a fixed port (`CV1.in` = the pipe just outside that port). The bubble then stands at that point instead of on a free face. That equipment is the instrument's equipment (`on` may be left out; if `on` names other equipment, `tap` wins with a warning) |
| `tag` | text | — | yes | Text in the top of the bubble (`FT`·`TT`·`LT`·`PT`·`FIC`·`LIC` …) |
| `number` | text | — | yes | Text in the bottom of the bubble (loop number `101`, `1001` …) |
| `panel` | true/false | false | yes | true = a horizontal line across the middle (central control room instrument); false = field instrument |
| `value` | text | — | yes | Small text beside the bubble, on the side away from the equipment (below) |
| `x` · `y` | px | beside its equipment | yes | Only with `placement="fixed"`: the bubble center ([instrument coordinates](dvml-process-layout.md#instrument-coordinates)) |
| `visible` · `onclick` · `style` | — | — | — | Same as an ordinary box |

```xml
<DvProcess>
  <DvTerminal name="IN" text="FEED"/>
  <DvValve name="V1" kind="control" open="{V1Open}" label="FCV-101"/>
  <DvInline name="F1" kind="flowmeter" label="FE-101"/>
  <DvTerminal name="OUT" text="PRODUCT"/>
  <DvLine from="IN" to="V1.in"/>
  <DvLine from="V1.out" to="F1.in"/>
  <DvLine from="F1.out" to="OUT"/>

  <DvInstrument name="FT1" tag="FT" number="101" on="F1"/>
  <DvInstrument name="FIC1" tag="FIC" number="101" on="V1" panel="true" value="{FlowText}"/>
  <DvSignal from="FT1" to="FIC1"/>
  <DvSignal from="FIC1" to="V1"/>
</DvProcess>
```

- **Always write `on` (or `tap`).** Instruments do not take part in the pipe-flow placement (columns and layers); they go into the footprint of their equipment and are placed on a free face beside it. When the equipment moves, the instrument follows, and **pipes avoid the instrument.**
- The face is chosen automatically: the face of that equipment with the fewest lines, attached boxes and labels; on a tie, top → right → bottom → left. It is placed 20px outside the label and attached boxes on that face. Pumps, valves and inline equipment usually get the **top**; vessels using both top and bottom ports usually get the **side**. There is no attribute to set the face.
- Several instruments on one piece of equipment sit side by side on that face (horizontally on top/bottom, vertically on a side). A single instrument is aligned to the equipment's center line (a controller above a control valve → the signal line drops straight down to the actuator).
- **A controller not attached to equipment** (control room FIC, LIC) is attached with `on` to **the valve (or pump) it drives**, like `FIC1` above.
- **With `x`·`y`** (coordinate placement only): the bubble stands exactly there, however far from its equipment; `on` or `tap` names the equipment it belongs to.
- **With `tap`** (where the point on the equipment matters — a drawing, a level transmitter at the column base):

  | `tap` target | Automatic placement | Coordinate placement with `x`·`y` |
  |---|---|---|
  | Face position (`T1.left@0.85`) | Bubble 24px outside that point, on the line out of the face (`left@…` → to the left, `top@…` → above) | The point slides along the face to line up with the bubble within 16px; further off, the line is diagonal |
  | Fixed port (`CV1.in`) | Taps the pipe 12px out of the port; bubble 24px above it (to the right of a vertical pipe) | The line drops straight onto the nearest part of that pipe; with no part level with the bubble, it goes to the spot 12px out of the port (may be diagonal) |

  Several `tap` instruments on the same equipment are each placed at their own point; instruments without `tap` keep the automatic face. When the connection line leaves the bubble to the right, the `value` text goes below the line. Pipes do not cross the connection line (they go around the bubble rather than between it and its equipment).
- The bubble diameter is 40px (wider if the text is long); text sizes are `tag` 12px, `number` 11px, `value` 11px (all × `font-size` / 14, [text size](dvml-process-layout.md#text-size)). The color follows `look` (same fill as terminals).
- The `value` text is **not part of the layout**: it does not widen the footprint and pipes do not avoid it, so a changing value never moves equipment, bubbles or pipes (signal lines go around it). Where it sits: on a top or bottom face, to the right of a single bubble, and above (top face) or below (bottom face) when several bubbles share the row; on a side face, on the outer side (right face → right, left face → left). Keep it short — a long value can cover neighbors, reported as `'X' value text overlaps 'Y'`. Large or long values are better shown with `DvAttach`.

<a id="dvsignal"></a>
## DvSignal — signal line

Connects instruments, or an instrument and equipment, with a **thin line**: dashed for an electric signal (default), solid with slashes (`//`) for a pneumatic one (ISA-5.1).

| attribute | values | default | bound | notes |
|---|---|---|---|---|
| `from` · `to` | instrument or equipment name | required | no | **Name only, no port** (`V1.in` is an error). Write them in the direction the signal travels (measurement → controller → valve) |
| `kind` | `electric` · `pneumatic` | `electric` | yes | `pneumatic` = solid with `//`: one pair per full 60px of each straight segment (after a 12px margin at each end), at least one on the line; their number and position cannot be set. Only the drawing differs. Other values: `electric` + Trace warning |
| `color` | `#RRGGBB` · `token(name)` | theme text color | yes | Line, slashes and arrowhead. Not a color: default + Trace warning; an unknown token is magenta ([color values](dvml-language.md#value-color), [magenta](dvml-diagnostics.md#magenta)) |
| `thickness` | px > 0 | 1.6 | yes | Dashes grow with it. Named like `DvRing thickness` (`width` is a box or body width). Otherwise default + Trace warning |
| `arrow` | `end` · `start` · `both` · `none` | `none` | yes | Small arrowhead at that end |
| `label` | text | — | yes | One line of small text beside the `from` end (`SH`, `SL`, signal names), on the side away from the line's first bend — or the other side if that spot touches a pipe, a signal line or a bubble |
| `visible` | true/false | true | yes | false = the signal line is not created. No other common attribute (`class`, `style` … = [DVML019](dvml-diagnostics.md#dvml019)) |

```xml
<DvSignal from="FT1" to="FIC1"/>
<DvSignal from="FIC1" to="V1"/>
```

- Signal lines and `tap` connection lines take the **theme text color**, not the `look` palette, so they read on a light and on a dark background in both looks. Use `color`·`thickness` only when a drawing or site standard says so; a fixed `#color` does not follow the theme, so check it on the dark theme too. `tap` connection lines have no `color`·`thickness`. Pipe color is `medium` only.
- It is a different tag from pipes. **Do not draw signals with `DvLine`.** Signal lines are drawn **after all pipes** and have **no effect** on equipment placement, flow judgment or flow animation; adding or removing them leaves equipment positions unchanged.
- Lines are orthogonal and avoid equipment bodies, labels, attached boxes and other instruments. Where they cross pipes they pass straight through **without a jump** (ISA convention). Paths running right alongside a pipe are avoided. There is no `via` on signal lines.
- Where the ends land: for an instrument, the center of the bubble face toward the counterpart, avoiding the face with the value text, faces used by other signal lines, a face with no room in front of it (something within about 12px) and, for a bubble beside its equipment, the face toward that equipment; for a control valve (`DvValve` or `DvValve3` with `kind="control"`), **the top of the actuator**; for other equipment, the center of the body's face toward the counterpart (the body includes accessories, so a pump with a `DvMotor` is reached at the motor). On a user symbol that is the center of a face of its rectangle, so draw an actuator there for the line to land on it.
- Terminals (`DvTerminal`), unit ports (`DvPort`) and instruments that are not drawn (a wrong `on`) cannot be signal ends (Trace error; only that signal line is dropped). Instruments inside a unit connect only within the same unit.
- A corner or branch of a signal line: an empty symbol at that point ([small symbols](dvml-process-equipment.md#symbol-drawing-marks)).

<a id="units"></a>
<a id="dvunit"></a>
## Units (DvUnit · DvPort)

Groups several pieces of equipment **into one unit.** Use it in two cases:

- **The same arrangement repeats** — 3 blenders or 2 reactors with the same tank/agitator/pump arrangement. Define the unit once as a component and use it many times ([repeating a unit](#units-components)).
- **Only part of the process flows in a different direction** — the inside of a unit runs top → bottom (feed → tank → discharge pump) but the units stand side by side.

```xml
<DvProcess direction="tb">
  <DvUnit name="U1" direction="tb" label="FEED UNIT">
    <DvTerminal name="IN" text="FEED"/>
    <DvVessel name="T" level="{FeedLevel}"/>
    <DvPump name="P" running="{FeedRun}"/>
    <DvPort name="out" side="bottom" at="0.7"/>
    <DvLine from="IN" to="T.top" arrow="end"/>
    <DvLine from="T.bottom" to="P.in"/>
    <DvLine from="P.out" to="out"/>
  </DvUnit>
  <DvVessel name="RMT" level="40" label="RMT"/>
  <DvLine from="U1.out" to="RMT.top" arrow="end"/>
</DvProcess>
```

| attribute | values | default | bound | notes |
|---|---|---|---|---|
| `name` | text | required | no | Unique within the outer diagram. Outer lines connect with `name.port` |
| `direction` | `lr` · `tb` | `lr` | yes | Flow direction **inside** the unit, independent of the outer direction |
| `label` | text | — | yes | Unit title outside the rectangle at its top-left, font 13 bold (theme `--text`); pipes avoid it |
| `row` · `rank` | integer | — | yes | Placement hints in the outer diagram ([hints](dvml-process-layout.md#hints)) |
| `x` · `y` | px | — | yes | Only when the outer `DvProcess` has `placement="fixed"`: the center of the whole unit frame |
| `placement` | `auto` | `auto` | — | Read only to warn: the inside of a unit is always placed automatically |
| `visible` · `onclick` | — | — | — | Same as regular equipment |
| `style` · `class` | — | no border, no background | yes | Border and background of the unit rectangle (e.g. `style="border:1 token(border); border-radius:8"`) |

- `look` follows the outer `DvProcess` (it cannot differ per unit); so does `flow-animation`.
- **Children**: equipment (including user symbols), `DvLine`, `DvPort`, `DvInstrument`, `DvSignal`, `DvStyle`. **A unit cannot contain another unit** (not drawn; Trace error).
- **Name scope**: equipment names inside a unit only need to be unique within that unit. Three units may all contain `T` and `P1`.

<a id="dvport"></a>
### DvPort — unit outer port

A connection point the unit exposes to the outside. **Outer lines can connect only to these ports.**

| attribute | values | default | bound | notes |
|---|---|---|---|---|
| `name` | text | required | no | Unique within the unit. Lines inside the unit use **the name only** (`to="out"`); outer lines use `unit.name` (`from="U1.out"`) |
| `side` | `left` · `top` · `right` · `bottom` | required | yes | Side of the unit rectangle. Missing or unknown: `right` + Trace **error** |
| `at` | 0–1 | 0.5 | yes | Position along that side (left/top = 0). Not a number or out of range: 0.5 + Trace error |
| `visible` | true/false | true | yes | The common visible rule |

- Several inner lines to one port (two pump outlets → `out`) merge at the port. The same applies outside.
- A `DvPort` outside a `DvUnit` (directly under `DvProcess`) is ignored and leaves a Trace error. A `DvPort` inside a `DvSymbol` is a symbol port ([`DvSymbol`](dvml-process-equipment.md#dvsymbol)).
- **Do not connect directly from outside to equipment inside a unit.** `from="U1.T.bottom"` is an error (red marking + Trace "equipment inside a unit cannot be connected directly from outside. Add `<DvPort name="…" side="…"/>` to the unit, …"). Put a `DvPort` inside the unit, connect to that port from inside, and connect from outside with `U1.port`.

**How units are placed**

1. **The inside of the unit is placed first**, using the [automatic placement](dvml-process-layout.md#auto-layout) rules as they are (with the unit's `direction`).
2. **Unit rectangle** = the rectangle enclosing all inner footprints + 34px on every side (room for pipes to leave a port straight and then bend).
3. **Ports** sit on the rectangle's `side` at the `at` position, and inner pipes run up to the port. Inner pipes never leave the rectangle.
4. In the **outer diagram**, the unit is a single piece of equipment the size of that rectangle. Outer pipes do not cross it and leave the port straight toward `side`.
5. The unit title (`label`) sits outside the rectangle at its top-left, and pipes avoid it.

**Choosing directions**: to stand several units **side by side**, make the outside `direction="tb"` and put the unit outlet ports at `side="bottom"` (the units stand on the same rank, and the receiving equipment comes centered below them). If the outside is `lr`, units in the same column stack vertically.

<a id="units-components"></a>
### Repeating the same unit — components

Define a unit once as a `DvComponent` and use it many times as instances. A component whose **template root is `DvUnit`** becomes a unit inside a process diagram.

```xml
<DvComponent name="Blender">
  <DvParam name="U"/>
  <DvUnit direction="tb" label="{U.Name}">
    <DvTerminal name="IN" text="{U.FeedTag}"/>
    <DvVessel name="T" level="{U.Level}">
      <DvAgitator running="{U.AgitatorRun}"/>
      <DvAttach side="right">
        <DvLabel class="caption muted" text="{U.TempTag}"/>
        <DvLabel class="title" text="{U.TempText}"/>
      </DvAttach>
      <DvAttach side="left">
        <DvButton variant="secondary" size="sm" text="Reset" onclick="Reset"/>
      </DvAttach>
    </DvVessel>
    <DvPump name="P1" running="{U.Pump1Run}" label="{U.Pump1Tag}"/>
    <DvPump name="P2" running="{U.Pump2Run}" label="{U.Pump2Tag}"/>
    <DvPort name="out" side="bottom" at="0.7"/>
    <DvLine from="IN" to="T.top" arrow="end"/>
    <DvLine from="T.bottom" to="P1.in"/>
    <DvLine from="T.bottom" to="P2.in"/>
    <DvLine from="P1.out" to="out"/>
    <DvLine from="P2.out" to="out"/>
  </DvUnit>
</DvComponent>
```

```xml
<DvPageFrame vm="Plant.BlendPage" title="Blenders">
  <DvProcess direction="tb" look="rich" grow="1" style="height:0">
    <Blender name="BL1" U="{BL1}"/>
    <Blender name="BL2" U="{BL2}"/>
    <Blender name="BL3" U="{BL3}"/>
    <DvVessel name="RMT" level="{RmtLevel}" label="RMT"/>
    <DvTerminal name="OUT" text="RESIN TANK FIRM"/>
    <DvLine from="BL1.out" to="RMT.top" arrow="end"/>
    <DvLine from="BL2.out" to="RMT.top" arrow="end"/>
    <DvLine from="BL3.out" to="RMT.top" arrow="end"/>
    <DvLine from="RMT.bottom" to="OUT"/>
  </DvProcess>
</DvPageFrame>
```

```csharp
using Devinno.UI.App;

namespace Plant;

// The state of one unit: one object of the same class per unit
public sealed class BlenderVm
{
    public required string Name { get; init; }
    public required string FeedTag { get; init; }
    public required string TempTag { get; init; }
    public required string Pump1Tag { get; init; }
    public required string Pump2Tag { get; init; }
    public double Level { get; set; }
    public bool AgitatorRun { get; set; }
    public bool Pump1Run { get; set; }
    public bool Pump2Run { get; set; }
    public double Temp { get; set; }
    public string TempText => $"{Temp:0.0} ℃";
}

public sealed partial class BlendPage : DvPage
{
    public BlenderVm BL1 { get; } = Make(1);
    public BlenderVm BL2 { get; } = Make(2);
    public BlenderVm BL3 { get; } = Make(3);
    public double RmtLevel { get; set; } = 40;

    // Reset button in the template: receives the BlenderVm of the pressed unit
    public void Reset(BlenderVm unit) => unit.Temp = 0;

    private static BlenderVm Make(int i) => new()
    {
        Name = $"BL-{i}", FeedTag = $"MTO RMMT-{i}", TempTag = $"TIA 100{i}",
        Pump1Tag = $"RTP 10{i}1", Pump2Tag = $"RTP 10{i}2",
    };
}
```

```csharp
using Devinno.UI.Components;

var registry = new ComponentRegistry();
registry.Register(blenderMarkup); // the <DvComponent name="Blender"> string above; with DvApp use app.Registry
```

- On the instance (`<Blender …>`), its `label`·`row`·`rank`·`x`·`y` win (resolved against the outer VM); if absent, the template `DvUnit`'s values (resolved against the parameters) are used. `name` is read from the instance only (a `name` on the template `DvUnit` is ignored).
- Inside the template only the parameters are visible; a button in the template receives its own unit through a screen VM method with one parameter, like `Reset(BlenderVm unit)` ([component parameters](dvml-components.md#component-params), [component instances](dvml-components.md#component-instances)).
- When only values change (level, running, temperature), neither the outer nor the inner layout is recomputed.

<a id="look-flow-medium"></a>
## Look, flow and medium

**Look (`look`)**

| | `plain` (default) | `rich` |
|---|---|---|
| Intent | ISA-101 high-performance HMI — gray, color only for state | Good-looking dashboard — metallic texture |
| Body (no state) | Flat gray | Metal gradient + shadow |
| Running / open | Dark gray (inner lines bright) | Green gradient |
| Stopped / closed | White | Gray gradient |
| Liquid | Gray-blue | Blue gradient + gloss |
| Pipe | 3px gray, corner radius 6 | 11px tube (three layers, center highlight), corner radius 10 |
| Pipe spacing | 12px | 16px |
| Suitable background | Light theme / gray | Dark theme |

- Colors are a fixed process-diagram palette, not theme tokens — the one exception to "every color a built-in widget draws is a theme token" ([theme tokens](dvml-style.md#theme-tokens)). Label text, signal lines and `tap` connection lines use theme `--text` instead, and a signal line can set its own `color` ([`DvSignal`](#dvsignal)).
- The background is not painted. It follows the app theme's background or the `background` in `style`. `rich` looks best on a dark background, `plain` on a light or gray background.
- The process diagram does not decide alarm colors (red, etc.). Show alarms with `DvLamp`·`DvBadge` inside `DvAttach`, or with alarm widgets.
- Pipes are not colored by medium **by default** (ISA-101 recommendation). Opt in per line with `medium` (below).

**Flow display (`flow-animation` · `flow`)**

The library decides from connections and equipment state which pipes currently have flow, and when enabled, a dashed line moves along those pipes in the flow direction (`from` → `to`).

```xml
<DvProcess look="rich" flow-animation="{ShowFlow}">
  <DvTerminal name="IN" text="RAW WATER"/>
  <DvVessel name="T1" level="{T1Level}"/>
  <DvPump name="P1" running="{P1Run}"/>
  <DvValve name="V1" open="{V1Open}"/>
  <DvTerminal name="OUT" text="TO DRAIN"/>
  <DvLine from="IN" to="T1.top"/>
  <DvLine from="T1.bottom" to="P1.in"/>
  <DvLine from="P1.out" to="V1.in" flow="{Ft101Flowing}"/>
  <DvLine from="V1.out" to="OUT"/>
</DvProcess>
```

| Attribute | Description |
|---|---|
| `DvProcess flow-animation` | If true, a moving dashed line on flowing pipes. **Off by default** (ISA-101 — motion that draws operator attention is not on by default). Bindable. Units follow the outer value |
| `DvLine flow` | The app decides this line's flow (override). True = flow, false = blocked. Not given, or a bound null = automatic decision |

Automatic decision rules (an inference for display, not a measurement):

1. **Sources**: terminals used as a line's `from` (coming in from outside), vessels with level greater than 0. **Sinks**: terminals used as a line's `to`, vessels. A user symbol with `DvFill` counts as a vessel.
2. **Paths**: lines only in the `from` → `to` direction. Inside equipment, the symbol's flow rules (`DvPass`) — pumps when running, valves when open (`open` true or a non-zero number), check valves one-way `in` → `out`, inline equipment always. Vessels are buffers and do not pass through internally. A user symbol with neither `DvPass` nor `DvFill` stops the flow: the pipes into it do not flow either. Unit ports connect inside and outside.
3. **Flow**: a line flows **only when it is reached from a source and continues from that line to a sink**. A pipe ending at a closed valve, the branch toward a stopped pump, and the downstream side of an empty tank do not flow.
4. **Override**: `flow="false"` is blocked, and its downstream side also stops. `flow="true"` always flows; it also counts as a source for what follows it and a sink for what comes ahead of it: the lines after it flow as far as they reach a sink (even behind a stopped pump, if a flowmeter shows flow), and the lines ahead of it flow if a source reaches them.
5. Re-decided every time values change and the screen is rebuilt (layout unchanged, almost no cost).

**When to use `flow`**: the inference can be wrong because of equipment not on the screen (manual field valves) — it looks like flow because the pump runs and the valve is open, but an off-screen valve is closed. **Bind `flow` on lines that have real data, such as flowmeters and flow switches.** Leave the rest to automatic; never compute flow per line in the VM.

Look and cost of the animation:

- A base stripe (plain = light sky blue, rich = dark teal) is laid on flowing pipes, and a thick dashed line (plain = blue 4px, rich = teal 5px) moves on top. On a pipe with `medium` the stripe and the dashes use darker and lighter tones of that medium's color, so the medium stays visible while it flows. Speed 40px/s.
- While at least one pipe flows, it **redraws at 20 frames per second**. With no flow, or `flow-animation` off, it does not redraw.
- The adapters (OpenTK CPU raster and GPU, WinForms) redraw **only the area containing the process diagram** (`DvSurface.PartialRender`): the cost grows with the area of the flowing diagram, not the whole screen. Measured in a 1600×1080 window, CPU raster: a small diagram covering about 7% of the screen costs about 7% of one core; a page that is mostly process diagram about 50%. `ViewWindow.Gpu` mode on the same small diagram: 3.8% (CPU time).
- With animation off, flow does not appear in the drawing (only the decision result exists).

**Medium color (`medium`)**

Shows what flows inside (water, steam …) through pipe color. **None by default** (ISA-101 — color only when it carries information); diagrams without `medium` are gray.

```xml
<DvLine from="IN" to="P1.in" medium="water"/>
<DvLine from="STM" to="E1.in2" medium="steam"/>
```

| Value | Meaning | plain | rich (tube center stripe) |
|---|---|---|---|
| `water` | Water | Blue | Light blue |
| `steam` | Steam | Dark red | Red |
| `air` | Air | Pale gray | White |
| `gas` | Gas | Dark yellow | Yellow |
| `oil` | Oil | Dark yellow-red | Orange |
| `acid` | Acid / alkali | Gray-violet | Light purple |

Colors follow the KS A 0503 pipe identification color family, chosen to read in both looks. **You cannot give color values directly** (only the 6 names; no `style` or color on pipes). Any other value → default gray + Trace warning. Bindable (`medium="{M}"`).

**Write it once on the first line of a run and it spreads in the flow direction:**

- From the line where it is written, it continues `from` → `to`, and inside equipment along that equipment's paths (pump `in`→`out`, valves, inline, 3-way valve branches, heat exchanger `in`→`out` and `in2`→`out2` separately) to the next line. **State does not matter** — behind a stopped pump or closed valve it is the same pipe, so the same color.
- It also reaches the other lines leaving the same port (two pumps fed from one point). A branch that carries something else gets its own `medium`.
- It stops at **vessels** (mixing inside), **terminals**, and **lines with their own `medium`**. After a tank, or where the medium changes (steam → condensate), write it again on that first line.
- Unit ports connect inside and outside.
- When different media meet on one line, it is colored with the medium written first and a Trace warning is left. Fix it by writing `medium` on the line after the merge.
- No legend is drawn automatically. Put one on the screen with `DvAttach` or regular text.
- Only the color changes; layout and flow decision are unaffected.

<a id="process-vm"></a>
## View model (C#) and updates

Process diagrams use the same VM binding as regular screens. Equipment state is VM properties (`running="{P1Run}"`, `open="{V1Open}"`, `level="{T1Level}"` 0–100 %); actions on pressing equipment are VM methods.

```csharp
using Devinno.UI.App;

namespace Plant;

public sealed partial class WaterPage : DvPage
{
    public double T1Level { get; set; } = 60;
    public bool P1Run { get; set; } = true;
    public bool P2Run { get; set; }
    public bool V1Open { get; set; } = true;
    public string FlowText { get; set; } = "0.0 m³/h";

    // Pressing the pump asks for confirmation to stop it
    public async void P1Click()
    {
        if (await App.MessageBox("Stop P-101?", "Pump", DvMessageButtons.YesNo) == DvDialogResult.Yes)
            P1Run = false; // after await: input thread, redrawn automatically
    }

    // Values from the communication thread: change them, then App.Invalidate()
    public void OnPlc(double level, bool p1, bool p2, double flow)
    {
        T1Level = level;
        P1Run = p1;
        P2Run = p2;
        FlowText = $"{flow:0.0} m³/h";
        App.Invalidate();
    }
}
```

```xml
<DvPageFrame vm="Plant.WaterPage" title="Water treatment">
  <DvProcess look="plain" grow="1" style="height:0">
    <DvTerminal name="IN" text="RAW WATER"/>
    <DvVessel name="T1" level="{T1Level}" label="T-101"/>
    <DvPump name="P1" running="{P1Run}" label="P-101" onclick="P1Click"/>
    <DvPump name="P2" running="{P2Run}" label="P-102"/>
    <DvValve name="V1" kind="control" open="{V1Open}" label="FCV-101"/>
    <DvInline name="F1" kind="flowmeter" label="FT-101">
      <DvAttach side="bottom"><DvLabel class="label" text="{FlowText}"/></DvAttach>
    </DvInline>
    <DvTerminal name="OUT" text="TO DRAIN"/>
    <DvLine from="IN" to="T1.top" arrow="end"/>
    <DvLine from="T1.bottom" to="P1.in"/>
    <DvLine from="T1.bottom" to="P2.in"/>
    <DvLine from="P1.out" to="V1.in"/>
    <DvLine from="P2.out" to="V1.in"/>
    <DvLine from="V1.out" to="F1.in"/>
    <DvLine from="F1.out" to="OUT"/>
  </DvProcess>
</DvPageFrame>
```

**Updates and cost**

- When only values (`level`, `running`, `open`, attached-box text, instrument `value`) change, **layout is not recomputed.** If equipment, line structure, labels and attached-box sizes are the same, the layout computed earlier is reused, inside units too. On a 1600×1000 screen, one value update costs 1–2ms for layout and about 16ms for drawing (CPU).
- If attached-box text length changes and the box size changes, layout is recomputed (tens of ms). Recent layouts are kept, so going back to a width seen earlier reuses its layout. Giving frequently changing numbers a fixed width (`style="width:…"` on the attached box) keeps the layout from shifting. An instrument `value` never moves anything; only the signal lines around it are routed again when its width changes.
- Thread rules are the same as on regular screens ([threads](dvml-app.md#threads)).
- Several units of the same arrangement (3 blenders): use a unit component, with one VM object of the same class per unit passed as a parameter ([repeating a unit](#units-components)).

## Complete examples

Complete diagrams to start from (water treatment, blender, recirculation, a distillation unit in coordinate placement): [process examples](dvml-process-examples.md#process-examples).

<a id="process-checklist"></a>
## Checklist (MUST · NEVER)

**MUST — all modes**

- Write process screens only as equipment + `<DvLine from="equipment.port" to="equipment.port"/>` inside `<DvProcess>`.
- Every piece of equipment has a unique `name`. On-screen text is `label` (`text` for terminals).
- `from` = upstream, `to` = downstream.
- Use only the defined port names: vessel `top`·`bottom`·`side`; pump/valve/inline `in`·`out` (inline `vent`·`blind`: `in` only); 3-way valve `a`·`b`·`c`; heat exchanger `in`·`out`·`in2`·`out2`; terminals by name only; user symbols by the names they define ([ports](dvml-process-equipment.md#ports)).
- Give state by binding or literal (`open="true"`). If not given, it is stopped / closed / empty vessel.
- Value displays and buttons go inside `<DvAttach>`; text uses classes (`label`·`caption`·`title`).
- To fill the area, `grow="1" style="height:0"` (inside a vertical layout).
- For screen size, follow the [sizing procedure](dvml-process-layout.md#sizing-procedure) step by step, rendering at the smallest real screen size.
- After fixing, render to PNG and check Trace errors and warnings.
- Place instruments (`DvInstrument`) directly under `DvProcess` and always state their equipment with `on` (or `tap`, where the point on the equipment matters). Attach controllers to the valve they move. Signal lines use `DvSignal`, ends by name only.
- Several identical units: define a unit component (template root `DvUnit`) once and repeat it as instances. Unit inside → outside connections go through a `DvPort` and `unit.port`.
- For a button inside a unit template to receive its own unit, write the screen VM method with one parameter, like `Reset(BlenderVm unit)`.
- Choose the mode with [the placement-mode rule](#placement-mode); automatic placement unless that rule says `fixed`.

**MUST — coordinate placement (`placement="fixed"`)**

- `x`·`y` on **every** piece of equipment and unit ([coordinate placement](dvml-process-layout.md#fixed-placement)).
- `base-size` (the size the coordinates were taken at) + `fit="contain"`.

**NEVER — all modes**

- Do not put `DvTank` (level widget), `DvLabel`·`DvButton` directly in a process diagram (→ `DvVessel`, `DvAttach`).
- Do not draw a process screen with `position:absolute`, `DvImage` or line boxes, and do not overlay absolutely positioned boxes on the diagram to draw values or lines (pipes cannot avoid them).
- Do not invent junction equipment for pipes (`DvTee`, `DvJunction`, etc.). Connect several lines to the same port.
- Do not invent kinds; register a [`DvSymbol`](dvml-process-equipment.md#dvsymbol) for equipment that is not built in.
- Do not use dots (`.`) in names.
- Do not hide connected equipment with `visible`.
- Do not try to put colors in user symbol paths (there is no attribute). Colors come from `class`.
- Do not connect directly from outside to equipment inside a unit, as in `U1.T.top` (use `DvPort`).
- Do not put a unit inside a unit. Do not bind screen VM properties directly inside a unit component template (only parameters are visible).
- Do not compute flow per line as VM properties — leave it to the automatic decision, and bind `flow` only on lines with real measurements.
- Do not draw signal lines as `DvLine` (pipes): they interfere with flow and layout. Use `DvSignal`.
- Do not invent attributes that set an instrument's face (`side`).
- Do not try to set pipe color via `style` or color values. Medium colors exist only as the 6 names of `medium`.
- Do not put `font-size` on a parent box for the diagram, and do not enlarge attached boxes one by one — `DvProcess`'s own `font-size` scales every diagram text, attached boxes included ([text size](dvml-process-layout.md#text-size)).
- Do not judge a shrunk diagram from the picture — use the `Xpx` in its warning.
- Do not invent the features listed in [not supported](#process-not-supported) as attributes.

**NEVER — automatic placement**

- Do not invent bend-point attributes; `via` works only with `placement="fixed"`. Do not put `x`·`y` on equipment or instruments (ignored with a warning).
- Do not sprinkle `row`·`rank` from the start. Render first, then use them only where needed.
- Do not add `fit="scale-down"` until a render has shown `Not enough space`.

**NEVER — coordinate placement**

- Do not mix: every piece of equipment needs `x`·`y`.
- Do not use `base-size` without `fit="contain"` to show a drawing.

<a id="process-not-supported"></a>
## Not supported

Any tag, attribute or value not on the process pages does not exist. The items below are the ones most often asked for. Do not write them; an unknown attribute is build warning [DVML019](dvml-diagnostics.md#dvml019) and is ignored at run time.

- There is no `exit` hint (an attribute setting the direction a pipe leaves); an `exit` attribute is DVML019 and ignored.
- Units inside units.
- Bend points in automatic placement (use [hints](dvml-process-layout.md#hints); `via` exists only in coordinate placement). No `via` on signal lines.
- Zooming in or enlarging an automatically placed diagram: `fit="scale-down"` only shrinks an overflowing one; a coordinate-placed drawing scales both ways with `fit="contain"` ([size and `fit`](dvml-process-layout.md#size-fit)).
- Pipe click events; dragging equipment.
- Mirroring or rotating terminals ([terminals](dvml-process-equipment.md#terminal)).
- Not available directly (use the [small symbols](dvml-process-equipment.md#symbol-drawing-marks)): the route of a signal line, a branch of one, starting one on a pipe, and leader lines.
- Not available at all: a multi-line `label`; the size of a single bubble or of its `value` text (`font-size` on `DvProcess` sets them all and may be below 14, [text size](dvml-process-layout.md#text-size)); the number of `//`; colors on symbols (classes only), colors on pipes (`medium` only, [medium](#look-flow-medium)); text inside a symbol.
- Automatic placement is not meant for processes with many lines back to earlier equipment ([placement mode](#placement-mode)); its layout time grows quickly with such loops.
- A large drawing is laid out once when the screen is first built (the distillation example on a desktop CPU: about 1.3 s the first time, 0.4 s for another screen with the same markup). The result is kept: resizing the window with `fit="contain"` takes 1–2 ms and a rebuild after a value change 12 ms.

<a id="process-faq"></a>
## FAQ

**I want a pipe to be a straight line.**
It is straight when the connected ports are on the same row. Series equipment aligns automatically. When branches, merges and side ports mix, bends appear; this is normal.

**I want to swap the order of two pieces of equipment.**
In the same column, use `row`. To move one to a different column, first check the flow (`from`/`to`) is right, and if needed, use `rank` ([hints](dvml-process-layout.md#hints)).

**I want a valve to stand vertically.**
`rotate="90"` — the inlet goes on top and the outlet on the bottom.

**I want to choose which side of the tank a line enters.**
`side` automatically picks the face toward the other equipment. To choose the face yourself, connect to a face position: `to="T1.right@0.5"` ([face position](#face-position)).

**Pipes go down like stairs, or a long vertical pipe runs from the tank to the pump.**
A line leaving a port perpendicular to the flow (a vessel's `bottom` in `lr`) places the next equipment at another height than the tank. `align` only chooses which: `step` (and `auto`) puts the next equipment below the tank, a staircase; `row` puts the next equipment's inlet at the tank's mid height, so the bottom outlet is lower and the pipe climbs back up in a U. The drawing is laid out at its natural size and then stretched to the area on each axis ([size and `fit`](dvml-process-layout.md#size-fit)), so in a tall area (`grow="1"`) that height difference grows with it: with `step` the pump ends up at the bottom of the area under a long drop; with `row` the tank ends up at the bottom under a long riser. Changing `align` does not fix it.
- A tank feeding a horizontal line: connect from `side` (`from="T1.side" to="P1.in"`). Tank and line are on one row and the pipe stays straight at any area height.
- The pipe must leave from the bottom: do not stretch the diagram. Leave out `grow="1" style="height:0"` so it keeps its natural height, where the step is small.

**Several lines enter the tank top separately and it looks cluttered.**
Lines on the same face and in the same direction are merged into one nozzle. What appears separate is an incoming and an outgoing line, or different left/right faces ([vessel ports](dvml-process-equipment.md#ports)).

**The screen is small and it overflows.**
Follow the [sizing procedure](dvml-process-layout.md#sizing-procedure): `fit="scale-down"`, then `font-size` if the reported text size is below 10px, and split the screen if that is not enough. (Ahead of `fit`, the library tries moving equipment closer and other alignments by itself.)

**I have several identical units of plant (3 blenders).**
Define a unit component once and use it like `<Blender name="BL1" U="{BL1}"/>` ([repeating a unit](#units-components)). To stand them side by side, outer `direction="tb"` + unit outlet port `side="bottom"`.

**I want to connect an outer pipe directly to a tank inside a unit.**
Not possible. Put `<DvPort name="in2" side="left"/>` on the unit, connect `<DvLine from="in2" to="T.side"/>` inside, and connect with `to="BL1.in2"` from outside.

**The screen must look exactly like the customer's drawing.**
Use coordinate placement ([placement mode](#placement-mode), [copying a given drawing](dvml-process-examples.md#copy-drawing)): `placement="fixed"`, and the symbol center of each piece of equipment from the drawing as `x`·`y`. Pipes are routed for you. Give instrument bubbles `x`·`y` too, and write [`via`](dvml-process-layout.md#via) on a pipe that runs differently from the drawing.

**Can I give process equipment an `id` to get it from code?**
Yes: `id` gives a `DvControl` handle (`Visible`, `Enabled`, `Bounds`, `Click`) like any box; lines connect by `name`, never by `id`. Prefer VM binding for state and `onclick` for actions ([equipment `id`](dvml-process-equipment.md#equipment-id)).
