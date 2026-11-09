# Devinno.UI process diagram examples

Covers: complete process diagrams to start from (water treatment left → right, a top → bottom blender, recirculation, a distillation unit in coordinate placement) and the procedure for copying a given drawing with `placement="fixed"`.
Read first: [process diagrams at a glance](dvml-process.md#process-overview) · [choose the placement mode](dvml-process.md#placement-mode) · [authoring procedure](dvml-process.md#procedure)
Not here: [the page + view model pair `WaterPage`](dvml-process.md#process-vm) · [a unit component with `BlendPage`](dvml-process.md#units-components) · [equipment tags and user symbols](dvml-process-equipment.md#equipment) · [coordinate placement details](dvml-process-layout.md#fixed-placement) · [process checklist](dvml-process.md#process-checklist)

<a id="process-examples"></a>
## Complete examples

The page + view model pair is in [view model and updates](dvml-process.md#process-vm) (`WaterPage`) and [repeating a unit](dvml-process.md#units-components) (`BlendPage`).

**Left → right water treatment (parallel pumps, control valve, reactor)**

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

**Top → bottom blender (two inlets, agitator, value display, parallel discharge pumps)**

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

**Recirculation (loop-back line)**

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

**Distillation unit (coordinate placement)**

A distillation unit with a feed train (two pumps, steam preheater, flow control), the column, overhead condenser, reflux drum with pressure control, reflux pumps with reflux flow control and distillate level control, a thermosiphon reboiler with steam temperature control, and bottoms pumps with a cooler and level control. Coordinates at `base-size` 1800×980; `font-size:21` comes from the sizing procedure at a 1280×720 screen (the smallest text: 7.6px → 14 × 11 / 7.6 = 20.3 → 21 (rounded up) → 11.4px).

```xml
<DvProcess look="rich" placement="fixed" base-size="1800 980" fit="contain" grow="1" style="height:0; font-size:21">
  <!-- doc-test: crossings-allowed — the reboiler steam line and the reboiler return cross once, as on real drawings -->
  <DvTerminal name="FEED" text="FEED&#10;FROM TK-100" x="80" y="470"/>
  <DvPump name="P101A" running="true" label="P-101A" x="230" y="400"/>
  <DvPump name="P101B" label="P-101B" x="230" y="540"/>
  <DvExchanger name="E101" label="E-101" x="400" y="470"/>
  <DvTerminal name="STM1" text="LP STEAM" x="270" y="300"/>
  <DvTerminal name="CND1" text="CONDENSATE" x="560" y="555"/>
  <DvInline name="FE101" kind="flowmeter" label="FE-101" x="540" y="470"/>
  <DvValve name="FV101" kind="control" open="true" label="FV-101" x="650" y="470"/>
  <DvVessel name="T201" kind="column" height="380" level="45" label="T-201" x="800" y="470"/>

  <DvValve name="PSV201" kind="relief" label="PSV-201" x="920" y="250"/>
  <DvTerminal name="FLR1" text="TO FLARE" x="1060" y="272"/>
  <DvExchanger name="E201" label="E-201" x="1000" y="110"/>
  <DvTerminal name="CWS1" text="CWS" x="860" y="40"/>
  <DvTerminal name="CWR1" text="CWR" x="1110" y="215"/>
  <DvVessel name="D201" kind="drum" level="55" label="D-201" x="1300" y="120"/>
  <DvValve name="PV201" kind="control" open="true" label="PV-201" x="1490" y="100"/>
  <DvTerminal name="FLR2" text="TO FLARE" x="1640" y="100"/>
  <DvPump name="P202A" running="true" label="P-202A" x="1290" y="270"/>
  <DvPump name="P202B" label="P-202B" x="1290" y="380"/>
  <DvInline name="FE202" kind="flowmeter" label="FE-202" x="1470" y="330">
    <DvAttach side="bottom"><DvLabel class="caption muted" text="REFLUX RATIO"/><DvLabel class="title" text="2.4"/></DvAttach>
  </DvInline>
  <DvValve name="FV202" kind="control" open="true" flip="h" label="FV-202" x="1060" y="450"/>
  <DvValve name="LV201" kind="control" open="true" label="LV-201" x="1590" y="330"/>
  <DvTerminal name="DIST" text="DISTILLATE" x="1720" y="330"/>

  <DvExchanger name="E202" label="E-202" flip="h" x="640" y="810"/>
  <DvValve name="TV201" kind="control" open="true" label="TV-201" x="470" y="725"/>
  <DvTerminal name="STM2" text="LP STEAM" x="330" y="725"/>
  <DvValve name="CV1" kind="check" label="CHK" x="690" y="915"/>
  <DvTerminal name="CND2" text="CONDENSATE" x="840" y="915"/>
  <DvPump name="P203A" running="true" label="P-203A" x="990" y="760"/>
  <DvPump name="P203B" label="P-203B" x="990" y="880"/>
  <DvExchanger name="E203" label="E-203" x="1160" y="820"/>
  <DvTerminal name="CWS2" text="CWS" x="1020" y="700"/>
  <DvTerminal name="CWR2" text="CWR" x="1280" y="915"/>
  <DvValve name="LV202" kind="control" open="true" label="LV-202" x="1370" y="820"/>
  <DvTerminal name="BTM" text="BOTTOMS" x="1510" y="820"/>

  <DvLine from="FEED" to="P101A.in" medium="oil"/>
  <DvLine from="FEED" to="P101B.in"/>
  <DvLine from="P101A.out" to="E101.in"/>
  <DvLine from="P101B.out" to="E101.in"/>
  <DvLine from="STM1" to="E101.in2" medium="steam"/>
  <DvLine from="E101.out2" to="CND1" medium="water"/>
  <DvLine from="E101.out" to="FE101.in"/>
  <DvLine from="FE101.out" to="FV101.in"/>
  <DvLine from="FV101.out" to="T201.left@0.5" arrow="end"/>
  <DvLine from="T201.top" to="E201.in" medium="gas"/>
  <DvLine from="T201.right@0.1" to="PSV201.in"/>
  <DvLine from="PSV201.out" to="FLR1"/>
  <DvLine from="CWS1" to="E201.in2" medium="water"/>
  <DvLine from="E201.out2" to="CWR1"/>
  <DvLine from="E201.out" to="D201.top" medium="oil"/>
  <DvLine from="D201.right@0.3" to="PV201.in" medium="gas"/>
  <DvLine from="PV201.out" to="FLR2"/>
  <DvLine from="D201.bottom" to="P202A.in"/>
  <DvLine from="D201.bottom" to="P202B.in"/>
  <DvLine from="P202A.out" to="FE202.in"/>
  <DvLine from="P202B.out" to="FE202.in"/>
  <DvLine from="FE202.out" to="FV202.in"/>
  <DvLine from="FE202.out" to="LV201.in"/>
  <DvLine from="FV202.out" to="T201.right@0.3" arrow="end"/>
  <DvLine from="LV201.out" to="DIST"/>
  <DvLine from="T201.bottom" to="E202.in"/>
  <DvLine from="E202.out" to="T201.left@0.9" arrow="end"/>
  <DvLine from="STM2" to="TV201.in" medium="steam"/>
  <DvLine from="TV201.out" to="E202.in2"/>
  <DvLine from="E202.out2" to="CV1.in" medium="water"/>
  <DvLine from="CV1.out" to="CND2"/>
  <DvLine from="T201.bottom" to="P203A.in"/>
  <DvLine from="T201.bottom" to="P203B.in"/>
  <DvLine from="P203A.out" to="E203.in"/>
  <DvLine from="P203B.out" to="E203.in"/>
  <DvLine from="CWS2" to="E203.in2" medium="water"/>
  <DvLine from="E203.out2" to="CWR2"/>
  <DvLine from="E203.out" to="LV202.in"/>
  <DvLine from="LV202.out" to="BTM"/>

  <DvInstrument name="FT101" tag="FT" number="101" on="FE101"/>
  <DvInstrument name="FIC101" tag="FIC" number="101" on="FV101" panel="true" value="38.2 m³/h"/>
  <DvInstrument name="PT201" tag="PT" number="201" on="D201"/>
  <DvInstrument name="PIC201" tag="PIC" number="201" on="PV201" panel="true" value="1.8 barg"/>
  <DvInstrument name="TT201" tag="TT" number="201" tap="T201.right@0.62"/>
  <DvInstrument name="TIC201" tag="TIC" number="201" on="TV201" panel="true" value="96.4 °C"/>
  <DvInstrument name="LT201" tag="LT" number="201" on="D201"/>
  <DvInstrument name="LIC201" tag="LIC" number="201" on="LV201" panel="true" value="55 %"/>
  <DvInstrument name="FT202" tag="FT" number="202" on="FE202"/>
  <DvInstrument name="FIC202" tag="FIC" number="202" on="FV202" panel="true" value="21.0 m³/h"/>
  <DvInstrument name="LT202" tag="LT" number="202" tap="T201.right@0.82"/>
  <DvInstrument name="LIC202" tag="LIC" number="202" on="LV202" panel="true" value="45 %"/>
  <DvInstrument name="TI203" tag="TI" number="203" tap="E203.out" value="42 °C"/>
  <DvSignal from="FT101" to="FIC101"/>
  <DvSignal from="FIC101" to="FV101"/>
  <DvSignal from="PT201" to="PIC201"/>
  <DvSignal from="PIC201" to="PV201"/>
  <DvSignal from="TT201" to="TIC201"/>
  <DvSignal from="TIC201" to="TV201"/>
  <DvSignal from="LT201" to="LIC201"/>
  <DvSignal from="LIC201" to="LV201"/>
  <DvSignal from="FT202" to="FIC202"/>
  <DvSignal from="FIC202" to="FV202"/>
  <DvSignal from="LT202" to="LIC202"/>
  <DvSignal from="LIC202" to="LV202"/>
</DvProcess>
```

What to notice:

- The column has four separate nozzles on its faces (feed, reflux return, reboiler return, relief), the reflux valve `FV202` and the reboiler `E202` are mirrored (`flip="h"`) because they flow right → left, and all receiving terminals are to the right of what feeds them.
- `TT201`, `LT202` and `TI203` use `tap` so they sit where they measure; the other instruments use `on`.
- In the demo app (run `dotnet run -- process`, tab "Distillation — fixed + contain") the same drawing scales down and up with the window, centered.

<a id="copy-drawing"></a>
## Copying a given drawing (`placement="fixed"`)

1. **Inventory first.** Write down from the drawing: every piece of equipment with its tag (`T-201`, `P-202A`), its kind ([equipment](dvml-process-equipment.md#equipment) — column, drum, shell-and-tube exchanger, control valve, check valve, relief valve …), every line with its direction (follow the flow arrows; `from` is upstream), every instrument bubble with its letters and number, and every signal line (dashed) with its two ends. Equipment that has no built-in tag becomes a [`DvSymbol`](dvml-process-equipment.md#dvsymbol) — do not invent kinds.
2. **Size**: set `base-size` to the size of the drawing image in px (e.g. `1600 900`), so the coordinates can be read straight off the image. Two adjustments: if a terminal or label would run off the edge, add the same margin to every `x` (or `y`) and to `base-size`. If the drawing's symbols are much bigger than the built-in sizes (a 118px pump against 64px), multiply all coordinates and `base-size` by k = built-in size ÷ drawing size, so built-in kinds come out in the drawing's proportions; `fit="contain"` enlarges the whole picture again.
3. **Coordinates**: for each piece of equipment, read the **center of its symbol** on the image (not of its label) and write it as `x`·`y`. Small offsets of up to 16px between connected ports are straightened automatically ([snap](dvml-process-layout.md#snap)).
4. **Nozzles**: where the drawing shows a line entering a vessel at a particular point, measure it along that face: position = (distance from the top of the face) ÷ (face height) — for the left and right faces from the top, for the top and bottom faces from the left. Write it as `T1.left@0.15` ([face position](dvml-process.md#face-position)).
5. **Instruments**: read each bubble's center off the image as `x`·`y` ([instrument coordinates](dvml-process-layout.md#instrument-coordinates)). A bubble joined to a pipe or nozzle by a solid line → `tap` at that pipe's port (`tap="CV1.in"`) or at that point of the face (`tap="T1.left@0.85"`). A bubble with only signal lines → `on` the equipment it belongs to (controllers: the valve they move; others: the equipment of their loop — with `x`·`y` this only names the owner). A line with slashes is `DvSignal kind="pneumatic"`; a dashed line is the default. A colored or heavier signal line is `color`·`thickness`. Signal arrows and switch actions on signal lines are `DvSignal arrow`·`label` ([`DvSignal`](dvml-process.md#dvsignal)); line numbers are `DvLine label`.
6. **Media**: color the main lines with `medium` (water, steam, gas, oil …) on the first line of each run, as the drawing's legend says.
7. **State**: a drawing shows no running state. For the same look, leave `running`·`open` out, and symbols are drawn stopped (near-white in `plain`). On a page that shows live values, bind them, and they show the state colors.
8. **Compare**: render at the image size and look at it next to the drawing. Fix differences by moving equipment (`x`·`y`) — the library routes the pipes, so a pipe that runs differently from the drawing usually means a nozzle position or a piece of equipment is off. If the positions are right and the pipe runs differently, write its route with [`via`](dvml-process-layout.md#via).
9. **Build the same form as the drawing, as far as the library allows.** Compare each symbol's shape and proportions, positions, nozzle points, pipe routes, where captions sit and line kinds, and keep going until only what [not supported](dvml-process.md#process-not-supported) lists differs. Use what exists first: `<DvSignal kind="pneumatic"/>`, `<DvSignal arrow="end" label="SH"/>` (signal arrows, switch actions), `<DvLine label="01-100-PE-N"/>` (line numbers), `DvPump kind="isa"` + `DvMotor kind="isa"`, `DvVessel height`·`width`, and the extra kinds of [`DvValve`](dvml-process-equipment.md#dvvalve) (globe, angle, relief), [`DvInline`](dvml-process-equipment.md#dvinline) (reducer, orifice, vent, blind) and [`DvVessel`](dvml-process-equipment.md#dvvessel) (drum, column, open). Where a built-in kind looks different from the drawing's symbol (an exchanger drawn as a circle with a coil, a tank with legs and flanged nozzles), write a [`DvSymbol`](dvml-process-equipment.md#dvsymbol) in the drawing's shape and proportions instead of settling for the nearest kind; the number of symbols is not a cost. Give it ports where the drawing's nozzles are and a `DvPass` so flow and `medium` continue through it. For captions, corners and branches of signal lines, dashed lines from a pipe and bare arrow ends, use the [small symbols for drawing marks](dvml-process-equipment.md#symbol-drawing-marks). For the drawing's bubble size, set `font-size` = 14 × bubble diameter ÷ 40 in `base-size` px, here from the start; it sets the diagram's text too. Do not nudge attached boxes with `transform`: on a `DvAttach` it does not follow `font-size`.
10. Then add `fit="contain"` and follow the [sizing procedure](dvml-process-layout.md#sizing-procedure) for the real screen sizes.

Laying out a large drawing (bands, spacing, terminals, instruments): [complex P&IDs](dvml-process-layout.md#complex-pid).
