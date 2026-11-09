# Working with controls from code (id, vm)

Covers: `id` and `vm`, what the source generator creates, handles (`DvControl` and value handles), writing values (code owns what it writes), reading values, the `Click` event, where handles cannot be used, and project setup for the generator.
Read first: [binding syntax](dvml-language.md#binding) · [pages and the app](dvml-app.md#dvpage)
Not here: [value widget table (value attribute, two-way, handle type and members)](dvml-language.md#value-widgets) · [two-way binding](dvml-language.md#two-way) · [build diagnostics DVML001–021](dvml-diagnostics.md#build-diagnostics) · [thread contract](dvml-app.md#threads) · [which redraw call to use](llm-rules.md#updates-threads) · [`HandlerError` kinds](dvml-language.md#handler-error)

<a id="code-id-vm"></a>
## id and vm

Controls given a name (`id`) in markup can be used directly inside the VM as **typed fields**. The source generator creates the field declarations at build time, so the only files you write are the dvml and the VM. A misspelled name is a compile error, caught prior to running the app.

```xml
<!-- Pages/MainPage.dvml -->
<DvStack vm="MyApp.MainPage">
  <DvLabel  id="lblTemp" class="title" text="{Temperature} ℃"/>
  <DvButton id="btnStart" text="Start" onclick="Start"/>
</DvStack>
```

```csharp
// MainPage.cs: the only C# file you write. A class deriving from DvPage (or DvBar) is its own VM
public partial class MainPage : DvPage
{
    public double Temperature { get; set; }

    partial void OnLoaded()                   // once, after the first connection to the screen (App is filled at this point)
    {
        btnStart.Click += (s, e) => { /* ... */ };
    }

    public void Start()
    {
        btnStart.Enabled = false;
        lblTemp.Text = "Running";
        var r = btnStart.Bounds;              // SKRect, window coordinates
    }
}

// App start: the generator connects the markup
var app = new DvApp { Theme = DvTheme.Dark };
app.Pages.Add(new MainPage());
new ViewWindow(app, 1280, 800, "My HMI").Run();
```

- `id="name"` can go on any element. It must be a C# identifier (starts with an ASCII letter or `_`, no keywords) and be unique within one dvml. It has no effect on appearance.
- `vm="Namespace.Class"` on the root element names the VM that uses this dvml. It must be a top-level (not nested), non-generic `partial class`. **One dvml per VM.** A class declared without a namespace uses the bare class name.

<a id="generated"></a>
## What the generator creates

- For `DvApp` pages (`DvPage`), areas (`DvBar`) and dialogs (`DvDialog<T>`), each one's own class is its VM, and the generator also creates the markup hookup (the `MarkupSource` override) ([pages](dvml-app.md#dvpage), [dialogs](dvml-dialogs.md#dialog-define)).
- A dvml without `vm` generates nothing.
- The generator adds a file to the VM (`MainPage.g.cs`; do not edit it by hand) containing:
  - `public const string Markup` — the dvml source text. For `DvPage`/`DvBar` the app uses this value automatically. When you use `DvSurface`/`DvRenderer` directly, pass this value so the generator and runtime use the same source.
  - One **private** field per `id`, named the same as the `id`. Its type is the handle named like the tag: `DvLabel`, `DvButton`, and the value widgets `DvToggle`, `DvSwitch`, `DvCheckBox`, `DvRadio`, `DvKnob`, `DvSlider`, `DvInput`, `DvSelect`, `DvSegment`, `DvListBox`, `DvExpander`, `DvTreeView`, `DvDateTimeInput`, `DvColorInput` ([value widget table](dvml-language.md#value-widgets)). All other tags = `DvControl`.
  - `partial void OnLoaded()` — implementing it is optional.
- An `id` that matches an existing VM member gives a C# duplicate definition error.

<a id="handles"></a>
## Handles

Namespace `Devinno.UI.Controls`.

| Member | Description |
|---|---|
| `Id` | The `id` from markup |
| `Tag` | Any value the app wants to attach (`object?`) |
| `Enabled` | Whether it is enabled. Writing it overrides `disabled` in markup |
| `Visible` | Whether it is visible. Writing it overrides `visible` in markup |
| `Bounds` | The area (`SKRect`, [logical px](dvml-layout.md#logical-px)) in **window (top-level screen) coordinates** |
| `Click` | `EventHandler<DvClickEventArgs>`. `Position` = click position (window coordinates) — see [Click event](#code-click) |
| `ResetOverrides()` | Clears all values written from code and reverts to the markup values |
| `DvLabel.Text`, `DvButton.Text` | Text. Writing it overrides `text` |
| Value members (`Checked`, `Value`, `Text`, `SelectedIndex` …) | A value widget's value and change event — [value widget table](dvml-language.md#value-widgets) |

- **When handles are connected.** Inside the VM constructor the fields are `null`. When the screen (`DvSurface`) is created, the fields are filled in and `OnLoaded()` is called. Use handles from `OnLoaded()` on.
- **Reattaching.** The same VM can be attached to a new screen (when you recreate a `DvSurface` with the same VM). `DvApp` creates page/area screens once and keeps them, so a theme or language change rebuilds the same screen. The handle objects are reused; values written from code and `Click` subscriptions are kept.
- `OnLoaded()` is called **only once, the first time**, per VM instance (subscriptions are not added twice). If the same VM is attached to two screens at once, the later screen is the current one.
- **Markup and VM mismatch.** If you pass different markup to an `IDvControlHost` VM via `DvSurface` and an `id` is missing or has a different tag type, there is no exception: a Trace warning is logged for each such `id` (again on every reattach), and that handle is "not connected" (`Bounds` = `SKRect.Empty`, writes are only stored, `Click` is never raised). `DvApp` pages/areas always use their own dvml, so this does not happen on the normal path; with `DvSurface` directly, pass the `Markup` constant.

### Value handles

Giving an `id` to a widget that has a value creates a handle for that value. Use it like WinForms' `chk.Checked = false`. Which tag gives which handle type, value member and change event: [value widget table](dvml-language.md#value-widgets). Who owns the value: [writing values](#code-writes).

- **Change events** are raised only when the value actually changes, in all three ownership cases. Distinguish them with `e.ByUser`:
  - Changed on screen (press, drag release, edit finished, selection) — `ByUser = true`, on the input-processing thread.
  - Code writes to the handle — `ByUser = false`, immediately **on the writing thread**. Handler exceptions propagate directly to the writing code.
  - Code changes a bound VM property directly and the screen is rebuilt (the next layout after `Invalidate()`) — `ByUser = false`.
  - The value at first attachment to the screen is not a "change".
- Change events are `EventHandler<DvValueChangedEventArgs>` (`(sender, e) => … e.ByUser`).
- `DvInput` changes **once when editing finishes**, not on every keystroke. Writing non-numeric text to `Text` on a numeric input throws `FormatException`.
- `SelectedIndex` is based on the options from the last layout. Written prior to the first layout (inside `OnLoaded()`), it is applied on the first layout. Out of range throws `ArgumentOutOfRangeException` (after layout).
- Values written from code to `Value` of `DvKnob`/`DvSlider` are not adjusted by `step` or `min`/`max` (only values turned on screen are adjusted).
- `DvDateTimeInput.Value` reads as `DateTime` even when the bound property is `DateOnly`/`TimeOnly`, and converts to that type on write. `DvColorInput.Value` reads a string property (`#RRGGBB`) as a color and writes it back as `#RRGGBB`.
- Handle writes work from any thread (a redraw is scheduled — [thread contract](dvml-app.md#threads)).
- Handles of elements that are **not built at the moment** (content of another tab, a collapsed `DvExpander`) keep working: values and `Enabled`/`Visible` written to them are kept and shown when the element is built; `Bounds` is empty meanwhile.
- **Display-only widgets have no value handle** (`DvProgress`, `DvLamp`, `DvGauge`, `DvRing`, `DvTank`, `DvStepBar`, charts): their `id` gives a plain `DvControl` (`Enabled`, `Visible`, `Bounds`, `Click`). Drive their values with a one-way binding to a VM property (`value="{Speed}"`) and call `App.Invalidate()` after changing it — the normal pattern even on pages that use handles for everything else.
- `DvSwitch` (a built-in component) is the same value handle as `DvToggle` (omit `value` and give only `id` to make the handle the owner). Its `Bounds` covers the whole row including the label. User components have no value handle (plain `DvControl`).

```csharp
partial void OnLoaded()
{
    tgPump.CheckedChanged += (_, e) => Log($"Pump {tgPump.Checked} ({(e.ByUser ? "screen" : "code")})");
    segMode.SelectedIndex = 0;      // prior to layout: applied at the first layout
}
public void Stop() { tgPump.Checked = false; knSpeed.Value = 0; }
```

See the example `samples/Devinno.UI.Demo/Pages/BasicPage.dvml` (run argument `basic`).

<a id="code-writes"></a>
## Writing values: if code writes it, code owns it

- `btnStart.Enabled = false` and `lblTemp.Text = "…"` **replace** the markup value (a literal or a `{binding}`). After that, VM property changes do not override the value written by code.
- Written text is **literal text**. `{` `}` in it are not resolved as a binding.
- Reverting: `Text = null` reverts only the text; `ResetOverrides()` reverts `Enabled`, `Visible` and `Text` to the markup values.
- Written values persist across reattaching to a new screen (changing the theme will not unlock a button you disabled).
- Writing values is **safe from any thread** and sets the rebuild flag (the same flag as `DvSurface.Invalidate()`).
  - For **`DvApp` pages/areas**, the write also wakes the adapter (window/control) immediately (`DvApp.RenderRequested`). `btnStart.Enabled = false` from a timer or communication thread shows up on the next frame with no `Invalidate()` call. Values written inside a click handler are redrawn automatically.
  - A **host that uses `DvSurface` directly** only sets the flag and **does not wake** the host. After writing from a timer or communication thread, call the host's `Invalidate()` (`DvSurface.Invalidate()`, then wake the render loop). Otherwise the change appears the next time something else triggers a draw.
- A `Click` subscription or a write that requires a rebuild takes effect on the next layout.

### Value ownership

**A value has exactly one owner.** The element's `value` attribute decides who holds it. For `DvExpander` the attribute is `expanded`; for multi-select `DvListBox` it is `selected-items`.

| Markup | Owner | Read via handle | Write via handle | Change on screen |
|---|---|---|---|---|
| `value="{Bind X}"` | VM property `X` | The value of `X` | Writes to `X` (converted to the property type). Does not call `onbeforecommit` | Writes to `X` |
| No `value` + `id` | Handle | The value the handle holds (initially `false`/0/empty) | Stored in the handle | **Yes** — stored in the handle (`onbeforecommit` checks it) |
| `value="{X}"` (one-way) or a fixed value | Markup | The displayed value | **Overrides** the on-screen value (revert with `ResetOverrides()`) | No (read-only) |

- The second row is the way to use a value without a binding. An input widget with neither `id` nor `value` is read-only.
- Handling one value through both a binding and a handle never gets out of sync. With a binding, the handle is a window for reading and writing the VM property.
- Binding forms and `onbeforecommit`: [two-way binding](dvml-language.md#two-way), [onbeforecommit](dvml-language.md#onbeforecommit).

<a id="code-reads"></a>
## Reading values: based on the last completed layout

- Writing `false` to `Enabled`/`Visible` reads back as `false` immediately. All other reads reflect the state from the last completed layout: after writing `true`, it reads `true` only once the next layout completes.
- If an ancestor is disabled or hidden, `Enabled`/`Visible` read `false` regardless of the element's own value.
- With no completed layout, or when not connected, a read returns the last written value (`true` if none).
- `Text` is the overridden value; otherwise the markup/binding value from the last layout; otherwise `null`.
- `Bounds` is `SKRect.Empty` when `visible` is false, prior to the first layout, or when not connected. Inside a scroll box it reflects the current scroll position; for a `DvApp` area/page screen it reflects where that slot is drawn in the window.
- `Bounds` is **safest to read on the UI thread**. Reading it from another thread while the window resizes may mix old and new values.

```csharp
// show a hint box right below a button (inside a click handler)
var r = btnStart.Bounds;
PopX = r.Left;  PopY = r.Bottom + 4;  ShowHint = true;
// inside a handler the screen re-reads automatically; from a timer/communication thread call App.Invalidate()
```

`left`/`top` of `position:absolute` are relative to the parent's content box ([absolute](dvml-layout.md#absolute)). To use `Bounds` values as-is for absolute positioning, place the box under a root without padding. See the example `samples/Devinno.UI.Demo/Pages/ControlPage.dvml`.

<a id="code-click"></a>
## Click event

- Raised on press and release. Hit testing is the same as `onclick` (pressed element = released element, dragging 8 or more scrolls instead, not on disabled elements) — [click](dvml-language.md#click).
- If the element also has `onclick`, `onclick` runs first, then the `Click` subscribers.
- **Subscribing makes the element a click target.** Even a `DvBox` without `onclick` receives clicks once you subscribe to `Click`, and the click does not leak to an outer element's `onclick` (bubbling rules: [click](dvml-language.md#click)).
- Subscriptions take effect from the **next layout** (subscribing/unsubscribing requests a redraw).
- Not raised when disabled (including disabled inherited from ancestors).
- Raised **only on the plain click path**. Not raised on elements with their own behavior: toggle, input, select, date, color, grid and alarm widgets.
- A throwing subscriber is reported via `HandlerError` (`Kind` = `"Click"`) and the screen keeps running. **Subscribers after it in that event are not called** (default C# event behavior).
- Called on the same input-processing thread as `onclick`.

<a id="code-limits"></a>
## Where handles cannot be used

- An `id` inside a `DvRepeat`/`DvComponent` template creates no handle (the element is cloned once per item/instance); warning [DVML004](dvml-diagnostics.md#dvml004). Handle such elements with bindings and an `onclick` method that takes the item (`onclick="Select"` → `public void Select(Item item)`) — [click](dvml-language.md#click).
- An `id` on a component **instance** (`<PumpCard id="p1"/>`) works. `Bounds` and overrides apply only to that instance.

<a id="generator-setup"></a>
## Project setup

- Put the dvml anywhere in the project as a `.dvml` file; `*.dvml` is automatically a generator input. The generator pairs each `DvPage` subclass (the markup `vm`) with its dvml file.
- Referencing the NuGet package `Devinno.UI` includes `*.dvml` as `AdditionalFiles` and attaches the generator automatically. Nothing else to do.
- With a `ProjectReference` inside the repository, the package props do not apply: wire up the generator as an analyzer and the dvml as `AdditionalFiles` yourself.
- **NativeAOT and trimming are not supported.** Bindings (`{Property}`), `onclick` and grid `field` look up properties/methods by name at runtime (reflection). Publish the app as a normal JIT app (`PublishAot`/`PublishTrimmed` can leave bindings empty or handlers uncalled).

```csharp
// *.csproj (ProjectReference wiring)
// <ProjectReference Include="..\..\src\Devinno.UI.Generators\Devinno.UI.Generators.csproj" OutputItemType="Analyzer" ReferenceOutputAssembly="false" />
// <AdditionalFiles Include="Pages/*.dvml" />
```

See the example `samples/Devinno.UI.Demo/Devinno.UI.Demo.csproj`.
