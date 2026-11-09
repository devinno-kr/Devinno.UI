# DVML repeats, components and custom widgets

Covers: `DvRepeat` (one template per item), user components (`DvComponent`, `DvParam`, `DvSlot`, instance rules, component names), and custom widgets drawn in C# (`DvWidget`).
Read first: [markup basics](dvml-language.md#markup-basics) · [binding syntax](dvml-language.md#binding) · [click rules](dvml-language.md#click)
Not here: [built-in components such as `DvCard`, `DvStat`, `DvSwitch`](dvml-widgets.md#semantic-components) · [a table with selection, sorting and virtualization (`DvDataGrid`)](dvml-grid.md#dvdatagrid) · [process diagram units built from components](dvml-process.md#units) · [process symbols (`DvSymbol`)](dvml-process-equipment.md#dvsymbol) · [which widgets write two-way](dvml-language.md#two-way) · [`HandlerError` kinds](dvml-language.md#handler-error) · [style priority](dvml-style.md#priority)

<a id="dvrepeat"></a>
## DvRepeat

Creates its child template once per item of a list and stacks the copies like `DvStack`.

### Attributes

`DvRepeat` also takes the [common attributes](dvml-language.md#common-attribute-matrix).

| attribute | values | default | bound | notes |
|---|---|---|---|---|
| `items` | a non-string `IEnumerable` | none | yes (one-way) | Empty or null = no children (height 0, no error) |
| | | | | A string, a non-collection or an enumeration error = no children |
| `gap` | step name `xs`…`2xl`, `none`, length | `md` | yes | Same as `DvStack` ([layout sugar](dvml-layout.md#layout-sugar)) |
| `padding` | 1 to 4 values | `0` | yes | Each value mapped like `gap` |
| `align` | `start` `center` `end` `stretch` | `stretch` | yes | `align-items` |
| `justify` | `start` `center` `end` `between` | `start` | yes | `between` = space-between |

### Behavior

- Children are a template, created once per item.
- Layout is the same as `DvStack`. For horizontal rows or grids use `style` (`display:flex; flex-direction:row`).
- Inside the template: `{Item}` / `{Item.X}` = current item, `{Index}` = 0-based index, any other path = outer context (screen VM, component parameters, or the item scope of an outer repeat).
- `{Bind Item.X}` writes two-way to the item.
- `onclick="Inspect"` inside the template calls `VM.Inspect(Pump p)` if it exists, otherwise `VM.Inspect()` ([click rules](dvml-language.md#click)).
- The data belongs to the app. Call `Invalidate()` after changing the collection.
- Nesting: `<DvRepeat items="{Item.Children}">` — the inner `Item` shadows the outer `Item`.
- A repeat also works inside component templates (outer scope = component parameters).
- An `onclick` **inside a component template** within a repeat does not receive the item; an `onclick` given on the instance does ([instance rules](#component-instances)).
- No virtualization, keys or selection: use [`DvDataGrid`](dvml-grid.md#dvdatagrid) for those. Use long lists together with `overflow:scroll`.
- An `id` inside the template creates no handle (build warning [DVML004](dvml-diagnostics.md#dvml004)).

### Example

```xml
<DvRepeat items="{Pumps}" gap="xs" grow="1" style="overflow:scroll">
  <DvRow>
    <DvLabel class="body" text="{Index}"/>                <!-- 0-based -->
    <DvLabel class="body" text="{Item.Name} {Unit}"/>     <!-- Item = current item, other paths = screen VM -->
    <DvToggle value="{Bind Item.Auto}"/>                  <!-- two-way writes to the item -->
    <DvButton variant="ghost" text="Open" onclick="Inspect"/>  <!-- VM.Inspect(Pump p) if present, else VM.Inspect() -->
  </DvRow>
</DvRepeat>
```

### See also

[layout sugar](dvml-layout.md#layout-sugar) · [scrolling](dvml-layout.md#scroll) · [handles cannot be made inside templates](dvml-code.md#code-limits)

<a id="dvcomponent"></a>
## DvComponent

Defines a reusable piece of markup under a new tag name; instances of the tag expand the template.

### Attributes

| tag | attribute | notes |
|---|---|---|
| `DvComponent` | `name` | Required; the tag name of the instances ([name rules](#component-names)) |
| `DvParam` | `name` | Parameter name ([parameters](#component-params)) |
| `DvParam` | `default` | Value when the instance does not give the parameter; none = null |
| `DvSlot` | — | Where the instance's children go ([slots](#component-slots)) |

### Behavior

- `DvComponent` requires `name`. **The first single child** that is not `DvParam`/`DvStyle` is the template root. A missing `name` or template root throws `ArgumentException` at registration.
- Register in C# with a `ComponentRegistry` and give that registry to the app or surface:

```csharp
using Devinno.UI.Components;

var registry = new ComponentRegistry();
registry.Register(markup);
app.Registry = registry;   // or new DvSurface(markup, vm, theme, registry) / DvRenderer
```

- A component whose template root is `<DvUnit>` becomes a unit inside a process diagram ([units](dvml-process.md#units)).
- The same `Register` also registers process diagram equipment symbols (`<DvSymbol>`) ([symbols](dvml-process-equipment.md#dvsymbol)).
- A `<DvStyle>` inside a definition (outside or inside the template) applies **globally** to every screen using that registry. Order: theme < component `DvStyle` (registration order) < screen `DvStyle`, so for the same class and property **the screen `DvStyle` wins** ([priority](dvml-style.md#priority)).
- `<DvStyle>` of components registered in code is not visible to the build check (see [DVML014](dvml-diagnostics.md#dvml014)).

### Example

```xml
<DvComponent name="PumpCard">
  <DvParam name="Title" default="-"/>
  <DvParam name="Value"/>
  <DvCard title="{Title}">
    <DvLabel class="display" text="{Value}"/>
    <DvSlot/>
  </DvCard>
</DvComponent>
```

```xml
<PumpCard Title="Intake pump" Value="{Flow}" grow="1">
  <DvLabel class="caption" text="Normal operation"/>
</PumpCard>
```

### See also

[parameters](#component-params) · [slots](#component-slots) · [instance rules](#component-instances) · [name rules](#component-names)

<a id="component-params"></a>
## Parameters (`DvParam`)

- `DvParam name default`: instance attribute names match parameters **case-insensitively**; `{Name}` inside the template is **case-sensitive**.
- Parameters not given get `default` (null if none).
- `{Name}` inside the template refers **only to parameters**, not to the screen VM. Instance attribute values are bound from the outer VM.
- Pass other screen VM properties as parameters. The simplest approach is one object per instance (for example one VM object per unit), passed as a single parameter (`U`, used as `{U.Level}`).
- Giving a parameter a two-way binding (`Value="{Bind X}"`) makes `value="{Value}"` inside the template write two-way to the outer VM for the template two-way tags: `DvToggle` `DvCheckBox` `DvKnob` `DvSlider` `DvInput` `DvSelect` `DvSegment` `DvTabs` `DvDateTimeInput` `DvColorInput`, latching `DvButton`, and every custom widget ([two-way rules](dvml-language.md#two-way)).
- `onclick` inside a template calls a method on the **screen VM**. The method name is fixed in the template; a parameter cannot change it.
- A one-parameter method receives the component parameter value of matching type: a button inside `<PumpCard Pump="{P1}"/>` with `onclick="Stop"` → `Stop(PumpVm pump)`. Matching rules: [instance rules](#component-instances).

<a id="component-slots"></a>
<a id="dvslot"></a>
## Slots (`DvSlot`)

- The instance's children go in place of `<DvSlot/>`.
- A `DvSlot` inside a nested component also receives the outer instance's children.

<a id="component-instances"></a>
## Instance rules

These apply to instances of user components and of built-in components (`DvCard`, `DvPageFrame`, `DvSwitch` …).

**What an instance takes**: component parameters, plus these attributes, forwarded unless a parameter has the same name (then it is the parameter):

| attribute | applied to | rule |
|---|---|---|
| `style` | template root | Merged into the root's style; the instance wins |
| `class` | template root | Appended after the root's classes |
| `grow` `width` `height` | template root | The instance wins |
| `onclick` | template root | Becomes the root box's click handler; the instance wins over a root `onclick` |
| `disabled` | template root | Evaluated against the outer VM; the instance wins; inherited downward (e.g. the toggle inside `DvSwitch` is disabled too) |
| `visible` | the instance | `false` = the instance is not expanded |
| `id` | the instance | A handle whose `Enabled`, `Visible`, `Bounds` and `Click` act on that instance only (also for a process unit instance) ([handles](dvml-code.md#handles)) |

- Any other attribute on an instance (`hover-style`, `content-align`, …) is ignored.
- Instance `onclick`: the method is looked up on the screen VM; inside a `DvRepeat`, `M(item)` takes precedence (same as an ordinary `onclick`).
- `onclick` **inside the template**: a method with **exactly one parameter** (`Reset(BlenderVm unit)`) receives the instance's parameter value whose type matches. If the component has several parameters, the first (in `DvParam` order) whose value fits the method's parameter type is passed. None fits = the parameterless `Reset()` is called. Methods with 2+ parameters are never called.
- This tells which instance's button got the press when one component is used several times (the same mechanism as `DvRepeat`'s `M(item)`).
- A component that expands itself (a cycle) throws `InvalidOperationException` at depth 64.

```xml
<DvCard title="Intake pump #1" onclick="OpenPump">
  <DvLabel text="Running"/>
</DvCard>
```

<a id="component-names"></a>
## Component and widget names

- A component cannot take the name of a built-in tag (`DvGauge`, `DvStack`, `DvScreen` …): `ComponentRegistry.Register` throws `ArgumentException`, and a `<DvComponent>` in a dvml is build error [DVML021](dvml-diagnostics.md#dvml021).
- Built-in components (`DvCard`, `DvPageFrame`, `DvSwitch` …) may be replaced: with the same name, a user component takes precedence over the built-in one.
- Custom widget tags starting with `Dv` are reserved.
- A custom widget and a `DvComponent` cannot share a name (`ArgumentException`).
- Prefix a component's class names with the component name (`.pumpcard-head`) so they do not collide with other components.

<a id="dvwidget"></a>
## Custom widgets (`DvWidget`)

When no built-in widget or component can show something (a battery cell bar, a spinning fan, a special instrument face), paint it with SkiaSharp: derive from `DvWidget`, register it under a tag with `registry.RegisterWidget<T>("Tag")`, and use the tag in markup.

Order of choice: built-in widget → `DvComponent` (assembling built-ins) → custom widget ([choosing a widget](llm-rules.md#choose-widget)).

### Attributes

Attributes are free-form: every attribute is resolved (bindings included) and the widget class reads the ones it uses with `ctx.Number("value")`, `ctx.Text("label")`, `ctx.Flag("running")`, `ctx.Value("series")` (the original object, e.g. a list).

The markup side works like any element: `class`, `style` (background, border, `padding`, `width`/`height`), `grow`, `onclick`, `hold`, `disabled`, `visible`, `id` (a plain `DvControl` handle), and `onbeforecommit` for a widget that writes `value`.

### Behavior

- Child elements of a custom widget are not used (Trace warning).
- **Size**: `Measure` returns the content size (inside `padding`); the default is 120×80. Style `width`/`height` win.
- **Painting**: `Paint` gets the content area; the canvas is clipped to the element. Use theme tokens through `ctx.Color("…")` (the same names as `token(…)`) so the widget follows light and dark themes.
- `ctx.TextColor`, `ctx.DrawText` and `ctx.MeasureText` draw text with the library's fonts (Korean included).
- **Animation**: return a frame interval in ms from `FrameInterval` while the picture moves (e.g. `ctx.Flag("running") ? 33 : null`; minimum 16 ms) and use `ctx.TimestampMs` for the phase. `null` = nothing is repainted.
- **Instances**: one per element, kept when the screen is rebuilt, so fields can hold animation state. Each `DvRepeat` item has its own. Read attribute values from `ctx` on every call; do not cache them in fields.
- **Paint errors**: an exception from `Paint` does not stop the screen. It is reported once through `HandlerError` (`Kind` = `"Paint"`) and the element is outlined in red ([HandlerError kinds](dvml-language.md#handler-error)).
- **Touch and drag**: override `OnPointerDown(ctx, point)` and return `true` to take the gesture. The widget then gets `OnPointerMove`, `OnPointerUp`, or `OnPointerCancel` (the element disappeared or a dialog opened); the screen does not scroll and no `onclick` happens. Return `false` to let the press work as a normal click.
- Pointer points are in the same coordinates as `ctx.Bounds` (the content area). Pointer methods are not called while disabled.
- **Pointer errors**: an exception from a pointer method is reported through `HandlerError` (`Kind` = `"Pointer"`) and ends the gesture.
- **Writing `value`**: give the element a two-way binding (`value="{Bind Setpoint}"`, an item path inside `DvRepeat`, or a two-way component parameter) and call `ctx.Commit(newValue)`: `onbeforecommit` is asked first, the value is converted to the property type, and the screen is rebuilt.
- `Commit` works only inside the pointer methods.
- `ctx.CanCommit` says whether writing is possible (false for a one-way `value`, when disabled, or outside the pointer methods).
- As with the built-in knob and slider, keep the preview in a field while dragging and commit once on release.

### Example

```csharp
using Devinno.UI.Widgets;
using SkiaSharp;

public sealed class BatteryGauge : DvWidget
{
    public override SKSize Measure(DvWidgetContext ctx, float? availableWidth) => new(240, 28);

    public override void Paint(SKCanvas canvas, SKRect bounds, DvWidgetContext ctx)
    {
        var soc = Math.Clamp(ctx.Number("value"), 0, 100);
        using var track = new SKPaint { IsAntialias = true, Color = ctx.Color("gauge-track") };
        using var fill = new SKPaint { IsAntialias = true, Color = ctx.Color(soc < 20 ? "tone-danger" : "tone-success") };
        canvas.DrawRoundRect(bounds, 4, 4, track);
        canvas.DrawRoundRect(new SKRect(bounds.Left, bounds.Top, bounds.Left + bounds.Width * (float)soc / 100, bounds.Bottom), 4, 4, fill);
    }
}

registry.RegisterWidget<BatteryGauge>("BatteryGauge"); // register once at startup, ahead of the first screen
```

```xml
<BatteryGauge value="{Soc}" grow="1" onclick="OpenRack"/>
```

A widget that writes its value:

```csharp
using Devinno.UI.Widgets;
using SkiaSharp;

public sealed class SetpointBar : DvWidget
{
    private double? _drag; // preview while dragging

    public override void Paint(SKCanvas canvas, SKRect bounds, DvWidgetContext ctx)
    {
        var sp = _drag ?? ctx.Number("value");
        using var p = new SKPaint { IsAntialias = true, Color = ctx.Color("primary") };
        canvas.DrawRect(new SKRect(bounds.Left, bounds.Top, bounds.Left + bounds.Width * (float)sp / 100, bounds.Bottom), p);
    }

    public override bool OnPointerDown(DvWidgetContext ctx, SKPoint point)
    {
        if (!ctx.CanCommit)
            return false;
        _drag = At(ctx, point);
        return true;
    }

    public override void OnPointerMove(DvWidgetContext ctx, SKPoint point) => _drag = At(ctx, point);

    public override void OnPointerUp(DvWidgetContext ctx, SKPoint point)
    {
        ctx.Commit(Math.Round(At(ctx, point)));
        _drag = null;
    }

    public override void OnPointerCancel(DvWidgetContext ctx) => _drag = null;

    private static double At(DvWidgetContext ctx, SKPoint p) =>
        Math.Clamp((p.X - ctx.Bounds.Left) / ctx.Bounds.Width * 100, 0, 100);
}
```

```xml
<SetpointBar value="{Bind Setpoint}" onbeforecommit="ConfirmSetpoint"/>
```

### See also

[name rules](#component-names) · [commit gate](dvml-language.md#onbeforecommit) · [theme tokens](dvml-style.md#theme-tokens)
