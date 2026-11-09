# Devinno.UI process diagram layout

Covers: diagram size and `fit`, text size and the sizing procedure; how automatic placement and pipe routes are computed and the hints that steer them; coordinate placement (`placement="fixed"`: coordinates, port offsets, snap, instrument coordinates, `via`, checks, laying out a complex P&ID); and the table of process Trace messages.
Read first: [process diagrams at a glance](dvml-process.md#process-overview) · [choose the placement mode](dvml-process.md#placement-mode)
Not here: [`DvProcess` attributes](dvml-process.md#dvprocess-attributes) · [equipment sizes and ports](dvml-process-equipment.md#equipment) · [copying a given drawing step by step](dvml-process-examples.md#copy-drawing) · [build diagnostics DVML001–021](dvml-diagnostics.md#build-diagnostics) · [general verification (build → PNG → app)](llm-rules.md#verify) · [box sizes, `grow` and flex](dvml-layout.md#size)

<a id="size-fit"></a>
## Size and `fit`

**How the drawing meets the area** — the master table:

| `fit` | Placement | Area smaller than the drawing | Area larger | Text |
|---|---|---|---|---|
| `none` (default) | both | Automatic: spacing shrinks down to 0.5×, then the diagram overflows (`Not enough space`). Fixed: overflows | Automatic: equipment spreads out. Fixed: the drawing stays at the top left (with `base-size` alone: positions spread) | keeps its size |
| `scale-down` | both (meant for automatic) | First everything `none` does; if it overflows after that, the whole drawing is scaled down to fit, centered | same as `none` | shrinks with the drawing |
| `contain` | `fixed` only (automatic: acts as `scale-down`, with a Trace warning) | The `base-size` picture is scaled down as a whole, centered | The picture is scaled **up** as a whole, centered | follows the drawing both ways |

An unknown `fit` value gives `none` + a Trace warning.

**Automatic placement**

- If no size is given, the diagram's size = the content size (the placement result + a 12px outer margin).
- The placement and the pipe routes are decided **once, at the natural size** (spacing scale 1; with `align="auto"`, the staircase). If a size is given or the parent determines the size, that same drawing is **stretched or shrunk to the area**: only positions move — equipment, text and pipes keep their size — and each axis is scaled on its own (a wider area spreads equipment sideways, a taller one downward). Bends, branch points and the side a pipe passes equipment on do not change with the area size. Equipment joined by a straight pipe moves together, so straight runs stay straight.
- Shrinking has a limit: bubbles, labels and pipe spacing do not shrink, so below a certain size the natural-size shape stops fitting (a segment would fold back, a pipe would run into equipment, or pipes would touch). Only then is placement computed again with smaller spacing and the other alignment, and the **first one that fits** is used: with `align="auto"` (default), staircase with spacing scale 1 → 0.85 → 0.7 → 0.6 → 0.5, then same-row with 1 → … → 0.5; with `align="step"`·`"row"`, only that mode. The drawing then **looks different from the natural size**, and the Trace warning `The piping no longer fits the diagram's natural-size shape …` is left.
- If nothing fits, the natural-size shape is kept at its smallest and overflows, with the Trace warning `Not enough space`. Equipment and text do not shrink.
- **`fit="scale-down"`** changes only that last case: the whole drawing (equipment, text, lines, attached boxes, instruments) is scaled down to the area, keeping its proportions, and centered. A diagram that fits is not touched, and nothing is ever enlarged. The result is reported as the Trace warning `Shrunk to N% … the smallest diagram text is now Xpx.`
- Do not add `fit="scale-down"` until a render has shown `Not enough space` ([sizing procedure](#sizing-procedure)).

**Giving the diagram an area**

- **To fill the remaining space**, use `grow="1" style="height:0"` inside a vertical layout (same meaning as CSS `flex-basis:0` — grows from a base height of 0), and `grow="1"` inside a horizontal layout ([sizes](dvml-layout.md#size)).
- With only `grow="1"` and no `height:0`, the diagram does not shrink below its content height and may overflow the screen, and `fit` does nothing (the diagram is not given a smaller area).
- `fit` only acts on the area the diagram is **given**: use `grow="1" style="height:0"` inside a page frame or card. Directly under `DvScreen` (not a flex container) `grow` does nothing, the diagram keeps its natural size and `fit` has nothing to do.

**Coordinate placement (`base-size`)**

- **Always use `base-size` (the size the coordinates were taken at) + `fit="contain"`** to show a drawing.
- **With `base-size="W H"` and `fit="contain"`**: the drawing is laid out exactly at the coordinates, as a W×H picture, and that picture is scaled **as a whole** — equipment, text, lines, instruments and attached boxes — to the area: `s = min(area width / W, area height / H)`, smaller or larger, and centered on the axis with room left over (a wide area leaves equal margins left and right; a tall one, above and below). Equipment positions and pipe routes are identical at every size, like an image; only the scale changes. When it is drawn smaller, the `Shrunk to N% … Xpx` warning reports the text size ([sizing procedure](#sizing-procedure), step 5); larger needs no warning. Without `base-size`, the content extent (origin to the far edge of the content, plus the 12px margin) is the picture. Equipment placed beyond `base-size` is drawn outside the picture and warned (`Equipment reaches … outside base-size`).
- **Without `base-size`**: coordinates are used as they are (scale 1). The natural size is from the origin to the far edge of the content, plus a 12px margin. If the area is smaller, it overflows with a `Not enough space` warning — or, with `fit="scale-down"`, the whole drawing is scaled down to the area.
- **With `base-size="W H"` only (no `fit`)**: the natural size is W×H. In an area of another size, the scale `s = min(area width / W, area height / H)` is applied **to the coordinates only**, and the W·s × H·s box is centered in the area. A larger area spreads the equipment apart; a smaller one brings it closer. Equipment, pipes and text keep their size, so in a much smaller area equipment can touch.
- **Pipe routes are decided once, at base-size.** At other sizes they are not routed again but stretched with the equipment: bends keep their number, order and side, and only their lengths change. Without `fit="contain"` that shape can stop fitting (a segment would fold back, a pipe would run into equipment, or pipes would touch): in a much smaller area, and in a dense drawing at about 1.4 times base-size or more. The pipes are then routed again for that size — they may look different — and the Trace warning `The piping no longer fits its base-size shape …` is left. `fit="contain"` never does this.
- Without `fit`, take `base-size` equal to the **smallest** real size of the screen area, so the scale stays at 1 or above; in a much smaller area equipment can end up touching — the overlap and `no free path` warnings tell you. If the shape has to stay the same at every size, use `fit="contain"`.

**In every mode**

- The scaled drawing behaves like the real one: presses, drags (knobs, sliders, custom widgets in `DvAttach`), editing and flow animation all follow the drawn positions.
- Zooming in on an automatically placed diagram is not supported ([not supported](dvml-process.md#process-not-supported)).

<a id="text-size"></a>
## Text size

- All text the diagram draws itself — equipment labels (13px), vessel name chips (14px), terminal text (12px), instrument `tag` (12px), `number` and `value` (11px), line and signal labels (11px) — and the minimum bubble size (40px) are given at `font-size` 14 (the default). `style="font-size:N"` on `DvProcess` scales all of them by N / 14 (units inside follow it). Sizes written elsewhere on the process pages are at 14.
- Attached boxes (`DvAttach`) grow with it too: each is laid out at its own size and drawn N / 14 times larger, contents included (buttons, values), and it takes that much room beside the equipment. So `font-size` on `DvProcess` is the one setting for every text in the diagram; do not enlarge attached boxes one by one.
- `font-size` is not inherited in Devinno.UI, so only a value set on `DvProcess` itself changes this. Never put the diagram's `font-size` on a parent.
- Use it to keep text readable when the diagram is shrunk, or when the screen is read from a distance.
- When the drawing is shrunk, `Shrunk to N% … the smallest diagram text is now Xpx.` tells you the smallest text on screen (attached boxes included). Text below 10px is not readable: set `style="font-size:F"` on `DvProcess`, F = current × 11 / X ([sizing procedure](#sizing-procedure), step 5).
- **After raising `font-size`, check the warnings at the base size again.** Bigger bubbles, labels and value text take more room and can produce a value text over a pipe or over another bubble, or a signal line with no free path. Move the equipment involved apart (coordinate placement) and render again.
- The size of a single bubble or its `value` text cannot be set apart from the rest.

<a id="sizing-procedure"></a>
## Sizing procedure — do exactly this, in order

1. Give the diagram its area: `grow="1" style="height:0"`. Do not put `font-size` yet. Automatic placement: no `fit` yet. Coordinate placement (`placement="fixed"`): always `base-size` + `fit="contain"` from the start, then go to step 2 and read step 5 for the `Shrunk to …` warning.
2. Render a PNG at the **smallest size the screen is really shown at** (the panel resolution, or the smallest window), and read the Trace warnings ([verify with a PNG](llm-rules.md#verify-png)).
3. No size warning → done.
4. `Not enough space` → add `fit="scale-down"` and render again.
5. `Shrunk to N% … the smallest diagram text is now Xpx.`:
   - X ≥ 10 → done.
   - X < 10 → set `style="font-size:F"` on `DvProcess` with F = (current font-size, 14 if none) × 11 / X, rounded up, and render again. (11, not 10: equipment symbols do not grow with the text, so the result lands a little below the aim.) Repeat step 5 at most twice.
   - X < 10 after that, or F would go above 28 → the diagram has too much for this screen: split it over several screens or leave out minor equipment (inlet valves, etc.). Do not keep raising `font-size`.
6. Judge by the warning, not by the picture: a shrunk diagram always looks complete in the PNG.

The minimum readable size is always 10px. Do not use another number.

```xml
<DvPageFrame title="Water treatment">
  <DvCard title="Flow diagram" grow="1">
    <DvProcess look="rich" grow="1" style="height:0">
      <DvVessel name="T1" level="40" label="T-101"/>
      <DvPump name="P1" running="true" label="P-101"/>
      <DvLine from="T1.bottom" to="P1.in"/>
    </DvProcess>
  </DvCard>
</DvPageFrame>
```

After steps 4–5 (the render said `Not enough space`, then `… text is now 7.9px.` → F = 14 × 11 / 7.9 = 19.5 → 20):

```xml
<DvProcess look="rich" grow="1" fit="scale-down" style="height:0; font-size:20">
  <DvVessel name="T1" level="40" label="T-101"/>
  <DvPump name="P1" running="true" label="P-101"/>
  <DvLine from="T1.bottom" to="P1.in"/>
</DvProcess>
```

<a id="auto-layout"></a>
## How automatic placement works

The order is written down so an AI can predict the result and choose hints. The same markup **always gives the same result** (no randomness), and changing one hint changes the result predictably.

1. **Break cycles** — lines that loop back, such as recirculation, are reversed only for the layout calculation (the drawn direction stays). Loop-back lines take part in the in-column ordering (step 4) but are not used for alignment (step 5) — equipment positions are set only by flow-direction lines, and the recirculation pipe then routes around the equipment (so that in a top → bottom process the pump is not pushed beside the tank and the recirculation does not cross the inlet pipe).
2. **Assign columns** (a column in `lr`, a layer in `tb`) — each equipment's column = length of its longest upstream path (0 if it has no predecessor). Equipment with no predecessor (terminals, etc.) is pulled to the column just ahead of its successor. A `rank` hint sets the column directly.
3. **Stack parallel equipment** — equipment with the same predecessor set and the same successor set, all of whose used ports are on flow-direction sides (`lr`: equipment using only top/bottom ports; `tb`: equipment using only left/right ports), is placed one step apart along the flow direction, standing on the same row. Two standby pumps stacking vertically in `tb` follow this rule. Parallel pumps with left/right ports in `lr` do not; they go into the same column and are placed one above the other per steps 4–6. Equipment with a `rank` hint is never stacked.
4. **In-column order** — start with declaration order, then repeatedly sort by the average position of neighbors in the previous/next columns, choosing the order with the fewest line crossings. The neighbor position uses **the side of the port the line is attached to** — equipment connected to a top (`tb`: left) side port goes earlier, equipment connected to a bottom (`tb`: right) side port goes later (the `c` branch of a 3-way valve, `in2`·`out2` of a heat exchanger). If crossings remain, adjacent pairs are swapped on trial, and the order is kept only if crossings decrease. A `row` hint is the order key.
5. **Alignment** — each equipment is aligned to the average height (`lr`) / horizontal position (`tb`) of its neighbor ports in the previous column. Equipment with no predecessor (terminals, units) is aligned to its successor, then aligned once more against the previous column. So a series process becomes a straight line, and when several pieces of equipment converge on one (3 units → 1 tank), the receiving equipment comes to the center. Ports on sides perpendicular to the flow are aligned according to `align`:
   - **Incoming (front) side ports** (top ports in `lr`, left ports in `tb`): always aligned to the spot 26px outside in front of the port — a line fed from above comes straight down and bends once to enter.
   - **Outgoing (back) side ports** (bottom ports in `lr`, right ports in `tb`): with `step`, 26px outside in front of the port (the next equipment sits lower than the tank → staircase, 1 bend, taller drawing); with `row`, the equipment's center line (the next equipment's inlet is at the tank's mid height → same row, pipe bends in a U shape with 3 bends, shorter and wider drawing). `auto` uses staircase; same row only when the staircase drawing cannot be shrunk into the area ([size and `fit`](#size-fit)). Unit interiors use the same method.
6. **Spacing** — between equipment in the same column = 40px × spacing scale (minimum 28px). Corridor between columns = 48px × spacing scale (minimum 44px) + number of pipe bundles passing through that corridor (lines connected by branches/merges form one bundle) × pipe spacing (plain 12px, rich 16px). Column width = the widest footprint in that column; equipment is centered in the column.
7. **Footprint** — the size one piece of equipment occupies = the rectangle enclosing symbol + accessories + label + attached boxes. So equipment with a large attached box widens its column. Instruments go into their equipment's footprint ([`DvInstrument`](dvml-process.md#dvinstrument)).

- Parallel equipment (standby pumps with the same upstream and downstream) shares an inlet header and an outlet header.
- Units are placed inside first, then as one piece of equipment ([units](dvml-process.md#units)).
- When only values change, the layout is not recomputed (the same structure reuses the previous layout), so updating several times per second is cheap ([updates and cost](dvml-process.md#process-vm)).

<a id="pipe-routing"></a>
## How pipe routes are computed

These rules apply in both placement modes (in coordinate placement, unless a `via` gives the route).

1. Pipes leave the port **18px straight** outward (the port's lead-out). No bend directly in front of a port. When two ports face each other closer than two lead-outs (36px — only possible with coordinate placement), both lead-outs are shortened to half the gap, so the pipe runs straight (or with one small step if the ports are offset).
2. Routes are searched along an 8px grid (+ port coordinates). Cost = length + bend (30) + crossing another pipe (40). Following an already-drawn trunk of the same bundle costs 1/5, so branches and merges share a trunk.
3. Routes do not enter within 10px of an equipment symbol or an instrument bubble, or within 4px of labels and attached boxes, and do not cross an instrument's `tap` connection line.
4. Pipes do not overlap pipes of other bundles, do not run closer than the pipe spacing when parallel, and do not bend on top of another pipe (it would look like they touch).
5. If there is no route, these constraints are turned into costs and the search is repeated (overlap allowed), leaving the Trace warning `no free path, so it overlaps other piping`.
6. Bundles are drawn in declaration order (lines written first get the good routes first).
7. At crossings with other bundles a semicircular jump is drawn: in `lr` the vertical line jumps, in `tb` the horizontal line jumps (radius plain 7, rich 13).
8. Pipe ends tuck slightly under the equipment body (fixed ports 6px, side ports 24px) and are hidden, so they appear attached without a gap even on the top of a rounded vessel.

- Return lines (recirculation) are drawn: the layout aligns using only flow-direction lines, and the recirculation is routed around the equipment.

<a id="hints"></a>
## Refining with hints (`row`·`rank`·`align`·`rotate`·`flip`)

Automatic placement has no bend-point attributes; the shape changes only through these hints. Use them only when needed **after viewing the result as a PNG**. If it came out clean, do not use them. Line crossings and blocked paths are reported as Trace warnings: first change the order with `row`, then the column with `rank`.

| Hint | On | Value | Effect | When |
|---|---|---|---|---|
| `row` | equipment, unit | integer (from 0) | Order key within the same column. Smaller = top (`lr`) / left (`tb`) | When lines cross, or to set the top/bottom order |
| `rank` | equipment, unit | integer ≥ 0 | Fixes that equipment's column (columns count from 0) | To push a piece of equipment to a column further right (down) |
| `align` | `DvProcess` | `auto`·`step`·`row` | Equipment after a tank-bottom outlet as staircase / same row | Only when you do not like the shape `auto` chose ([automatic placement](#auto-layout), step 5) |
| `rotate` | equipment (not terminals) | `0`·`90`·`180`·`270` | Rotates equipment clockwise; port sides rotate too | Valve on a vertical pipe (`90`: enters from the top, leaves at the bottom) |
| `flip` | equipment | `h`·`v`·`hv` | Flip (horizontal / vertical / both) | Pump with reversed flow (`h`) |

`row` and `rank` that are not numbers are ignored with a warning; with `placement="fixed"`, `row`·`rank`·`align` are ignored with a Trace warning.

Caveats:

- The order among equipment that has a `row` hint is always respected: the crossing reduction never swaps two hinted pieces, and each column is sorted by `row` at the end.
- It is safest to give `row` **to all equipment in the same column**. If only some have it, it mixes with the order key (current index) of equipment without hints and may differ from what you expect.
- `rank` is a **last resort**. Forcing a column can break flow order, making lines run backward or cross. Other equipment's columns are computed automatically, so it is hard to predict. Always re-render to check after using it. One case where it is the right tool: a control valve on a utility line (steam, cooling water) feeding equipment far down the process. Its upstream path is short, so it lands in an early column ([automatic placement](#auto-layout), step 2) and its pipe runs across the screen; `rank` = the fed equipment's column − 1 puts it next to that equipment and keeps the flow order.
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

<a id="fixed-placement"></a>
## Coordinate placement (`placement="fixed"`)

When to use it: [choose the placement mode](dvml-process.md#placement-mode). Copying a given drawing step by step: [copying a given drawing](dvml-process-examples.md#copy-drawing).

- **One mode per diagram.** A `DvProcess` is either automatic or coordinate-placed. In a `fixed` diagram, every piece of equipment (and every unit) has `x`·`y`; there is no mixing.
- **Equipment gets coordinates; instruments and pipes can.** Pipes, branches, merges, crossing jumps and signal lines are routed automatically, exactly as in [connections](dvml-process.md#connections) and [pipe routing](#pipe-routing), and moving a piece of equipment moves its pipes with it. Instruments without coordinates sit beside their equipment. To match a drawing further, give a bubble `x`·`y` ([instrument coordinates](#instrument-coordinates)) and a pipe [`via`](#via).
- **Without `fit`, text and equipment keep their size and only the distances change with the area. With `base-size` + `fit="contain"` (recommended) the whole drawing scales like an image** ([size and `fit`](#size-fit)).

```xml
<DvProcess placement="fixed" base-size="1000 560" fit="contain" look="rich">
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

| Attribute | On | Meaning |
|---|---|---|
| `placement="fixed"` | `DvProcess` | Turns on coordinate placement |
| `x` · `y` | each piece of equipment, each unit | The **center of the symbol** in px (labels, attached boxes, instruments and accessories such as an agitator are not counted). Bindable |
| `base-size="W H"` | `DvProcess` | Optional. The size the coordinates were taken at ([size and `fit`](#size-fit)) |

- The origin `(0, 0)` is the top-left of the inside of the `DvProcess` (inside its `padding`).
- Labels, `DvAttach` boxes and accessories go where they would in automatic placement, relative to their equipment. So do instruments, unless they have their own `x`·`y`.
- To match the drawing's nozzles and instruments, connect at face positions (`to="T1.left@0.15"`, [face position](dvml-process.md#face-position)), give instruments that are joined to the process `tap` (`tap="T1.left@0.85"`, `tap="CV1.in"`, [`DvInstrument`](dvml-process.md#dvinstrument)) and give bubbles `x`·`y`.
- In coordinate placement, `direction` matters too (terminal shape, which faces are "front" and "back"). `row`, `rank` and `align` are ignored with a Trace warning.
- Equipment without both `x` and `y` is **not drawn** (Trace error).
- A unit (`DvUnit` or a unit component) is placed as a whole: `x`·`y` is the center of the unit's frame. The inside of a unit is always placed automatically (`placement` on a unit is ignored with a warning).
- **Taking coordinates from a drawing**: measure the center of each symbol on the drawing image. If the image is a different size from the screen area, either convert the numbers yourself, or keep the image's numbers and set `base-size` to the image size.

<a id="port-offsets"></a>
### Where the pipe runs relative to `x`·`y`

`x`·`y` is the symbol center, but a port is not always on the center line. Height of the `in`·`out` port line from the center, in px (+ = below); everything not listed is 0:

| Equipment | Port line |
|---|---|
| Straight `DvValve` kinds, `flowmeter` | +8 |
| `strainer` | −8 |
| `plate` exchanger | −16 |
| `centrifugal` pump | `in` +4, `out` −12 |
| `blower` | `in` +4, `out` −13 |
| `angle` valve | `out` −4; `in` (bottom) is 4px left of center |
| `relief` valve | `out` +4; `in` (bottom) is 4px left of center |

So a valve on a pipe at y=300 has `y="292"`. `rotate` turns this with the symbol: a valve on a vertical pipe at x=400 has `x="408"` with `rotate="90"` and `x="392"` with `rotate="270"`. An offset of up to 16px is straightened anyway, by moving the equipment ([snap](#snap)).

<a id="snap"></a>
### Straightening small offsets (snap)

Equipment has its ports at different heights — a valve's inlet is below its symbol center, a terminal's is at its center. Coordinates measured from a drawing therefore produce small jogs of a few px. To avoid this, when two connected fixed ports face each other (right ↔ left, or bottom ↔ top) and are **16px or less** out of line, the **downstream** (`to`) equipment is moved so the line is straight. The adjustment follows chains (A → B → C stay in line), and each piece of equipment follows only its first such upstream line. Face ports (vessel `top`·`side` …) are not snapped; the router picks their position on the face.

So the coordinates you wrote may move by up to 16px. A larger offset is kept as written and gets a bend.

<a id="instrument-coordinates"></a>
### Instrument coordinates

In a `fixed` diagram, `x`·`y` on a `DvInstrument` is the bubble center, in the same space as equipment coordinates (scaled the same way by `base-size`). Read it straight off the drawing. Instruments without `x`·`y` stay beside their equipment, in the same diagram.

```xml
<DvProcess placement="fixed" base-size="900 420" fit="contain">
  <DvTerminal name="IN" text="FEED" x="80" y="300"/>
  <DvInline name="FE1" kind="orifice" x="240" y="300"/>
  <DvValve name="FV1" kind="control" open="true" label="FV-1" x="420" y="292"/>
  <DvTerminal name="OUT" text="PRODUCT" x="780" y="300"/>
  <DvLine from="IN" to="FE1.in"/>
  <DvLine from="FE1.out" to="FV1.in"/>
  <DvLine from="FV1.out" to="OUT"/>

  <!-- joined to the process: tap + x·y. Only a signal line: on + x·y -->
  <DvInstrument name="FT1" tag="FT" number="101" tap="FE1.top@0.5" x="240" y="150"/>
  <DvInstrument name="TT1" tag="TT" number="102" tap="FV1.out" x="600" y="200"/>
  <DvInstrument name="FIC1" tag="FIC" number="101" on="FV1" panel="true" x="420" y="60"/>
  <DvSignal from="FT1" to="FIC1"/>
  <DvSignal from="FIC1" to="FV1"/>
</DvProcess>
```

- **`on` or `tap` is required** with `x`·`y` too. It names the equipment the instrument belongs to; the bubble does not have to be near it.
- The bubble does not widen its equipment's footprint and does not move when [snap](#snap) moves the equipment.
- Pipes go around the bubble, and a bubble over a piece of equipment or another free bubble gets the `overlap` warning. Signal lines end on the bubble as usual.
- The `value` text stands to the right of the bubble, and signal lines do not leave on that face, so a signal to a counterpart straight to the right goes around. Where that matters, put the counterpart above or below, or show the value on another bubble.
- **With `tap`** the connection line runs from the tap point to the bubble:
  - a fixed port (`tap="FV1.out"`) means **that pipe**: the line drops straight onto the nearest part of it, anywhere along its length. If no part of the pipe is level with the bubble, it goes to the spot 12px out of the port, and may be diagonal.
  - a face position (`tap="FE1.top@0.5"`) is that point, slid along the face to line up with the bubble when it is within 16px, so small offsets stay straight. Further off, the line is diagonal: line the bubble up with the point.
  - Other pipes avoid the bubble but not this line, so keep it clear of them.
- Only one of `x`·`y`, or a value that is not a number: Trace warning, and the bubble goes beside its equipment. In automatic placement and inside units they are ignored with a Trace warning.

<a id="via"></a>
### Pipe bend points (`via`)

`via` on a `DvLine` is the route, written as the points it runs through, in the same space as equipment coordinates. It exists only with `placement="fixed"`; write it only for a pipe that runs differently from the drawing.

```xml
<DvProcess placement="fixed" base-size="800 360" fit="contain">
  <DvTerminal name="IN" text="FEED" x="80" y="300"/>
  <DvPump name="P1" running="true" label="P-101" x="420" y="100"/>
  <DvTerminal name="OUT" text="OUT" x="700" y="88"/>
  <DvLine from="IN" to="P1.in" via="230,300 230,104"/>
  <DvLine from="P1.out" to="OUT"/>
</DvProcess>
```

- The pipe leaves its port, runs through the points in order and enters the other port. Everything else about it (color, flow, arrows, `label`, jumps at crossings) is the same as a routed pipe. Other pipes are routed after it and keep clear of it.
- **Use it only where the automatic route differs from the drawing.** Moving equipment does not move the points, so a `via` has to be rewritten when its equipment moves.
- You do not need the exact height of a port: a first or last point within 16px of the port's line is moved onto it, together with its neighbors on the same line.
- Points that are not on one line are joined by a right-angle elbow, so `via="230,200"` alone is a valid route. Pipes stay orthogonal.
- It is drawn as written: a route through equipment or a bubble is reported (`via runs through`), and overlapping another pipe is not prevented.
- With `base-size`, the points scale like the coordinates. Bindable (`via="{Route}"`).
- A typo, or `via` in automatic placement or inside a unit, gives a Trace warning and the pipe is routed automatically. At build time a bad format is [DVML011](dvml-diagnostics.md#dvml011).
- Signal lines (`DvSignal`) have no `via`.

<a id="fixed-check"></a>
### Checking a coordinate-placed diagram

Render to PNG and check Trace ([process diagnostics](#process-diagnostics)). In coordinate placement, also watch for:

- `'A' and 'B' overlap` — move one of them. The body, the label and attached boxes all count.
- `no free path` — two pieces of equipment are too close for a pipe to leave one and enter the other. Keep at least 20px between ports facing each other on one line; otherwise each pipe needs about 18px straight out of its port. Move them apart.
- Crossings — move equipment; there are no `row` hints in this mode.

Large drawings: [complex P&IDs](#complex-pid); fitting all screen sizes: [size and `fit`](#size-fit).

<a id="complex-pid"></a>
### Drawing a complex P&ID in coordinate placement

1. **Pick `base-size`** for the shape of the real screen: `1800 980` for a wide (16:9) panel, `1400 1000` for 4:3. Coordinates are px inside that box; `fit="contain"` scales the box to any screen later, so draw it as large as is comfortable.
2. **Place the main equipment first, then build bands around it.** For a distillation unit: the column in the middle; the overhead (condenser, reflux drum, reflux pumps) in the top band; the reboiler below the column; the feed train to the left; products to the right (distillate top right, bottoms bottom right). Each band reads left → right in the flow direction.
3. **Spacing**: about 110–160px between neighboring pieces of equipment along a line (labels and instrument bubbles need room; each pipe needs about 18px straight out of a port), about 110px between parallel pumps.
4. **Parallel equipment goes one above the other** (`P-202A` at y 270, `P-202B` at y 380), not side by side — side by side, their outlet pipes cross.
5. **Terminals cannot be turned.** Put receiving terminals downstream (to the right in `lr`, below in `tb`) of the equipment that feeds them, and supplying terminals upstream ([terminal direction](dvml-process-equipment.md#terminal)).
6. **Equipment that must flow right → left** (a reflux valve returning to the column, a reboiler fed from its right side) is mirrored with `flip="h"`: its `in` is then on the right.
7. **Several lines on one face of a vessel**: do not connect them all to `side` (lines on the same face in the same direction merge into one nozzle). Give each its own point with a [face position](dvml-process.md#face-position): feed `T201.left@0.5`, reflux return `T201.right@0.3`, reboiler return `T201.left@0.9`, relief `T201.right@0.1`.
8. **Instruments**: `on` (`on="FV101"`) puts the bubble on the least busy face automatically — fine for valves and flowmeters. Where the position means something (a level transmitter at the column base, a temperature at a tray, a temperature on a product line), use `tap`: `tap="T201.right@0.82"`, `tap="E203.out"`. A bubble placed with `on` can also sit in front of a nozzle and block a pipe (a `no free path` on that equipment's line) — `tap` fixes that too. When it has to be somewhere else entirely, give it `x`·`y` ([instrument coordinates](#instrument-coordinates)).
9. **Value text** (`value`) is not part of the layout. Keep it short (`96.4 °C`, `38.2 m³/h`) and watch for `'X' value text overlaps …`.
10. **Render at the base size** and fix every `overlap` and `no free path` by moving equipment. One or two crossings are normal on a real P&ID and are drawn with jumps. A pipe that runs where you do not want it (around the wrong side, or across a busy area) gets its route with [`via`](#via). Then follow the [sizing procedure](#sizing-procedure) at the smallest real screen size.

The full distillation example: [complete examples](dvml-process-examples.md#process-examples).

<a id="process-diagnostics"></a>
## Process diagnostics (Trace)

Process diagram problems are reported through `System.Diagnostics.Trace`. Keep a Trace listener on during development (console: `Trace.Listeners.Add(new ConsoleTraceListener())`). The same message appears only once per screen (`DvSurface`); another screen, or the same screen after its markup is reloaded, reports it again ([Trace warnings](dvml-diagnostics.md#trace-warnings)). Layout quality warnings are based on **the layout actually drawn**.

On screen, invalid connections show as a **red dashed line** between the two pieces of equipment, and equipment with a wrong port shows a **red border**; everything else is drawn normally.

Messages are prefixed with `Devinno.UI process:`; value-rule warnings (unknown words, colors) are prefixed with `Devinno.UI:`. Mode: auto = automatic placement only, fixed = coordinate placement only, both = either.

**Layout quality and size**

| Message | Kind | Mode | Meaning | Fix |
|---|---|---|---|---|
| `'A.out'→'B.in': crosses other piping N time(s)` | Warning | both | Crossings (drawn with jumps) | Auto: change the order of equipment in the same column with `row`; declare in flow order. Fixed: move equipment |
| `'A.out'→'B.in': no free path, so it overlaps other piping` | Warning | both | Blocked, drawn overlapping | Enlarge the area, adjust `row`·`rank` (auto) or move equipment apart (fixed), move an attached box standing in front of a port |
| `Not enough space: needs W×H, area is W×H` | Warning | auto | Overflows even with minimum spacing (the natural-size shape is kept and overflows) | [Sizing procedure](#sizing-procedure), step 4: `fit="scale-down"` |
| `Shrunk to N% to fit (needs W×H, area is W×H); the smallest diagram text is now Xpx. …` | Warning | both | `fit="scale-down"` or `contain` drew the diagram smaller as a whole | [Sizing procedure](#sizing-procedure), step 5 (X ≥ 10 is fine; otherwise `font-size` × 11 / X, or split the screen). X counts attached boxes too. Judge by X, not by the PNG |
| `The piping no longer fits the diagram's natural-size shape in this area, so it is laid out again …` | Warning | auto | The area is too small for the natural-size drawing to shrink into, so alignment and spacing were chosen again and the drawing looks different | Enlarge the area if the shape must stay the same |
| `'X' value text overlaps 'Y'` | Warning | both | An instrument `value` covers other equipment, a label, another instrument or a pipe (the value is not part of the layout) | Shorten the value, move the bubble or the pipe, or show the number on another bubble or with `DvAttach` |
| `'T': not enough room on the top face for N nozzles (incoming and outgoing lines) …` | Warning | both | Agitator / attached boxes cover most of the face, so two nozzles are too close | Move the incoming or outgoing line to `side`, or move the attached box |
| `Signal line '…': no free path, so it is drawn across equipment` | Warning | both | No route for the signal line | Attach the instrument to other equipment, or enlarge the area |

**Connections and names**

| Message | Kind | Mode | Meaning | Fix |
|---|---|---|---|---|
| `DvLine '…': no equipment named 'X'` | Error | both | Name typo, or equipment hidden with `visible` | Check the name |
| `DvLine '…': 'X' has no port 'p' (…)` | Error | both | Port name typo | Use a name from the parentheses |
| `DvLine '…': 'X' requires a port (…)` | Error | both | Port omitted, as in `from="X"` | Use `X.in`, etc. |
| `DvLine '…': an endpoint (DvTerminal) is written without a port, …` | Warning | both | `.port` appended to a terminal | Name only |
| `'…' is not a face (top·bottom·left·right)` · `'@…' is not a position` · `position … is outside 0–1` | Error (last: Warning, clamped) | both | Face position typo in `DvLine` `from`/`to` or `tap` | `left@0.85` form |
| `<Tag> is not process equipment (…)` | Error | both | Non-equipment tag directly under `DvProcess`, or an unregistered symbol (not drawn) | Move it into `DvAttach`, or register the symbol |
| `<X> has no name` | Error | both | `name` missing | Add `name` |
| `Equipment name 'X' is used twice` | Error | both | Duplicate name | Use a different name |
| `<Y> is not allowed inside <X> (…)` | Error | both | Tag other than `DvAttach`·`DvAgitator`·`DvMotor` inside equipment | Wrap it in `DvAttach` |

**Units**

| Message | Kind | Mode | Meaning | Fix |
|---|---|---|---|---|
| `DvLine '…': equipment inside a unit cannot be connected directly from outside …` | Error | both | Connecting into a unit directly from outside, as in `U1.T.top` | Put a `DvPort` on the unit, connect to the port from inside, and from outside use `U1.port` |
| `DvLine '…': a unit is connected only at its ports (DvPort), not at a face position ('…')` | Error | both | A face position on a unit | Connect at a `DvPort` |
| `DvLine '…': 'U1' has no port 'p' (…)` | Error | both | Port name not on the unit | Use a name in the parentheses (that unit's `DvPort` names) |
| `DvLine '…': a unit port (DvPort) is written by name only …` | Warning | both | A dot appended to a port inside the unit, as in `out.x` | Name only |
| `<DvPort> can only be used inside <DvUnit>` | Error | both | `DvPort` directly under `DvProcess` | Move it into a unit |
| `<DvPort name='…'> side must be one of …` · `at='…' must be between 0 and 1 …` | Error | both | Port side / position error | Always give `side`; `at` within 0–1 |
| `<X>: a unit cannot be placed inside another unit` | Error | both | Nested units | Move it outside |

**Instruments and signal lines**

| Message | Kind | Mode | Meaning | Fix |
|---|---|---|---|---|
| `<DvInstrument name='X'> has no on (name of the equipment to attach to)` · `on='Y': no such equipment` | Error | both | No equipment for the instrument to attach to (not drawn) | Put the name of equipment in the same diagram in `on` |
| `<DvInstrument name='X'> tap='…': …` | Error | both | `tap` target missing, a terminal or unit, no port given, an unknown port, or a face port without a position (`T1.side`) — not drawn | `equipment.face@position` or a fixed port such as `CV1.in` |
| `<DvInstrument name='X'> on='Y' and tap='…' name different equipment; using the tap equipment 'Z'` | Warning | both | `on` and `tap` disagree | Remove `on`, or make it name the `tap` equipment |
| `<DvInstrument> has no name` · `Name 'X' is used twice (equipment and instrument names must be unique …)` | Error | both | Instrument name missing, or clashing with equipment or another instrument | A unique `name` |
| `DvSignal '…': no instrument or equipment named 'X'` | Error | both | Signal line end name typo, a terminal / unit port, or an instrument that is not drawn | Check instrument / equipment names |
| `DvSignal '…': signal lines use names only, without ports` | Error | both | Port appended, as in `to="V1.in"` | Name only |
| `DvSignal '…' thickness='…' must be a number greater than 0` | Warning | both | Value typo (default drawn) | A number greater than 0 |

**Values**

| Message | Kind | Mode | Meaning | Fix |
|---|---|---|---|---|
| `Devinno.UI: <Tag> <attr>='v' is not one of a, b; using <default>` | Warning | both | Unknown word for `kind`, `align`, `rotate`, `direction`, `look`, `placement`, `fit`, `arrow`, `medium`, `side`, `DvSignal kind` … | Use a value from the list in the message |
| `Devinno.UI: DvSignal color='…' is not a color (#RRGGBB or token(name)); …` | Warning | both | Not a color (default drawn) | `#RRGGBB` or `token(name)` |
| `<DvVessel width='…'> is outside A–B for kind 'k'; using C` (also `height`) | Warning | both | Vessel size outside 0.6–4 × the kind's size, clamped | A size within the range |

**Coordinate placement**

| Message | Kind | Mode | Meaning | Fix |
|---|---|---|---|---|
| `<X name='N'>: placement="fixed" needs both x and y …; not drawn` | Error | fixed | Equipment without (or with an invalid) `x`/`y`. Lines to it then also report "no equipment named" | Give both `x` and `y` |
| `'A' and 'B' overlap; move one of them (x/y) or enlarge base-size` | Warning | fixed | Two pieces of equipment (symbol, label, attached boxes, instruments) overlap | Move one, or check `base-size` against the area |
| `Not enough space: equipment reaches W×H, area is W×H …` | Warning | fixed | Equipment goes past the area | Use `base-size` + `fit="contain"` ([size and `fit`](#size-fit)) |
| `Equipment reaches W×H, outside base-size W×H …` | Warning | fixed | `fit="contain"`: equipment is placed beyond `base-size`, so it sticks out of the scaled picture | Move equipment (`x`/`y`) or enlarge `base-size` |
| `The piping no longer fits its base-size shape at this size, so it is routed again …` | Warning | fixed | Without `fit="contain"`: the pipes were routed again for this area and may look different from base-size | `fit="contain"` keeps the shape at every size |
| `<DvInstrument name='X'>: needs both x and y` | Warning | fixed | Only one of them, or not a number (placed beside its equipment) | Give both |
| `DvLine '…' via='…': write points as x,y separated by spaces` | Warning | fixed | `via` typo (routed automatically) | `via="230,190 230,74"` |
| `'A'→'B': via runs through 'X'` | Warning | fixed | The written route crosses a piece of equipment or a bubble (drawn as written) | Change the points, or move `X` |
| `via is used only with <DvProcess placement="fixed">` | Warning | auto | `via` in automatic placement (routed automatically) | Remove it, or switch the mode |
| `<X name='N'>: x/y are used only with <DvProcess placement="fixed">` | Warning | auto | Coordinates in automatic placement (ignored) | Remove them, or switch the mode |
| `row`/`rank`/`align … is not used with placement="fixed"` · `base-size …` | Warning | both | An attribute for the other mode, or a `base-size` typo | Remove it, or switch the mode |

**Build-time checks**: if the process diagram is in a `*.dvml` file, the source generator reports connection errors at build time as diagnostics on that dvml line — [DVML008](dvml-diagnostics.md#dvml008) (name not found: a `from`/`to`/`on`/`tap`/signal end naming no equipment, or a `DvInstrument` with neither `on` nor `tap`), [DVML009](dvml-diagnostics.md#dvml009) (port error), [DVML010](dvml-diagnostics.md#dvml010) (duplicate name), [DVML011](dvml-diagnostics.md#dvml011) (invalid word, number, color or `via`, or a `{` in a name; warning). The messages have the same meaning as the tables above. Names and references (`name`, `from`, `to`, `on`, `tap`) are never bound: a `{` in one is DVML011 and the name is checked as written. Layout quality (crossings, overlaps, space) is known only at run time.

**AI check order**: ⓪ build and confirm there are no DVML errors → ① render PNG → ② confirm no errors (Error) in Trace → ③ confirm no warnings (crossings, overlaps, space) → ④ in the drawing, confirm pipes don't pass through equipment, labels / attached boxes don't overlap, and flow direction is correct → ⑤ if there is a problem, [hints](#hints) (automatic) or moving equipment (coordinate) → back to ①. The general steps: [verify](llm-rules.md#verify).

```csharp
using System.Diagnostics;
using Devinno.UI;
using Devinno.UI.Styling;

Trace.Listeners.Add(new ConsoleTraceListener());
DvRenderer.RenderToFile(markup, vm, 1280, 800, "process.png", DvTheme.Dark, registry);
```
