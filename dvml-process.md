# Devinno.UI Process Diagram (P&ID) Authoring Guide

This document explains, from start to finish, how to build a **process screen** (a P&ID-style screen) of tanks, pumps, valves and pipes with `<DvProcess>`. People can read it too, but it was written so that **an AI can write a correct process screen from this document alone**. That is why every rule is listed and every number is exact.

- Read [`llm-rules.md`](llm-rules.md) (principles and pitfalls) first. Tags, styles and bindings outside the process diagram are in [`dvml-ui-markup.md`](dvml-ui-markup.md). Chapter 22 of that document is a summary of this one.
- Any tag, attribute or value not in this document **does not exist**. Do not invent them.

## Contents

1. At a glance — connections only, no coordinates
2. When to use `DvProcess`
3. Authoring procedure (in this order)
4. `DvProcess` attributes and size
5. Equipment reference (tag, kind, ports, size, state)
6. Connection (`DvLine`) rules
7. Attaching to equipment (`DvAttach`, `DvAgitator`, `DvMotor`, instrument `DvInstrument`, signal line `DvSignal`)
8. Unit groups (`DvUnit` · `DvPort`)
9. How placement is decided
10. How pipe routes are decided
11. Refining with hints (`row`·`rank`·`rotate`·`flip`)
12. Appearance (`look`)
13. View model (C#) and updates
14. User symbols (`DvSymbol`)
15. Diagnostics and how to fix them
16. Full example
17. Checklist (MUST · NEVER)
18. Not yet available
19. Frequently asked questions
20. Coordinate placement (`placement="fixed"`) — matching a given drawing

---

## 1. At a glance — connections only, no coordinates

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

This markup contains no coordinates at all. The library does the following.

1. It places equipment in columns from left to right in flow order (`IN` → `T1` → `P1`·`P2` → `V1` → `OUT`).
2. It stacks the parallel pumps (`P1`, `P2`) one above the other in the same column.
3. It draws pipes at right angles (orthogonally). Each pipe leaves straight out of the face its port is on and bends only in the empty corridors between equipment.
4. Where `T1.bottom` splits to the two pumps (a branch) and where the two pumps join into `V1.in` (a merge), it draws T-junctions.

Adding or removing a piece of equipment recomputes the placement and the pipes. Since there are no coordinates for a person to touch, mistakes like "a pipe runs through equipment" or "lines are misaligned" cannot happen.

This automatic placement is the default and the right choice for almost every screen. Only when the screen **must match a drawing you were given** (a customer's P&ID) can equipment be placed by coordinates instead — chapter 20. Even then, pipes are still routed automatically.

---

## 2. When to use `DvProcess`

| Screen | Use |
|---|---|
| A process picture of tanks, pumps and valves connected by pipes | **`DvProcess`** |
| A gauge showing just one tank's level in large size | `DvTank` (level widget, `dvml-ui-markup.md` chapter 9) |
| An equipment list or status table | `DvRepeat` + `DvStatusItem`, or `DvDataGrid` |
| A panel of numbers and gauges | General widgets such as `DvGauge`·`DvStat` |

- The process vessel is **`DvVessel`**. Its name differs from the level widget `DvTank`. If you put a `DvTank` inside `DvProcess`, it is not equipment and is not drawn.
- Do not draw a process picture yourself with `position:absolute`, `DvImage` or line-shaped boxes. The pipes will not follow the equipment, and things drift out of place when the resolution changes.

---

## 3. Authoring procedure (in this order)

AI MUST follow the order below exactly. Each step says what to decide.

**① Choose the flow direction.** If the process is long horizontally, use `direction="lr"` (default, left → right); if it is long vertically (blender or reactor units fed at the top and discharged at the bottom), use `direction="tb"`.

**② List the equipment.** For each piece of equipment in the process, pick a tag and a `kind` (table in chapter 5). Add one `DvTerminal` for each pipe that comes in from or goes out to outside the screen.

**③ Assign names.** Give every piece of equipment a `name`. Names must be unique within the process diagram and are used to connect lines. Short ASCII letters and digits (`T1`, `P101`, `FCV1`) are safe (do not use a dot `.` — it is the port separator). Put the name shown on screen (`P-101`) separately in `label`.

**④ Choose ports and connect.** Write one `<DvLine from="앞장비.포트" to="뒷장비.포트"/>` (from="upstreamEquipment.port" to="downstreamEquipment.port") for each segment of flow. **`from` is the front of the flow (upstream), `to` is the back (downstream).** This determines the placement order.
- Pumps, valves and inline equipment: `in` on the incoming side, `out` on the outgoing side.
- Vessels: `top` when entering from above, `bottom` when leaving from the bottom, `side` when entering or leaving from the side.
- Terminals: the name only, with no port (`from="IN"`, `to="OUT"`).
- For a branch, write several lines from the same `from` port; for a merge, write several lines to the same `to` port.

**⑤ Bind the state.** Pump `running="{…}"`, valve `open="{…}"`, vessel `level="{…}"` (0–100 %). **If omitted, they are drawn stopped, closed and empty.** For a valve that is always open, use a literal such as `open="true"`.

**⑥ Attach value displays and buttons.** Temperature or flow numbers next to equipment and Reset buttons go in ordinary dvml inside `<DvAttach side="right">` (chapter 7). For an agitator, put `<DvAgitator running="{…}"/>` inside the vessel.

**⑦ Render and check.** Produce a PNG with `DvRenderer.RenderToFile`, look at it yourself, and check the Trace warnings (chapter 15). If there are no warnings and the picture looks natural, you are done.

**⑧ Refine with hints only when needed.** If lines cross, use `row`; to change an equipment's column, use `rank`; for equipment orientation, use `rotate`·`flip` (chapter 11). If the result is clean without hints, do not use hints.

---

## 4. `DvProcess` attributes and size

| Attribute | Values | Default | Description |
|---|---|---|---|
| `direction` | `lr` · `tb` | `lr` | Flow direction. An unknown value gives `lr` + a Trace warning |
| `look` | `plain` · `rich` | `plain` | Appearance (chapter 12). An unknown value gives `plain` + a Trace warning |
| `flow-animation` | true/false | false | Moving dashes over pipes that have flow (12.1). Bindable |
| `align` | `auto` · `step` · `row` | `auto` | Row alignment of the next equipment connected to a port that leaves perpendicular to the flow (the bottom port in lr, the right port in tb) (chapter 9, item 5). `step` = staircase, `row` = same row, `auto` = chosen to fit the area. An unknown value gives `auto` + a Trace warning |
| `placement` | `auto` · `fixed` | `auto` | `auto` = automatic placement (this whole document). `fixed` = every piece of equipment sits at its `x`·`y` (chapter 20). An unknown value gives `auto` + a Trace warning |
| `base-size` | `"W H"` (px) | — | `fixed` only: the size the coordinates were taken at; positions are scaled to the area (chapter 20.3) |
| `style` | general style | — | `padding`, `background`, `width`, `height`, `border`, etc. |
| `grow` and other layout attributes | — | — | Same as for an ordinary box |

`direction`·`look` can be bound (`look="{Look}"`).

**The only allowed children are** equipment tags (chapter 5, including registered user symbols), units (`DvUnit`, or a component whose template root is `DvUnit` — chapter 8), `DvLine`, instrument `DvInstrument` and signal line `DvSignal` (7.3·7.4), and `DvStyle`. Any other tag (`DvLabel`, `DvButton`, `DvRepeat` …) placed directly under `DvProcess` is not drawn and leaves a Trace error. Put value displays and buttons inside `DvAttach`.

**Size**

- If no size is given, the process diagram's size = the content size (the placement result + a 12px outer margin).
- If a size is given or the parent determines the size, the content is placed in the **center** of that area.
- If the area is smaller than the content, placement is retried in the following order and the **first one that fits** is used. With `align="auto"` (default): staircase with spacing scale 1 → 0.85 → 0.7 → 0.6 → 0.5, then same-row with 1 → … → 0.5. With `align="step"`·`"row"`, spacing is reduced only in that mode. If it still overflows, the placement that overflows least is used and a Trace warning ("Not enough space") is left. No zooming (scaling) is done. Equipment sizes themselves do not shrink, so only spacing and row alignment can be reduced.
- **To make it fill the remaining space**, use `grow="1" style="height:0"` inside a vertical layout (same meaning as CSS `flex-basis:0` — grows from a base height of 0), and `grow="1"` inside a horizontal layout. With only `grow="1"` and no `height:0`, the process diagram does not shrink below its content height and may overflow the screen.

```xml
<DvPageFrame title="정수 공정">
  <DvCard title="계통도" grow="1">
    <DvProcess look="rich" grow="1" style="height:0">
      <DvVessel name="T1" level="40" label="T-101"/>
      <DvPump name="P1" running="true" label="P-101"/>
      <DvLine from="T1.bottom" to="P1.in"/>
    </DvProcess>
  </DvCard>
</DvPageFrame>
```

The **background** is not painted. It follows the app theme's background or the `background` in `style`. `rich` looks best on a dark background, `plain` on a light or gray background.

---

## 5. Equipment reference (tag, kind, ports, size, state)

### 5.1 Common attributes

Usable on every equipment tag (including user symbols).

| Attribute | Description |
|---|---|
| `name` | **Required.** Must be unique within the process diagram. Without it the equipment cannot be connected by lines (Trace error); if duplicated, the later one cannot be connected (Trace error) |
| `label` | Name tag. For vessels, a rounded chip in the middle of the body. For everything else, placed on a **face not used by lines** — the first free face in the order bottom → top → right → left (if all four faces are used, bottom — and if it would cover the front of a port on that face, it shifts along the face to stay at least 12px from the port). 4px from the symbol (6px left/right). Font 13 bold, color is the theme `--text`. Not used on terminals (use `text`) |
| `kind` | Type (tables below). If omitted, the first kind; an unknown value gives the first kind + a Trace warning |
| `rotate` | `0` `90` `180` `270` (clockwise). The drawing and the port faces rotate together. Other values give 0 + a Trace warning |
| `flip` | `h` (flip left-right) · `v` (up-down) · `hv`. Flip is applied, then rotation |
| `row` · `rank` | Placement hints (chapter 11) |
| `x` · `y` | Only with `placement="fixed"`: the symbol center in px (chapter 20). Ignored with a Trace warning in automatic placement |
| `visible` | If false, the equipment is not created. **Lines connected to that equipment become "equipment does not exist" errors, so they cannot be hidden along with it** — do not hide equipment that has lines; express it with state instead |
| `onclick` · `hold` · `disabled` | Same as an ordinary button. Pressing the equipment body (the symbol and accessory area) triggers `onclick` |
| `class` · `style` · `hover-style` · `press-style` | Applied to the equipment body rectangle (giving a background paints a rectangle behind the symbol — not normally used) |

### 5.2 Vessel `DvVessel`

| kind | Size (px) | Shape |
|---|---|---|
| `tank` (default) | 100 × 150 | Vertical cylinder rounded at top and bottom |
| `reactor` | 100 × 160 | Reactor with a jacket (an added outer wall) |
| `silo` | 100 × 160 | Conical roof + bottom funnel + legs |
| `hopper` | 100 × 110 | Funnel open at the top |
| `drum` | 160 × 80 | Horizontal drum (receiver, knock-out drum) with saddles |
| `column` | 60 × 220 | Tall tower with dashed trays |
| `open` | 120 × 90 | Open-top tank or basin (no roof line) |

| Port | Position | When several lines connect |
|---|---|---|
| `top` | Top face (face port) | **Incoming lines merge into one nozzle and outgoing lines merge into one nozzle** (they gather into a T or cross in front of the tank). Nozzles lie in the 10–90 % horizontal span of the top face, avoiding the spot of an agitator or a cell attached on top. If there are both an incoming and an outgoing nozzle, they sit side by side ordered by the direction of their counterparts |
| `bottom` | One point at the bottom center | Splits from that one point (branch) |
| `side` | Left or right face (face port) | For each line, picks **the face toward the counterpart equipment** (left if the counterpart is to the left). If that face is mostly covered by an attached cell, the opposite face. Lines on the same face in the same direction (in/out) merge into one nozzle |

| State attribute | Values | If omitted |
|---|---|---|
| `level` | 0–100 (%). Out-of-range values are clamped to 0 or 100. Number or numeric string | 0 (empty vessel) |

| Size attribute | Values | If omitted |
|---|---|---|
| `height` | Height in px (to match a drawing's tall tank). Only the straight body grows or shrinks — the heads keep their shape and the width stays the same. 0.6–4 × the kind's height (outside that, clamped with a Trace warning) | The kind's height |

- The level fills from the bottom following the body shape (rounded bottoms are filled round).
- `label` is drawn as a rounded chip in the middle of the body (readable over the liquid too).
- `DvAgitator`·`DvMotor`·`DvAttach` can be placed as children (chapter 7).

### 5.3 Rotating equipment `DvPump`

| kind | Size (px) | `in` | `out` | Shape |
|---|---|---|---|---|
| `centrifugal` (default) | 64 × 64 | Left, 36px from top | Right, 20px from top | Round casing + tangential discharge at top + base |
| `gear` | 72 × 52 | Left center | Right center | Rounded rectangle + two gears |
| `compressor` | 72 × 64 | Left center | Right center | Circle + two converging lines |
| `blower` | 72 × 64 | Left, 36px from top | Right, 19px from top | Round casing + blades |
| `isa` | 64 × 64 | Left center | Right center | ISA-5.1 pump: circle + centerline + `>` toward the outlet (as on P&ID drawings) |

| State attribute | Values | If omitted |
|---|---|---|
| `running` | true/false, or a number (greater than 0 = running) | Stopped |

- **For a pump whose flow is right → left**, use `flip="h"` to put `in` on the right.
- In a top → bottom (`tb`) process, pumps are still drawn horizontal by default (inlet left, outlet right). Parallel pumps stack vertically (chapter 9).

### 5.4 Valve `DvValve`

Straight kinds are 60 × 48px, with `in` on the left and `out` on the right, both 32px from the top (the pipe centerline). `angle` and `relief` are angle bodies: `in` at the bottom, `out` on the right (`flip="h"` puts `out` on the left).

| kind | Shape |
|---|---|
| `gate` (default) | Bowtie + handle on top |
| `ball` | Bowtie + ball in the middle + lever |
| `butterfly` | Body + tilted disc + actuator on top |
| `control` | Bowtie + dome-shaped actuator on top (control valve) |
| `check` | Body + triangle (non-return, no state) |
| `globe` | Bowtie + filled dot + handwheel (throttling) |
| `plug` | Bowtie + hollow plug + lever |
| `diaphragm` | Bowtie + dome (diaphragm) + handwheel |
| `angle` | 48 × 48. Two triangles meeting at a right angle + handwheel. `in` bottom, `out` right (20px from top) |
| `relief` | 48 × 64. Angle body + spring (pressure safety / relief valve, PSV). `in` bottom, `out` right (36px from top). `open` = relieving |

| State attribute | Values | If omitted |
|---|---|---|
| `open` | true/false, or an opening number (greater than 0 = open) | **Closed** |

- Give `open="true"` even to an always-open valve. Without it, the valve is drawn closed.
- A valve on a vertical pipe uses `rotate="90"` (enters from the top, leaves at the bottom).

### 5.5 Inline `DvInline` (no state)

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

Put flow numbers in `<DvAttach side="bottom">` or `side="top"` (chapter 7).

### 5.6 Terminal `DvTerminal`

A tag (off-page connector) for a pipe that continues outside the screen.

| Attribute | Description |
|---|---|
| `text` | Tag text (font 12 bold). The width fits the longest line. For two or more lines, put `&#10;` at each line break (`text="SOLVENT&#10;FROM UNIT 1"`); the tag grows one line height taller per extra line |
| Port | Only one, and it has no name. In a line, write just the name, like `from="IN"`, `to="OUT"` (adding a dot gives a Trace warning and is ignored) |

- **Used in a line's `from`, it means "coming in from outside"**; used only in `to`, it means "going out".
- Shape: in `lr`, an arrow tag pointing right (height 28, width = text + 34, minimum 64). For incoming, the pipe connects at the pointed end (right); for outgoing, at the flat left side. In `tb`, a tag pointing down (height 38, width = text + 20, minimum 60). Incoming connects at the bottom tip, outgoing at the top.
- Use each terminal in one direction only. Incoming and outgoing pipes get separate terminals.

### 5.7 Three-way valve `DvValve3`

A valve that splits one pipe into two (branch) or gathers two into one (mixing). Both kinds are 60 × 64px.

| kind | Shape |
|---|---|
| `ball` (default) | Three-way bowtie + ball in the middle + lever |
| `control` | Three-way bowtie + dome-shaped actuator on top (control valve — the signal line reaches the top of the actuator, 7.4) |

| Port | Position | Meaning |
|---|---|---|
| `a` | Left, 32px from top | Common port |
| `b` | Right, 32px from top | Straight branch |
| `c` | Bottom center | Turning branch |

The heights of `a`·`b` match `in`·`out` of `DvValve` (in line with ordinary valves).

| State attribute | Values | If omitted |
|---|---|---|
| `open-b` | true/false, or a number (greater than 0 = open) — `a`↔`b` passes | Closed |
| `open-c` | Same format — `a`↔`c` passes | Closed |

- If both are true, both paths are open (mixing or modulating); if both are false, it is blocked. Only the triangles of open branches are painted in the state color (the common `a` side is always the same color).
- Paths pass in both directions. So the same tag is used for a **branch** (`from="P1.out" to="V3.a"`, `from="V3.b" …`, `from="V3.c" …`) and for **mixing** (`to="V3.b"`, `to="V3.c"`, `from="V3.a" …`).
- **When using mixing in a left → right process, use `flip="h"`** to put the common `a` on the right (the side where flow leaves). Otherwise the incoming pipes wrap around the valve to get in.
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

### 5.8 Heat exchanger `DvExchanger` (no state)

| kind | Size (px) | Shape |
|---|---|---|
| `shell` (default) | 100 × 48 | Horizontal cylinder + tube bundle (shell and tube) |
| `plate` | 64 × 80 | Plate type with stacked plates |

| Port | `shell` position | `plate` position | Meaning |
|---|---|---|---|
| `in` | Left center | Left, 24px from top | Main process inlet |
| `out` | Right center | Right, 24px from top | Main process outlet |
| `in2` | Top, 30 % from left | Top, 30 % from left | Heat medium (steam, cooling water) inlet |
| `out2` | Bottom, 70 % from left | Bottom, 70 % from left | Heat medium outlet |

- **`in`·`out` = the process fluid being heated or cooled**, **`in2`·`out2` = the medium that exchanges heat**. Which is shell side and which is tube side differs by design, so it is not in the names.
- The two paths (`in`→`out`, `in2`→`out2`) always pass and never mix. Even if the process side stops, flow on the medium side is judged separately.
- Medium supply and return are usually connected to terminals (`DvTerminal`). A terminal connected to the top port (`in2`) is placed above the exchanger, and one connected to the bottom port (`out2`) below it (chapter 9, item 4).
- Since all four faces are used, the name tag goes at the bottom, and if it would cover the bottom port it shifts sideways (5.1 `label`).

---

## 6. Connection (`DvLine`) rules

| Attribute | Description |
|---|---|
| `from` | The upstream end of the flow: `장비.포트` (equipment.port), or a face position `장비.면@위치` (6.1). For a terminal or a user symbol with only one port, just `장비` (equipment). For a unit, `유닛.포트` (unit.port — the name of that unit's `DvPort`); inside a unit, that unit's own ports are referenced by name only (chapter 8) |
| `to` | The downstream end of the flow. Same format as `from` |
| `arrow` | `end` (arrow into the `to` port) · `start` (into the `from` port) · `both` · `none` (default) |
| `flow` | The app decides this line's flow (true/false, bindable). If omitted, judged automatically (12.1) |
| `medium` | Medium color `water`·`steam`·`air`·`gas`·`oil`·`acid` (12.2). Propagates in the flow direction. If omitted, gray |
| `label` | Small text beside the pipe — the line number from the drawing (`01-100-PE-N`). Placed at the middle of the pipe's longest straight segment (below it if horizontal, to the right if vertical), in the theme text color. Does not affect layout, so give the pipe a segment long enough for the text |

**Rules**

- `from` → `to` is the flow direction. It determines the column order of the placement, so **writing it backwards places the equipment backwards.**
- Do not write a line twice between the same two ports.
- **Branches and merges**: lines touching the same port form one "pipe group" that shares a trunk and splits at T-junctions. There is no branch-point equipment or tag.
  - Several lines on a fixed port (`bottom`, `in`, `out`, etc.) → they split from that single point.
  - Several lines on a face port (`top`, `side`) → lines in the same direction (incoming with incoming, outgoing with outgoing) merge into one nozzle and gather into a T or cross in front of the tank. Incoming and outgoing lines are not merged (merging would make the inlet and outlet look connected outside the tank). To show separate nozzles, split the connections between `top` and `side`.
- Pipes of different groups never overlap, and when running in parallel they keep a minimum spacing (plain 12px, rich 16px). Where they must cross, a semicircular jump is drawn on one of them.
- There are **no** attributes for bend points. Change the shape with the hints in chapter 11. (Equipment coordinates exist only in coordinate placement, chapter 20 — and even there, pipe routes are automatic.)
- `DvLine` has no `visible`·`class`·`style` (ignored).

**Invalid connections**

| Mistake | Drawing | Trace |
|---|---|---|
| Nonexistent equipment name | Not drawn | Error "no equipment named 'X'" |
| Nonexistent port name | Red dashed line connecting the centers of the two pieces of equipment + a red border on that equipment | Error "'X' has no port 'p' (available ports)" |
| No port given on equipment with several ports (`from="P1"`) | Red dashed line + red border | Error "'X' requires a port (…)" |

All other lines and equipment are drawn normally. The available port names appear in the parentheses, so fix it with one of those.

### 6.1 Face position (`장비.면@위치`) — a nozzle at a set point

Instead of a port name, a line end (and an instrument `tap`, 7.3) can name **a point on a face of the equipment**: `T1.left@0.15` = the left face, 15 % of the way down.

```xml
<DvLine from="IN" to="T1.left@0.15"/>       <!-- nozzle N1: upper left -->
<DvLine from="PSV.out" to="T1.right@0.15"/> <!-- nozzle N2: upper right -->
```

- Face: `top` · `bottom` · `left` · `right` — the faces of the symbol rectangle **as seen on screen** (after `rotate`/`flip`).
- Position: 0–1 along the face — `top`/`bottom` from left to right, `left`/`right` from top to bottom. Outside 0–1 is clamped with a Trace warning; a wrong face or a non-number is an error.
- The pipe leaves straight out of that face. Lines naming the same point share one nozzle (a branch there).
- Works on any equipment except terminals and units (units connect only at their `DvPort`s). On a vessel the point counts as a vessel port for flow (12.1).
- Use it when a drawing fixes where nozzles are (mostly with `placement="fixed"`, chapter 20). Otherwise prefer the ports, which place nozzles automatically.

---

## 7. Attaching to equipment (`DvAttach`, `DvAgitator`, `DvMotor`, instrument `DvInstrument`, signal line `DvSignal`)

`DvAttach`·`DvAgitator`·`DvMotor` go **inside** an equipment tag. No other children may be written inside equipment (Trace error). The instrument `DvInstrument` and the signal line `DvSignal` go not inside equipment but **directly under `DvProcess` (or `DvUnit`)**, and an instrument points by name with `on` to the equipment it attaches to (7.3·7.4).

### 7.1 `DvAttach` — ordinary dvml beside equipment

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

| Attribute | Description |
|---|---|
| `side` | `left` · `right` (default) · `top` · `bottom` |
| Content | Any dvml (value displays, buttons, lamps, badges). Stacked vertically (can be changed with e.g. `style="display:flex; flex-direction:row"`) |

- Attached 8px from the symbol. `left`/`right` at the symbol's vertical center, `top` above the accessories and name tag, `bottom` below the name tag. If there are several on the same face, `top`/`bottom` stack in order but `left`/`right` overlap, so use only one per face.
- The attached cell is included in the equipment size for placement, and **pipes avoid that cell.** Vessel `side`·`top` ports also position themselves to avoid the span covered by an attached cell.
- Buttons and inputs inside work exactly as on an ordinary screen (`onclick` is a screen VM method).
- **Set text color with classes** (`label`, `caption`, `title`, `muted`, etc. — they follow the theme). Text without a class is dark regardless of the theme and is invisible on a dark background.

### 7.2 `DvAgitator` · `DvMotor` — accessories mounted on top

| Tag | Size (px) | Placement | State |
|---|---|---|---|
| `DvAgitator` | 40 × 100 | Top center of the equipment. The motor and gearbox (30px) stick out above, and the shaft and blades go into the vessel | `running` |
| `DvMotor` | 76 × 46 | Mounted whole on the top center of the equipment | `running` |

- Mainly used inside `DvVessel` (agitator). When an accessory is present, the vessel's `top` port leaves the center empty and takes positions on both sides.
- Pump drive motors are usually not drawn (they are considered part of the pump symbol). When the drawing shows one, put `DvMotor` inside `DvPump`: a shaft line joins the motor to the pump.
- `DvMotor kind="isa"` draws the ISA-5.1 motor instead: an `M` in a circle (36 × 52, stem included). Pair it with `DvPump kind="isa"` to match a P&ID drawing.
- A signal line (`DvSignal to="P1"`) to equipment with an accessory ends on the accessory (the motor), as drawings show it.

### 7.3 `DvInstrument` — instrument bubble

Draws field instruments (flow transmitter FT, level transmitter LT …) and controllers (FIC, LIC …) as ISA-5.1-style circles (bubbles). The instrument symbol (`tag`) is written in the upper part of the bubble and the number (`number`) in the lower part.

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

| Attribute | Required | Description |
|---|---|---|
| `name` | Yes | The name signal lines connect to. Must not collide with equipment names either (one each within the process diagram) |
| `on` | Yes (unless `tap`) | Name of the equipment to attach to (in the same process diagram and same unit). If missing or wrong, not drawn and a Trace error |
| `tap` | — | Connection line: a thin solid line from the bubble to a point on equipment — `장비.면@위치` (equipment.face@position, like `T1.left@0.85`, 6.1) or a fixed port `장비.포트` (equipment.port, like `CV1.in`, = the pipe just outside that port). The bubble then stands at that point instead of on a free face. That equipment is the instrument's equipment (`on` may be left out; if `on` names other equipment, `tap` wins with a warning) |
| `tag` | — | Text in the top of the bubble (instrument symbol: `FT`·`TT`·`LT`·`PT`·`FIC`·`LIC` …) |
| `number` | — | Text in the bottom of the bubble (loop number: `101`, `1001` …) |
| `panel` | — | If true, a horizontal line across the middle of the bubble (central control room instrument). Default false = no line (field instrument) |
| `value` | — | Small text to the right of the bubble (current value, etc.). Bindable |
| `onclick` · `style` · `visible` | — | Same as an ordinary box |

- **Always write `on` (or `tap`).** Instruments do not take part in the pipe-flow placement (columns and layers); they go into the footprint of the `on` equipment and are placed on a free face beside it. So when the equipment moves, the instrument follows, and **pipes avoid the instrument.**
- The face is chosen automatically: the face of that equipment with the fewest lines, attached cells and name tags; on a tie, top → right → bottom → left. It is placed 20px outside the name tag and attached cells on that face. Pumps, valves and inline equipment usually get the **top**; vessels using both top and bottom ports usually get the **side**.
- If one piece of equipment has several instruments, they sit side by side on that face (horizontally on top/bottom, vertically on a side). A single instrument is aligned to the equipment's center line (a controller above a control valve → the signal line drops straight down to the actuator).
- **A controller not attached to equipment** (control room instruments such as FIC, LIC) is attached with `on` to **the valve (or pump) that controller drives**. Like `FIC1` in the example above.
- **With `tap`** (to match a drawing): a face position puts the bubble 24px outside that point, on the line out of the face (`left@…` → to the left, `top@…` → above). A fixed port taps the pipe 12px out of the port and puts the bubble 24px above it (to the right of a vertical pipe). Several instruments with `tap` on the same equipment are each placed at their own point; instruments without `tap` keep the automatic face. When the connection line leaves the bubble to the right, the `value` text goes below the line.
- The bubble diameter is 40px (wider if the text is long); text sizes are `tag` 12px, `number` 11px, `value` 11px. The color follows `look` (same fill as terminals).
- The width of the `value` text is included in that equipment's size. If a value change alters the text width, placement is recomputed as with `DvAttach`, and surrounding items may move slightly. Large, frequently changing values are better shown separately with `DvAttach`.

### 7.4 `DvSignal` — signal line

Connects signals (electrical or control signals) between instruments, or between an instrument and equipment, with a **thin dashed line**.

```xml
<DvSignal from="FT1" to="FIC1"/>
<DvSignal from="FIC1" to="V1"/>
```

| Attribute | Description |
|---|---|
| `from` · `to` | Instrument name (`DvInstrument name`) or equipment name. Write **the name only, with no port** (writing it like `V1.in` is an error) |
| `arrow` | `end` · `start` · `both` · `none` (default). Small arrowhead at that end |
| `label` | Small text beside the `from` end (`SH`, `SL` …). Bindable |

- It is a different tag from pipes (`DvLine`). Signal lines are drawn **after all pipes are drawn**, and have **no effect** on equipment placement, flow judgment or flow animation. Adding or removing signal lines leaves equipment positions unchanged.
- Lines are orthogonal (horizontal and vertical) and avoid equipment bodies, name tags, attached cells and other instruments. Where they cross pipes they pass straight through **without a jump** (ISA convention). Paths running right alongside a pipe are avoided.
- Where the ends land: for an instrument, the center of the bubble face toward the counterpart (avoiding the right face with the value text, the face toward its attached equipment, and faces already used by other signal lines); for a control valve (`DvValve kind="control"`), **the top of the actuator**; for other equipment, the center of the body's face toward the counterpart (the body includes accessories, so a pump with a `DvMotor` is reached at the motor).
- `arrow="end"` (`start` · `both` · `none`, default none) draws a small arrowhead, like `DvLine`. `label="SH"` puts small text beside the `from` end (switch actions such as `SH`·`SL`, signal names). Write `from`·`to` in the direction the signal travels (measurement → controller → valve).
- Terminals (`DvTerminal`), unit ports (`DvPort`) and instruments that are not drawn (instruments with a wrong `on`) cannot be connected (Trace error; only that signal line is dropped). Instruments inside a unit connect only within the same unit.

---
## 8. Unit groups (`DvUnit` · `DvPort`)

Groups several pieces of equipment **into one unit.** Use it in two cases.

- **When the same arrangement repeats** — plant where the tank/agitator/pump arrangement is identical, such as 3 blenders or 2 reactors. Define the unit once as a component and use it many times (8.4).
- **When only part of the process flows in a different direction** — screens where the inside of a unit runs top→bottom (feed → tank → discharge pump) but the units must stand side by side.

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

### 8.1 `DvUnit` attributes and children

| Attribute | Description |
|---|---|
| `name` | **Required.** Unique within the outer process diagram. Outer lines connect with `name.포트` (unit name + port) |
| `direction` | Flow direction **inside** the unit: `lr` (default) · `tb`. Independent of the outer direction |
| `label` | Unit title. Top-left of the unit, font 13 bold (theme `--text`) |
| `row` · `rank` | Placement hints in the outer process diagram (chapter 11) |
| `x` · `y` | Only when the outer `DvProcess` has `placement="fixed"`: the center of the whole unit (chapter 20). The inside of a unit is always placed automatically |
| `style` · `class` | Border and background of the unit rectangle. **Default is no border and no background** (set e.g. `style="border:1 token(border); border-radius:8"`) |
| `onclick` · `visible` | Same as regular equipment (5.1) |

`look` follows the outer `DvProcess` (it cannot differ per unit).

**Children**: equipment (chapter 5, including user symbols), `DvLine`, `DvPort`. **A unit cannot contain another unit** (not drawn; Trace error).

**Name scope**: equipment names inside a unit only need to be unique within that unit. Three units may all contain `T` and `P1`.

### 8.2 `DvPort` — unit outer port

A connection point the unit exposes to the outside. **Outer lines can connect only to these ports.**

| Attribute | Description |
|---|---|
| `name` | Port name (unique within the unit). Lines inside the unit use **the name only** (`to="out"`); outer lines use `유닛.이름` (unit.name, `from="U1.out"`) |
| `side` | **Required.** Which side of the unit rectangle: `left` · `top` · `right` · `bottom`. Missing or invalid → `right` + Trace error |
| `at` | Position along that side, 0–1 (left/top = 0), default 0.5. Out of range → 0.5 + Trace error |

- Connecting several inner lines to one port (two pump outlets → `out`) merges them at the port (a merge). The same applies outside.
- A `DvPort` placed outside a `DvUnit` (directly under `DvProcess`) is ignored and leaves a Trace error.

**Do not connect directly from outside to equipment inside a unit.** `from="U1.T.bottom"` is an error (red marking + Trace "equipment inside a unit cannot be connected directly from outside. Add `<DvPort name="…" side="…"/>` to the unit, …"). Put a `DvPort` inside the unit, connect to that port from inside, and connect from outside with `U1.포트` (U1.port).

### 8.3 How units are placed

1. **The inside of the unit is placed first**, using the chapter 9 rules as-is (with the unit's `direction`).
2. **Unit rectangle (border)** = the rectangle enclosing all inner equipment footprints + 34px on every side (room for pipes to leave a port straight before bending).
3. **Ports** sit on the rectangle's `side` side at the `at` position, and inner pipes run up to the port. Inner pipes never leave the rectangle.
4. In the **outer process diagram**, the unit is a single piece of equipment the size of that rectangle. Outer pipes do not cross the unit rectangle and leave the port straight toward `side`.
5. The unit title (`label`) sits outside the rectangle at its top-left, and pipes avoid it.

**Choosing directions**: to stand several units **side by side**, make the outside `direction="tb"` and put the unit outlet ports at `side="bottom"` (the units stand horizontally on the same rank, and the receiving equipment comes centered below them). If the outside is `lr`, units in the same column stack vertically.

### 8.4 Repeating the same unit — components

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
<DvPageFrame vm="Plant.BlendPage" title="블렌더">
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

// 유닛 하나의 상태 — 같은 클래스의 객체를 유닛마다 하나씩
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

    // 템플릿 안 Reset 버튼 — 눌린 유닛의 BlenderVm 이 들어온다
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
registry.Register(blenderMarkup); // 위 <DvComponent name="Blender"> 문자열 — DvApp 이면 app.Registry 에
```

Rules:

- For the instance (`<Blender …>`), its `name`·`label`·`row`·`rank` win (resolved against the outer VM). If absent, the template `DvUnit`'s values (resolved against the parameters) are used.
- Inside the template **only the parameters** are visible (`{U.Level}`). Pass other screen VM properties as parameters. The simplest approach is one object of the same class per unit, passed as a single parameter (`U`).
- **How a button inside the template receives its own unit**: if `onclick="Reset"` and the screen VM has `Reset(BlenderVm unit)` with **exactly one parameter**, the instance's parameter value whose type matches (`U`) is passed. If none matches, the parameterless `Reset()` is called. With several parameters, the first type-matching value in declaration order is used. This rule applies equally to regular components outside process diagrams (the same mechanism as `DvRepeat`'s `M(항목)`).
- When only values change (level, running, temperature), neither the outer nor the inner layout is recomputed.


---

## 9. How layout is determined

The order is written down so an AI can predict the result and choose hints. The same markup **always gives the same result** (no randomness).

1. **Break cycles** — lines that loop back, such as recirculation, are reversed only for the layout calculation (the drawn direction stays). Loop-back lines take part in the in-column ordering (step 4) but are not used for alignment (step 5) — equipment positions are set only by flow-direction lines, and the recirculation pipe then routes around the equipment on top of that (so that in a top→bottom process the pump is not pushed beside the tank and the recirculation does not cross the inlet pipe).
2. **Assign columns** (lr = column, tb = rank) — each equipment's column = length of its longest upstream path (0 if it has no predecessor). Equipment with no predecessor (terminals, etc.) is pulled to the column just before its successor. If there is a `rank` hint, that value is used.
3. **Stack parallel equipment** — equipment with the same predecessor set and the same successor set, all of whose used ports are on flow-direction sides (lr: equipment using only top/bottom ports; tb: equipment using only left/right ports), is placed one step apart along the flow direction, standing on the same row. Two standby pumps stacking vertically in tb follow this rule. Parallel pumps with left/right ports in lr do not follow this rule; they go into the same column and are placed one above the other per steps 4–6.
4. **In-column order** — start with declaration order, then repeatedly sort by the average position of neighbors in the previous/next columns, choosing the order with the fewest line crossings. The neighbor position uses **the side of the port the line is attached to** — equipment connected to a top (tb: left) side port goes earlier, equipment connected to a bottom (tb: right) side port goes later (the `c` branch of a 3-way valve, `in2`·`out2` of a heat exchanger). If crossings still remain, adjacent equipment pairs are swapped on trial, and the order is kept only if crossings decrease. If there is a `row` hint, that value is the order key.
5. **Alignment** — each equipment is aligned to the average height (lr) / horizontal position (tb) of its neighbor ports in the previous column. Equipment with no predecessor (terminals, units) is aligned to its successor, then aligned once more against the previous column. So a series process becomes a straight line, and when several pieces of equipment converge on one (a merge, 3 units → 1 tank), the receiving equipment comes to the center. Ports on sides perpendicular to the flow are aligned according to `align`.
   - **Incoming (front) side ports** (top ports in lr, left ports in tb): always aligned to "the spot 26px outside in front of the port" — a line fed from above comes straight down and bends once to enter.
   - **Outgoing (back) side ports** (bottom ports in lr, right ports in tb): with `step`, 26px outside in front of the port (the next equipment sits lower than the tank → staircase, 1 bend, taller drawing); with `row`, the equipment's center line (the next equipment's inlet is at the tank's mid height → same row, pipe bends in a U shape with 3 bends, shorter and wider drawing). `auto` uses staircase if it fits the area, otherwise same row (sizing in chapter 4). Unit interiors use the same method as the outside.
6. **Spacing** — between equipment in the same column = 40px × spacing scale (minimum 28px). Corridor between columns = 48px × spacing scale (minimum 44px) + number of pipe bundles passing through that corridor (lines connected by branches/merges form one bundle) × pipe spacing (plain 12px, rich 16px). Column width = the widest equipment (footprint) in that column; equipment is centered in the column.
7. **Footprint** — the size one piece of equipment occupies = the rectangle enclosing symbol + accessories + name label + attached boxes. So equipment with a large attached box also widens its column.

---

## 10. How pipe routes are determined

1. Pipes leave the port **18px straight** outward (the port's lead-out end). No bend directly in front of a port.
2. Routes are searched along an 8px grid (+ port coordinates). Cost = length + bend (30) + crossing another pipe (40). Following an already-drawn trunk of the same bundle costs 1/5, so branches and merges share a trunk.
3. Routes do not enter within 10px of an equipment symbol, or within 4px of name labels and attached boxes.
4. Pipes do not overlap pipes of other bundles, do not run closer than the pipe spacing when parallel, and do not bend on top of another pipe (it would look like they touch).
5. If there is still no route, these constraints are turned into costs and the search is repeated (overlap allowed), leaving the Trace warning "no free path, so it overlaps other piping".
6. Bundles are drawn in declaration order (lines written first get the good routes first).
7. At crossings with other bundles a semicircular jump is drawn: in lr the vertical line jumps, in tb the horizontal line jumps (radius plain 7, rich 13).
8. Pipe ends tuck slightly under the equipment body (fixed ports 6px, side ports 24px) and are hidden. So they appear attached without a gap even on the top of a rounded vessel.

---

## 11. Refining with hints (`row`·`rank`·`rotate`·`flip`)

Use hints only when needed **after viewing the result as a PNG**. If it came out clean, do not use them.

| Hint | Value | Effect | When |
|---|---|---|---|
| `row` | integer | Order key within the same column. Smaller value = top (lr) / left (tb) | When lines cross, or to set the top/bottom order of equipment |
| `rank` | integer ≥ 0 | Fixes that equipment's column | To push a particular piece of equipment to a column further right (down) |
| `align` (`DvProcess`) | auto·step·row | Equipment after a tank-bottom outlet as staircase / same row | Fix it only when you don't like the shape `auto` chose (chapter 4, chapter 9) |
| `rotate` | 0·90·180·270 | Rotates equipment (port sides rotate too) | Valve on a vertical pipe (`90`) |
| `flip` | h·v·hv | Flip | Pump with reversed flow (`h`) |

Caveats:

- It is safest to give `row` **to all equipment in the same column**. If only some have it, it mixes with the order key (current index) of equipment without hints and may differ from what you expect.
- `rank` is a **last resort**. Forcing a column can break flow order, making lines run backward or cross. Column numbers start at 0, and other equipment's columns are computed automatically, so it is hard to predict. Always re-render to check after using it.
- Hints do not solve a lack of space. Enlarge the area, split the screen, or reduce the equipment count (e.g., omit the inlet valve and go straight from the terminal to the vessel).

**Example: swapping the top/bottom order of parallel pumps**

```xml
<DvProcess>
  <DvVessel name="T1" level="50"/>
  <DvPump name="P1" row="1" label="P-101"/>
  <DvPump name="P2" row="0" label="P-102"/>
  <DvValve name="V1" open="true"/>
  <DvLine from="T1.bottom" to="P1.in"/>
  <DvLine from="T1.bottom" to="P2.in"/>
  <DvLine from="P1.out" to="V1.in"/>
  <DvLine from="P2.out" to="V1.in"/>
</DvProcess>
```

**Example: valve on a vertical pipe**

```xml
<DvProcess direction="tb">
  <DvTerminal name="IN" text="FEED"/>
  <DvValve name="V1" rotate="90" open="true" label="XV-1"/>
  <DvVessel name="T1" level="30" label="T-1"/>
  <DvLine from="IN" to="V1.in"/>
  <DvLine from="V1.out" to="T1.top" arrow="end"/>
</DvProcess>
```

---

## 12. Look (`look`)

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

- Colors are a fixed process-diagram palette, not theme tokens (only name-label text uses theme `--text`).
- The process diagram does not decide alarm colors (red, etc.). Alarms are shown with `DvLamp`·`DvBadge` inside `DvAttach`, or with alarm widgets.
- Pipes are not colored by medium (water, steam, etc.) **by default** (ISA-101 recommendation — chapter 18). Opt in per line with `medium` (12.2).

### 12.1 Flow display (`flow-animation` · `flow`)

**The library decides from connections and equipment state** which pipes currently have flow, and when enabled, a dashed line moves along those pipes in the flow direction (`from` → `to`).

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
| `DvLine flow` | The app decides this line's flow (override). True = flow, false = blocked. If not given or the bound value is null, automatic decision |

**Automatic decision rules** (an inference for display, not a measurement)

1. **Sources**: terminals used as a line's `from` (coming in from outside), vessels with level greater than 0. **Sinks**: terminals used as a line's `to`, vessels.
2. **Paths**: lines only in the `from` → `to` direction. Inside equipment, the symbol's flow rules (`DvPass`) — pumps when running, valves when open (`open` true or greater than 0), check valves one-way `in` → `out`, inline equipment always. Vessels are buffers and do not pass through internally (incoming and outgoing are separate). Unit ports connect inside and outside.
3. **Flow**: a line flows **only when it is reached from a source and continues from that line to a sink**. So a pipe ending before a closed valve, the branch toward a stopped pump, and the downstream side of an empty tank do not flow.
4. **Override**: a line with `flow="false"` is treated as blocked, and its downstream side also stops. A line with `flow="true"` flows and is treated as connected upstream and downstream (flow even behind a stopped pump, if a flowmeter shows flow).
5. Re-decided every time values change and the screen is rebuilt (layout unchanged, almost no cost).

**When to use `flow`**: the inference can be wrong because of equipment not on the screen (manual field valves, etc.) — e.g., it looks like flow because the pump runs and the valve is open, but an off-screen valve is closed. **Bind `flow` on lines that have real data, such as flowmeters and flow switches.** Leave the rest to automatic.

**Look and cost**

- Look: a base stripe (plain = light sky blue, rich = dark teal) is laid on flowing pipes, and a thick dashed line (plain = blue 4px, rich = teal 5px) moves on top — the high light/dark contrast between dashes and gaps keeps it from blending with the tube color. On a pipe with `medium` (12.2) the stripe and the dashes use tones of that medium's color instead, so the medium stays visible while it flows. Speed 40px/s.
- If at least one pipe is flowing, it **redraws at 20 frames per second** (only during that time). With no flow, or `flow-animation` off, it does not redraw.
- Adapters (OpenTK CPU raster and GPU, WinForms) then redraw **only the area containing the process diagram** (`DvSurface.PartialRender`) — cost scales with the area of the flowing process diagram, not the whole screen. Measured (1600×1080 window, CPU raster): a small process diagram at about 7% of the screen goes from 27% → 7% of one core; a demo page that is mostly process diagram stays about 50% (almost unchanged — large area to redraw). `ViewWindow.Gpu` mode on the same small process diagram: 8.7% → 3.8% (CPU time).
- With animation off, flow does not appear in the drawing (only the decision result exists).

---

### 12.2 Medium color (`medium`)

Shows what flows inside (water, steam …) through pipe color. **None by default** (ISA-101 — color only when it carries information). Process diagrams without `medium` stay gray as before.

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

Colors follow the KS A 0503 pipe identification color family, and the library chooses them so they read in both looks (`plain`·`rich`). **You cannot give color values directly** (only the 6 names above). Any other value → default gray + Trace warning. Bindable (`medium="{M}"`).

**Write it once and it spreads in the flow direction.**

- From the line where it is written, it continues in the `from` → `to` direction, and inside equipment along that equipment's paths (pump `in`→`out`, valves, inline, 3-way valve branches, heat exchanger `in`→`out` and `in2`→`out2` separately) to the next line. **State does not matter** — behind a stopped pump or closed valve it is the same pipe, so the same color.
- It stops at **vessels** (mixing inside), **terminals**, and **lines with their own `medium`**. So after a tank, or where the medium changes (e.g., when you want to color steam → condensate separately), write it again on that first line.
- Unit ports connect inside and outside.
- When different media meet on one line (mixing), it is colored with the medium written first and a Trace warning is left. Set it by writing `medium` directly on the line after the merge.
- No legend is drawn automatically. If needed, put one on the screen with `DvAttach` or regular text.
- Only the color changes. Layout and flow decision are unaffected. With flow animation (12.1) on, a flowing pipe with a medium draws its base stripe and moving dashes in tones of the medium color (darker and lighter), so the medium stays readable.

## 13. View model (C#) and updates

Process diagrams use the same VM binding as regular screens. Equipment state is VM properties; actions on tapping equipment are VM methods.

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

    // 펌프를 누르면 정지 확인
    public async void P1Click()
    {
        if (await App.MessageBox("P-101 을 정지할까요?", "펌프", DvMessageButtons.YesNo) == DvDialogResult.Yes)
            P1Run = false; // await 뒤는 입력 스레드 — 자동으로 다시 그림
    }

    // 통신 스레드에서 값이 오면: 값을 바꾸고 App.Invalidate()
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
<DvPageFrame vm="Plant.WaterPage" title="정수 공정">
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

- When only values (`level`, `running`, `open`, attached-box text, instrument `value`) change, **layout is not recomputed.** If equipment, line structure, name labels, attached-box sizes and the width of instrument `value` text are the same, the previously computed layout is reused. This holds inside units too. On a 1600×1000 screen, one value update costs 1–2ms for layout and about 16ms for drawing (CPU).
- If attached-box text length changes and the box size changes, or the instrument `value` text gets wider or narrower, layout is recomputed (tens of ms). Recent layouts are kept, so going back to a width seen before reuses its layout. Giving frequently changing numbers a fixed width (`style="width:…"` on the attached box) keeps the layout from shifting.
- Thread rules are the same as regular screens: after changing the VM on a communication/timer thread, call `App.Invalidate()`. Handlers (`onclick`) and code after their `await` run on the input thread, so `Invalidate` is not needed (`llm-rules.md` chapter 6).
- To draw several units of the same arrangement (3 blenders, etc.), use a unit component. Keep one VM object of the same class per unit and pass it as a parameter (8.4).

---

## 14. User symbols (`DvSymbol`)

Equipment not in chapter 5 (cyclones, conveyors …) is used by defining a symbol. Built-in equipment is defined in exactly the same format.

### 14.1 Definition and registration

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
registry.Register(CycloneMarkup);   // 컴포넌트와 같은 Register — 루트가 DvSymbol 이면 심볼로 등록
// DvApp 이면 app.Registry 에, DvRenderer·DvSurface 면 registry 인자로 넘깁니다.
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

- Inside a process diagram, **the symbol `name` is the tag** (`<Cyclone …>`). All common attributes (5.1) are available.
- If you register under a built-in tag name (`DvVessel`, `DvPump`, `DvValve`, `DvInline`, `DvTerminal`), the built-in wins. Use a different name.
- Registering the same name again replaces it with the new definition. It is not applied to screens already built, so register before building screens.

### 14.2 Elements

| Element · attribute | Description |
|---|---|
| `DvSymbol name` | Required. Name used as the tag |
| `DvSymbol view-box` | Required. `"0 0 너비 높이"` (0 0 width height). **Only width and height (positive) are used** — the first two values are always `0 0`. These values are the on-screen size (px) as-is |
| `DvParam name type default` | A state value received from outside. Bound via an instance attribute (`running="{…}"`). `type` is descriptive (not checked); `default` is the value when the attribute is not given |
| `DvShape class path when` | One drawing layer. Drawn top to bottom (the one written first is behind). `path` = SVG path syntax, `class` = fill (table below), `when` = name of the parameter a `state` layer follows |
| `DvPort name side at role` | Port. `side` = `left`·`top`·`right`·`bottom`, `at` = position along that side 0–1 (left/top = 0), default 0.5. `role` = `in`·`out`·`inout` (default). A port is a point on the view-box border |
| `DvPass from to when` | Flow rule (which port passes to which port, when the `when` parameter is true). Currently only validated, not used in drawing (used in flow display stage 3) |
| `DvFill clip value` | Level fill. `clip` = the `class` used as the fill shape (the union of that class's layers), `value` = name of a 0–100 parameter. The liquid is painted right after the first layer of that class, so layers written later (agitator shaft, etc.) show on top of the liquid |

`DvShape class` — colors are not put in the path but set by class. That is why the same symbol is drawn in both looks, `plain` and `rich`.

| class | Fill | Used for |
|---|---|---|
| `vessel` | Body (no state) — plain gray, rich metal | Vessels, tubes, main bodies |
| `state` | Body; running color when the `when` parameter is true | Pump casings, valve bodies, motors |
| `accent` | Dark color | Flanges, bases, handles, nozzles, small parts |
| `detail` | Stroke only (1.6px, round caps) | Blades, patterns, axis lines |
| `dash` | Dashed stroke only | Filter mesh, internal boundaries |
| `screen` | Dark display window | Instrument screens |
| `glass` | Light glass | Sight glasses |

- Filled layers (`vessel` `state` `accent` `screen` `glass`) are all drawn with an outline stroke.
- If any `state` layer is in the running state, `detail` strokes switch to a light color so they stay visible on the dark body.
- A `state` layer without `when` is always the stopped color.

### 14.3 How to draw a symbol (when an AI writes the path)

1. **Decide the size first.** Match proportions with built-in equipment: rotating equipment and valves 60–72 × 48–64, vessels 100 × 110–160. Too small (under 40) gets lost next to name labels and pipes.
2. **Coordinate system**: top-left is (0, 0), bottom-right is (width, height). Use absolute commands (`M L H V Q C A Z`). Two decimal places are enough.
3. **Draw the body touching the port positions.** A port is a point on the border, and pipe ends tuck 6px under the body to be hidden. If the space in front of a port is empty (the body does not reach the border), the pipe end appears to float in the air, so draw a nozzle (a small `accent` rectangle) reaching the border. Example: left-center port → `M0 26 H8 V38 H0 Z`.
4. **Layer order**: base/flanges (`accent`) → body (`vessel`/`state`) → pattern (`detail`) → small parts (`accent`). Layers written first lie underneath.
5. **Match port sides to the flow direction**: regular equipment has inlet `left`, outlet `right`. Equipment that receives from above and discharges below (cyclone dust, hoppers) uses `top`/`bottom`.
6. **Do not put rotation in the definition.** If you need a different orientation, use `rotate`·`flip` on the instance.
7. **Always render to check**: both looks (`plain`·`rich`), both running and stopped.

### 14.4 Definition errors

If a definition is wrong, `Register` throws an `ArgumentException` describing what is wrong (the screen does not open — it surfaces immediately at startup).

| Words in the message | Cause |
|---|---|
| `requires a name` | `DvSymbol` has no `name` |
| `view-box` | Format is not `"0 0 W H"`, or W·H is 0 or less |
| `side` | Port side is not `left`·`top`·`right`·`bottom` |
| `at` | Port position outside 0–1 |
| `role` | Not `in`·`out`·`inout` |
| `duplicate port` | Two ports with the same name |
| `DvPass` | Flow rule points to a nonexistent port |
| `class` | Unknown `DvShape` class |
| `path` | SVG path syntax error |
| `clip` | `DvFill clip` matches no `DvShape` class |
| `unknown child` | Unknown child element (e.g., `DvCircle`) — circles and rectangles are also written as paths |

Circle path example (center cx, cy, radius r): `M{cx-r} {cy} A{r} {r} 0 1 0 {cx+r} {cy} A{r} {r} 0 1 0 {cx-r} {cy} Z` → for center (30, 30), radius 10: `M20 30 A10 10 0 1 0 40 30 A10 10 0 1 0 20 30 Z`.

---

## 15. Diagnostics and fixes

Process diagram problems are reported through `System.Diagnostics.Trace`. Keep a Trace listener on during development (console: `Trace.Listeners.Add(new ConsoleTraceListener())`). The same message appears only once per process. Layout quality warnings are based on **the layout actually drawn**.

| Message (prefixed with `Devinno.UI process:`) | Kind | Meaning | Fix |
|---|---|---|---|
| `'A.out'→'B.in': crosses other piping N time(s)` | Warning | Crossings (drawn with jumps) | Change the order of equipment in the same column with `row`. Arrange declaration order in flow order |
| `'A.out'→'B.in': no free path, so it overlaps other piping` | Warning | Blocked, drawn overlapping | Enlarge the area, adjust `row`·`rank`, move attached boxes |
| `Not enough space: needs W×H, area is W×H` | Warning | Overflows even with minimum spacing | Enlarge the area, split the screen, or reduce equipment |
| `DvLine '…': no equipment named 'X'` | Error | Name typo, or equipment hidden with `visible` | Check the name |
| `DvLine '…': 'X' has no port 'p' (…)` | Error | Port name typo | Use a name from the parentheses |
| `DvLine '…': 'X' requires a port (…)` | Error | Port omitted, as in `from="X"` | Use `X.in`, etc. |
| `DvLine '…': an endpoint (DvTerminal) is written without a port, …` | Warning | `.port` appended to a terminal | Name only |
| `<Tag> is not process equipment (…)` | Error | Non-equipment tag directly under `DvProcess`, or unregistered symbol | Move it into `DvAttach`, or register the symbol |
| `<X> has no name` | Error | `name` missing | Add `name` |
| `Equipment name 'X' is used twice` | Error | Duplicate name | Use a different name |
| `<Y> is not allowed inside <X> (…)` | Error | Tag other than `DvAttach`·`DvAgitator`·`DvMotor` inside equipment | Wrap it in `DvAttach` |
| `<X kind='k'> is not a known kind (…)` | Warning | kind typo | Use a value from the parentheses |
| `'T': not enough room on the top face for N nozzles (incoming and outgoing lines) …` | Warning | Agitator / attached boxes cover most of the side, so two nozzles are too close | Move the incoming or outgoing line to `side`, or move the attached box |
| `DvProcess align='…' is not a valid value` | Warning | align typo | `auto`·`step`·`row` |
| `DvLine '…': equipment inside a unit cannot be connected directly from outside …` | Error | Connecting into a unit directly from outside, as in `U1.T.top` | Put a `DvPort` on the unit, connect to the port from inside, and from outside use `U1.포트` |
| `DvLine '…': 'U1' has no port 'p' (…)` | Error | Port name not on the unit | Use a name in the parentheses (that unit's `DvPort` names) |
| `DvLine '…': a unit port (DvPort) is written by name only …` | Warning | A dot appended to a port inside the unit, as in `out.x` | Name only |
| `<DvPort> can only be used inside <DvUnit>` | Error | `DvPort` directly under `DvProcess` | Move it into a unit |
| `<DvPort name='…'> side must be one of …` · `at='…' must be between 0 and 1 …` | Error | Port side / position error | Always give `side`; `at` within 0–1 |
| `<X>: a unit cannot be placed inside another unit` | Error | Nested units | Move it outside |
| `rotate='r' is not a valid value` · `direction=…` · `look=…` | Warning | Value typo | Use a value from the tables |
| `<DvInstrument name='X'> has no on (name of the equipment to attach to)` · `on='Y': no such equipment` | Error | No equipment for the instrument to attach to (not drawn) | Put the name of equipment in the same process diagram in `on` |
| `<DvInstrument name='X'> tap='…': …` | Error | `tap` target missing, a terminal or unit, no port given, an unknown port, or a face port without a position (`T1.side`) — not drawn | `장비.면@위치` or a fixed port such as `CV1.in` |
| `'…' is not a face (top·bottom·left·right)` · `'@…' is not a position` · `position … is outside 0–1` | Error (last: Warning, clamped) | Face position typo in `DvLine` `from`/`to` or `tap` | `left@0.85` form |
| `<DvInstrument> has no name` · `Name 'X' is used twice (equipment and instrument names must be unique …)` | Error | Instrument name missing, or clashing with equipment or another instrument | A unique `name` |
| `DvSignal '…': no instrument or equipment named 'X'` | Error | Signal line end name typo, a terminal / unit port, or an instrument that is not drawn | Check instrument / equipment names |
| `DvSignal '…': signal lines use names only, without ports` | Error | Port appended, as in `to="V1.in"` | Name only |
| `Signal line '…': no free path, so it is drawn across equipment` | Warning | No route for the signal line | Attach the instrument to other equipment, or enlarge the area |
| `<X name='N'>: placement="fixed" needs both x and y …; not drawn` | Error | Coordinate placement, but the equipment has no (or an invalid) `x`/`y`. Lines to it then also report "no equipment named" | Give both `x` and `y` |
| `'A' and 'B' overlap; move one of them (x/y) or enlarge base-size` | Warning | Coordinate placement: two pieces of equipment (symbol, label, attached boxes, instruments) overlap | Move one, or check `base-size` against the area |
| `Not enough space: equipment reaches W×H, area is W×H …` | Warning | Coordinate placement: equipment goes past the area | Move equipment, set `base-size`, or enlarge the area |
| `<X name='N'>: x/y are used only with <DvProcess placement="fixed">` · `row`/`rank`/`align … is not used with placement="fixed"` · `placement='…'` · `base-size …` | Warning | Attribute for the other placement mode, or a value typo | Remove it, or switch the mode |

On screen, invalid connections show as a **red dashed line**, and equipment with a wrong port shows a **red border**.

**Build-time checks**: if the process diagram is in a `*.dvml` file, the source generator reports connection errors at build time as diagnostics on that dvml line — DVML008 (missing name), DVML009 (port error), DVML010 (duplicate name), DVML011 (invalid value, warning). The messages have the same meaning as the table above. Names/values containing bindings (`{…}`) and equipment whose ports are unknown at build time (`DvSymbol` registered in code, component units) are skipped at build time and reported only through Trace at run time. Layout quality (crossings, overlaps, space) is known only at run time.

**AI check order**: ⓪ build and confirm there are no DVML errors → ① render PNG → ② confirm no errors (Error) in Trace → ③ confirm no warnings (crossings, overlaps, space) → ④ in the drawing, confirm pipes don't pass through equipment, name labels / attached boxes don't overlap, and flow direction is correct → ⑤ if there is a problem, chapter 11 hints → back to ①.

```csharp
using System.Diagnostics;
using Devinno.UI;
using Devinno.UI.Styling;

Trace.Listeners.Add(new ConsoleTraceListener());
DvRenderer.RenderToFile(markup, vm, 1280, 800, "process.png", DvTheme.Dark, registry);
```

---

## 16. Complete examples

### 16.1 Left→right water treatment process (parallel pumps, control valve, reactor)

```xml
<DvProcess look="rich">
  <DvTerminal name="IN" text="RAW WATER"/>
  <DvVessel name="T1" level="{T1Level}" label="T-101"/>
  <DvPump name="P1" running="{P1Run}" label="P-101"/>
  <DvPump name="P2" running="{P2Run}" label="P-102"/>
  <DvValve name="V1" kind="control" open="{V1Open}" label="FCV-101"/>
  <DvInline name="F1" kind="flowmeter" label="FT-101"/>
  <DvVessel name="R1" kind="reactor" level="{R1Level}" label="R-201">
    <DvAgitator running="{AgitatorRun}"/>
  </DvVessel>
  <DvTerminal name="OUT" text="TO DRAIN"/>
  <DvLine from="IN" to="T1.top" arrow="end"/>
  <DvLine from="T1.bottom" to="P1.in"/>
  <DvLine from="T1.bottom" to="P2.in"/>
  <DvLine from="P1.out" to="V1.in"/>
  <DvLine from="P2.out" to="V1.in"/>
  <DvLine from="V1.out" to="F1.in"/>
  <DvLine from="F1.out" to="R1.top" arrow="end"/>
  <DvLine from="R1.bottom" to="OUT"/>
</DvProcess>
```

### 16.2 Top→bottom blender (two inlets, agitator, value display, parallel discharge pumps)

```xml
<DvProcess direction="tb" look="rich">
  <DvTerminal name="MTO" text="MTO RMMT-1"/>
  <DvTerminal name="UGTF" text="UGTF BL-1"/>
  <DvVessel name="BL" level="{BlLevel}" label="BL-1">
    <DvAgitator running="{AgitatorRun}"/>
    <DvAttach side="right">
      <DvLabel class="caption muted" text="TIA 1001"/>
      <DvLabel class="title" text="{BlTempText}"/>
    </DvAttach>
    <DvAttach side="left">
      <DvButton variant="secondary" size="sm" text="Reset" onclick="ResetBl"/>
    </DvAttach>
  </DvVessel>
  <DvPump name="RP1" running="{Rp1Run}" label="RTP 1001"/>
  <DvPump name="RP2" running="{Rp2Run}" label="RTP 1002"/>
  <DvTerminal name="OUT" text="RESIN TANK"/>
  <DvLine from="MTO" to="BL.top" arrow="end"/>
  <DvLine from="UGTF" to="BL.side" arrow="end"/>
  <DvLine from="BL.bottom" to="RP1.in"/>
  <DvLine from="BL.bottom" to="RP2.in"/>
  <DvLine from="RP1.out" to="OUT"/>
  <DvLine from="RP2.out" to="OUT"/>
</DvProcess>
```

Result: `MTO` enters the left of the top side, avoiding the agitator; `UGTF` enters the side facing it; and the two pumps stack one above the other below the bottom, sharing a left inlet trunk and a right outlet trunk.

### 16.3 Recirculation (loop-back line)

```xml
<DvProcess>
  <DvVessel name="T1" level="70" label="T-1"/>
  <DvPump name="P1" running="true" label="P-1"/>
  <DvValve name="V1" open="true" label="XV-1"/>
  <DvTerminal name="OUT" text="PRODUCT"/>
  <DvLine from="T1.bottom" to="P1.in"/>
  <DvLine from="P1.out" to="V1.in"/>
  <DvLine from="V1.out" to="OUT"/>
  <DvLine from="P1.out" to="T1.side" arrow="end"/>
</DvProcess>
```

The recirculation pipe branching from `P1.out` routes back into the tank's side. Recirculation is also written simply as `from` (upstream) → `to` (downstream).

---

## 17. Checklist (MUST · NEVER)

**MUST**

- Write process screens only as equipment + `<DvLine>` inside `<DvProcess>`.
- Every piece of equipment has a unique `name`. On-screen text is `label` (`text` for terminals).
- `from` = upstream, `to` = downstream.
- Use only the defined port names: vessel `top`·`bottom`·`side`; pump/valve/inline `in`·`out`; 3-way valve `a`·`b`·`c`; heat exchanger `in`·`out`·`in2`·`out2`; terminals by name only; user symbols by the names they define.
- Give state by binding or literal (`open="true"`). If not given, it is stopped / closed / empty vessel.
- Value displays and buttons go inside `<DvAttach>`; text uses classes (`label`·`caption`·`title`).
- To fill the area, `grow="1" style="height:0"` (inside a vertical layout).
- After fixing, render to PNG and check Trace errors and warnings.
- Place instruments (`DvInstrument`) directly under `DvProcess` and always state the equipment to attach to with `on` (or with `tap`, when matching a drawing). Attach controllers to the valve they move. Signal lines use `DvSignal`, ends by name only.
- When there are several identical units of plant, define a unit component (template root `DvUnit`) once and repeat it as instances. For unit inside → outside connections, put a `DvPort` and use `유닛.포트` (unit.port).
- For a button inside a unit template to receive its own unit, write the screen VM method with one parameter, like `Reset(BlenderVm unit)`.
- Use automatic placement unless you were given a drawing the screen must match. Then use `placement="fixed"` with `x`·`y` on **every** piece of equipment (chapter 20).

**NEVER**

- Do not put `DvTank` (level widget), `DvLabel`·`DvButton` directly in a process diagram (→ `DvVessel`, `DvAttach`).
- Do not invent bend-point attributes such as `points`·`via`. They do not exist. Do not put `x`·`y` on equipment in automatic placement (they work only with `placement="fixed"`, chapter 20), and do not mix: in a `fixed` diagram every piece of equipment needs `x`·`y`.
- Do not overlay absolutely positioned boxes on the process diagram to draw values or lines (pipes cannot avoid them).
- Do not invent junction equipment (`DvTee`, `DvJunction`, etc.). Connect several lines to the same port.
- Do not use dots (`.`) in names.
- Do not hide connected equipment with `visible`.
- Do not sprinkle `row`·`rank` from the start. Render first, then only where needed.
- Do not try to put colors in user symbol paths (there is no attribute). Colors come from `class`.
- Do not connect directly from outside to equipment inside a unit, as in `U1.T.top` (use `DvPort`).
- Do not put a unit inside a unit. Do not bind screen VM properties directly inside a unit component template (only parameters are visible).
- Do not compute flow per line as VM properties — leave it to the automatic decision, and bind `flow` only on lines with real measurements.
- Do not draw signal lines as `DvLine` (pipes) (they interfere with flow and layout). Use `DvSignal`.
- Do not invent attributes that set an instrument's position or side (`side`·`x`·`y`). They do not exist — the position is automatic, next to the `on` equipment.
- Do not try to set pipe color via `style` or color values. Medium colors exist only as the 6 names of `medium`.
- Do not invent the not-yet-available features in chapter 18 as attributes.

---

## 18. Not yet available

Planned for later stages. Do not use them now (they are silently ignored or drawn as errors).

| Stage | Content |
|---|---|
| On hold | `exit` hint (specifying the outgoing direction) — when a real screen needs it |

Other items not planned: specifying bend points directly, zoom (in/out), pipe click events, dragging equipment.

---

## 19. FAQ

**I want a pipe to be a straight line.**
It is straight when the connected ports are on the same row. Series equipment aligns automatically. When branches, merges and side ports mix, bends appear; this is normal.

**I want to swap the order of two pieces of equipment.**
In the same column, use `row`. To move one to a different column, first check the flow (`from`/`to`) is right, and if still needed, use `rank`.

**I want a valve to stand vertically.**
`rotate="90"` — the inlet goes on top and the outlet on the bottom.

**I want to choose which side of the tank a line enters.**
`side` automatically picks the side facing the other equipment. If you need the opposite side, make sure attached boxes do not cover that side and try changing the other equipment's order with `row`. There is no attribute to choose the side directly.

**Pipes go down like stairs and the drawing is too tall.**
This is due to the staircase layout, where equipment after a line leaving the tank bottom drops below the tank. If the area is too small, `align="auto"` switches to the same-row method. To keep it low regardless of area, use `align="row"`.

**Several lines enter the tank top separately and it looks cluttered.**
Lines on the same side and in the same direction are already merged into one nozzle. What appears separate is an incoming and an outgoing line, or different left/right sides (`side`).

**The screen is small and it overflows.**
Alignment (`auto`) and spacing (down to 0.5×) adjust automatically. If it still overflows, enlarge the area, split the process across two screens, or omit less important equipment (inlet valves, etc.).

**I have several identical units of plant (3 blenders).**
Define a unit component once and use it like `<Blender name="BL1" U="{BL1}"/>` (8.4). To stand them side by side, outer `direction="tb"` + unit outlet port `side="bottom"`.

**I want to connect an outer pipe directly to a tank inside a unit.**
Not possible. Put `<DvPort name="in2" side="left"/>` on the unit, connect `<DvLine from="in2" to="T.side"/>` inside, and connect with `to="BL1.in2"` from outside.

**The screen must look exactly like the customer's drawing.**
Use coordinate placement (chapter 20): `placement="fixed"`, and the symbol center of each piece of equipment from the drawing as `x`·`y`. Pipes are still routed for you.

**Can I give process diagram equipment an `id` to get it from code?**
A code handle (`id`) for process diagram equipment is not a confirmed usage. Handle equipment state through VM binding and actions through `onclick`.

---

## 20. Coordinate placement (`placement="fixed"`) — matching a given drawing

Automatic placement (chapters 1–19) lays equipment out in flow order so that it looks right. It cannot reproduce a specific drawing. When a customer hands you a P&ID and the screen must look **the same as that drawing**, place the equipment by coordinates instead.

- **One mode per diagram.** A `DvProcess` is either automatic or coordinate-placed. In a `fixed` diagram, every piece of equipment (and every unit) has `x`·`y`; there is no mixing.
- **Only equipment gets coordinates.** Pipes, branches, merges, crossing jumps, instruments and signal lines are still placed and routed automatically, exactly as in chapters 6–10. Moving a piece of equipment moves its pipes with it.
- **Text and equipment are never scaled.** Only the distances between equipment change with the area (20.3).

### 20.1 At a glance

```xml
<DvProcess placement="fixed" base-size="1000 560" look="rich">
  <DvTerminal name="SRC1" text="FROM P-201" x="110" y="140"/>
  <DvValve name="FV40" kind="control" open="true" label="FV-3040" x="300" y="140"/>
  <DvInline name="FE10" kind="flowmeter" label="FE-10" x="470" y="140"/>
  <DvTerminal name="SRC2" text="FROM P-504" x="110" y="400"/>
  <DvValve name="FV41" kind="control" open="true" label="FV-3041" x="300" y="400"/>
  <DvInline name="FE11" kind="flowmeter" label="FE-11" x="470" y="400"/>
  <DvVessel name="R1" kind="reactor" level="55" label="R-102" x="800" y="270"/>

  <DvLine from="SRC1" to="FV40.in"/>
  <DvLine from="FV40.out" to="FE10.in"/>
  <DvLine from="FE10.out" to="R1.side" arrow="end"/>
  <DvLine from="SRC2" to="FV41.in"/>
  <DvLine from="FV41.out" to="FE11.in"/>
  <DvLine from="FE11.out" to="R1.side" arrow="end"/>
</DvProcess>
```

### 20.2 Coordinates

| Attribute | On | Meaning |
|---|---|---|
| `placement="fixed"` | `DvProcess` | Turns on coordinate placement |
| `x` · `y` | each piece of equipment, each unit | The **center of the symbol** in px (labels, attached boxes, instruments and accessories such as an agitator are not counted). Bindable |
| `base-size="W H"` | `DvProcess` | Optional. The size the coordinates were taken at (20.3) |

- The origin `(0, 0)` is the top-left of the inside of the `DvProcess` (inside its `padding`).
- Labels, `DvAttach` boxes, instruments and accessories go where they would in automatic placement, relative to their equipment.
- To match the drawing's nozzles and instruments, connect at face positions (`to="T1.left@0.15"`, 6.1) and give instruments `tap` (`tap="T1.left@0.85"`, `tap="CV1.in"`, 7.3) — the bubble then stands where the drawing has it, joined by a connection line.
- `direction` still matters (terminal shape, which faces are "front" and "back"). `row`, `rank` and `align` are ignored with a Trace warning.
- Equipment without both `x` and `y` is **not drawn** (Trace error).
- A unit (`DvUnit` or a unit component) is placed as a whole: `x`·`y` is the center of the unit's frame. The inside of a unit is always placed automatically (`placement` on a unit is ignored with a warning).

**Taking coordinates from a drawing**: measure the center of each symbol on the drawing image. If the image is a different size from the screen area, either convert the numbers yourself, or keep the image's numbers and set `base-size` to the image size.

### 20.3 Size and `base-size`

- **Without `base-size`**: coordinates are used as they are (scale 1). The diagram's natural size is from the origin to the far edge of the content, plus a 12px margin. If the area is smaller, it overflows and a "Not enough space" warning is left.
- **With `base-size="W H"`**: the natural size is W×H. In an area of another size, the scale `s = min(area width / W, area height / H)` is applied **to the coordinates only**, and the W·s × H·s box is centered in the area. A larger area spreads the equipment apart; a smaller one brings it closer. Equipment, pipes and text keep their size.
- Best practice: take `base-size` equal to the real size of the screen area, so the scale stays near 1 and the screen looks like the drawing. When the area is much smaller, equipment can end up touching — the overlap and "no free path" warnings tell you.

### 20.4 Straightening small offsets (snap)

Equipment has its ports at different heights — a valve's inlet is below its symbol center, a terminal's is at its center. Coordinates measured from a drawing therefore produce small jogs of a few px. To avoid this, when two connected fixed ports face each other (right ↔ left, or bottom ↔ top) and are **16px or less** out of line, the **downstream** (`to`) equipment is moved so the line is straight. The adjustment follows chains (A → B → C stay in line), and each piece of equipment follows only its first such upstream line. Face ports (vessel `top`·`side` …) are not snapped; the router picks their position on the face.

So the coordinates you wrote may move by up to 16px. A larger offset is kept as written and gets a bend.

### 20.5 Checking

Render to PNG and check Trace, as in chapter 15. In coordinate placement, also watch for:

- `'A' and 'B' overlap` — move one of them.
- `no free path` — two pieces of equipment are too close for a pipe to leave one and enter the other (each pipe needs about 18px straight out of each port). Move them apart.
- Crossings — move equipment; there are no `row` hints in this mode.
