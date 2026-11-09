# Dialogs (DvDialog, ShowDialog, MessageBox)

Covers: how dialogs work, defining a dialog, showing it and receiving the result, the message box, the built-in input/number/select boxes, finding a dialog element's position from code (`BoundsOf` and the built-in box ids), dialog lifecycle and input, patterns (confirm, edit a copy, communication alarms, stacking, closing one dialog), and screen lock and idle detection.
Read first: [pages and the app](dvml-app.md#dvapp) · [the complete app example with `SetpointDialog`](dvml-app.md#app-example)
Not here: [`<DvDialogFrame>` attributes and look](dvml-widgets.md#dvdialogframe) · [thread contract](dvml-app.md#threads) · [`HandlerError` kinds](dvml-language.md#handler-error) · [scroll boxes](dvml-layout.md#scroll) · [dialog button texts (localization keys)](dvml-app.md#localization) · [alarm widgets](dvml-instruments.md#dvalarmlist)

<a id="dialog-flow"></a>
## How dialogs work

A dialog is a window that floats above the page and collects an answer from the user (recipe editing, "Really delete?", communication alarms). Like pages, **dialogs are created, held and closed by `DvApp`.** App code asks "show this dialog with these arguments" and receives the result returned on close via `await`.

| Name | Role |
|---|---|
| `DvDialog<TResult>` | One dialog. Built like a page with a `partial class` + dvml; the class itself is the VM. `TResult` is the type of the result it returns |
| `<DvDialogFrame>` | Root tag of a dialog's dvml. The theme draws the background, border, shadow and title ([DvDialogFrame](dvml-widgets.md#dvdialogframe)) |
| `App.ShowDialog<TDialog, TResult>(args…)` | The app creates an instance through the constructor matching the arguments, shows it on top, and returns a `Task<TResult?>` that yields the result ([showing and results](#dialog-show)) |
| `App.MessageBox(text, caption, buttons)` | Built-in message box. Returns the button pressed (`DvDialogResult`) ([message box](#messagebox)) |
| `App.InputBox` · `App.InputNumber` · `App.SelectBox` | Built-in boxes. Ask for one string, one number, or one item from a list and return the entered string, the entered number, or the chosen item's index (`null` on cancel) ([built-in boxes](#builtin-boxes)) |
| `App.Dialogs` | List of open dialogs (bottom → top). `Count`, indexer, `Top` (topmost) |
| `App.CloseDialogs()` | Closes all open dialogs as canceled |
| `DvApp.Current` | The way to reach the app from outside a page (communication classes, etc.) |
| `App.Lock<TDialog>()` · `App.Unlock()` · `App.IsLocked` | Lock layer — shows an app-made login dialog above all dialogs and blocks input below until unlocked ([lock and idle](#lock-idle)) |
| `App.IdleTimeout` · `App.Idle` | Idle detection — one event when there is no input for the set time ([lock and idle](#lock-idle)) |

All are in the `Devinno.UI.App` namespace. The smallest example is `SetpointDialog` (setpoint editing) in [the complete app example](dvml-app.md#app-example).

- Dialogs can only be shown from screens inside `DvApp` (pages, bars, dialogs). Hosts that use `DvSurface` directly must build their own confirm UI.

**Flow at a glance** — pressing "Change setpoint" in [the complete app example](dvml-app.md#app-example):

1. `EditSetpoint()` calls `App.ShowDialog<SetpointDialog, double?>(Setpoint)`. The app immediately creates `new SetpointDialog(25.0)` and puts it on top of the list (`App.Dialogs`). From this moment the page below receives no presses.
2. On the next frame the dialog's view is built and drawn at the center of the window. A translucent overlay (`scrim`) covers what is below.
3. When the user edits the value and presses "OK", `Ok()` sets the result with `Close(Value)`. The dialog leaves the list immediately.
4. On the next frame the view is detached, and `await` receives the result (`30.0`) and continues. If canceled, it is `null`.

<a id="dialog-define"></a>
## Defining a dialog

Same approach as pages ([DvPage](dvml-app.md#dvpage)): write two files, a class and a dvml, and connect them with `vm` on the dvml root.

```xml
<DvDialogFrame vm="MyApp.RecipeEditDialog" title="Edit recipe" subtitle="Change the values, then press OK" style="width:420">
  <DvInput value="{Bind Name}" placeholder="Name"/>
  <DvInput value="{Bind Temp}" keyboard-type="numeric" min="0" max="200" format="0.0" unit="℃"/>
  <DvRow gap="sm">
    <DvSpacer/>
    <DvButton variant="secondary" text="Cancel" onclick="Cancel"/>
    <DvButton variant="primary" text="OK" onclick="Ok"/>
  </DvRow>
</DvDialogFrame>
```

```csharp
namespace MyApp;

public sealed record Recipe(string Name, double Temp);
```

```csharp
using Devinno.UI.App;

namespace MyApp;

// Edits a copy of the values instead of the original Recipe: on cancel the original is unchanged.
public sealed partial class RecipeEditDialog : DvDialog<Recipe>
{
    public RecipeEditDialog(Recipe source)
    {
        Name = source.Name;
        Temp = source.Temp;
    }

    public string Name { get; set; }
    public double Temp { get; set; }

    public void Ok() => Close(new Recipe(Name, Temp));
    public void Cancel() => Close();        // result = null (cancel)
}
```

- **Use `DvDialogFrame` as the root** ([its attributes](dvml-widgets.md#dvdialogframe)). Other tags (`DvStack`, etc.) can be the root and are centered at their size, but without a background they are **transparent, so what is below shows through.**
- **`vm` is `namespace.class`** (`vm="MyApp.RecipeEditDialog"`). Only a class declared without a namespace (the `SetpointDialog` sample) uses the bare class name. If wrong, build error [DVML005](dvml-diagnostics.md#dvml005).
- The class is a `partial class` deriving from `DvDialog<TResult>` and MUST have a **public constructor**. The generator creates the markup connection, `id` fields and `OnLoaded()` ([what is generated](dvml-code.md#generated)).
- `DvInput`, `DvSelect`, pickers and the virtual keyboard inside a dialog behave exactly as on a page; popups appear above the dialog.
- The base class has no `Id` or `Title` properties. The title is the dvml `title`; to tell dialogs apart, put a key on the class yourself ([pattern 3](#dialog-patterns)).
- What a dialog can use: `App` (the app that showed it — in handlers and `OnLoaded`; **reading it inside the constructor throws `InvalidOperationException`**), `IsOpen`, `protected Close(result)`/`Close()`, and the overridable `protected virtual void OnClosed()` (cleanup after closing).

**Size and position**

- A dialog never grows larger than the window minus 16 on each side. Text is measured with wrapping within window width − 32 ([text wrapping](dvml-layout.md#text-wrap)) and re-wrapped when the window shrinks.
- Content that overflows the height is clipped, so put long content in an `overflow:scroll` box ([scroll](dvml-layout.md#scroll)). In a height-clipped slot the scroll box shrinks so the rest (button row) stays inside. Examples: `style="max-height:320; overflow:scroll"`, or `overflow:scroll` with no `max-height` (content height if it fits in the window, otherwise it shrinks to fit the slot — the built-in message box body works this way).
- The position is always the center of the whole window (it also covers fixed areas such as the top bar), and it re-centers when the window size changes.

<a id="dialog-show"></a>
## Showing a dialog and receiving the result

Signature: `Task<TResult?> ShowDialog<TDialog, TResult>(params object?[] args) where TDialog : DvDialog<TResult>`. Callable from any thread.

**Constructor selection** — the app picks the one public constructor whose **parameter count equals `args` and to whose parameter types each argument is directly assignable**, and creates the instance on the thread that called `ShowDialog`.

- No matching constructor, or more than one, throws `ArgumentException` on the spot (the message lists the passed argument types and the available constructors). The dialog is not registered. An exception thrown by the constructor propagates as-is to the caller.
- No numeric conversion is done: `25` (int) does not match a `double` parameter, so pass `25.0`. Optional parameters (default values) are not filled in; pass the full count.
- `null` matches only reference-type and nullable-type parameters. `ShowDialog<X, R>(null)` means "one argument that is null".
- Passing one array (`string[] names`) makes that array one argument. **To pass a single `object[]`, wrap it as `(object)items`** — unwrapped, its elements spread into the argument list.
- **In the constructor, only store the received values in fields/properties.** The constructor may run on any thread (a communication thread); there is no view at that point and `App` is empty. Put setup code in `OnLoaded()` (once after the view exists, on the input thread).

**Results**

- `Close(result)` gives that value; `Close()` gives `default` (cancel).
- Closed from outside (`CloseDialogs()`, the dialog below closes, the window closes), the result is always `default`. **Use a result type in which `default` is distinguishable as cancel.**

| Result type | On cancel | Use |
|---|---|---|
| Reference type (`Recipe`, `string`) | `null` | Returning edited data |
| `bool?` | `null` | Yes/No + cancel |
| `int?`, `double?` | `null` | Chosen number, entered number |
| `DvDialogResult` | `Cancel` (value 0) | The button pressed |
| `bool`, `int`, `double` | `false`, `0` | **Do not use** — "No" or "entered 0" cannot be distinguished from cancel |

- The result is delivered **exactly once**, whatever the closing path. Calling `Close` twice keeps the first value. `Close` is callable from any thread.
- The result is carried on the app's frame. `await` keeps waiting in two cases: an **app never attached to a window** (until its first frame) and a **detached app** (WinForms `ViewControl.App` switched to another app; until attached again). `ShowDialog` on an already **closed app** completes immediately with a cancel result on the calling thread.
- `Close` is `protected`: only the dialog itself can call it. The only public outside close is `App.CloseDialogs()` (all); to close one specific dialog use [pattern 5](#dialog-patterns).
- `ShowDialog` does not return the instance. If you need it, find it in `App.Dialogs`.

**After `await`: which thread, and what to do** ([thread contract](dvml-app.md#threads))

- `await` in a page, area or dialog handler (`onclick`, etc.) continues **on the input (UI) thread** in the frame where the result arrives. Use the VM directly.
- VM values changed after `await` are **redrawn without notification**: in the frame where the continuation runs, the app re-reads the current page, areas and open dialogs (`EditSetpoint` in [the complete app example](dvml-app.md#app-example)). Values changed from timer or communication threads need `App.Invalidate()` ([update table](llm-rules.md#updates-threads)).
- `await` with no synchronization context (a communication thread) continues on the input thread that delivered the result. Long work after it (waiting for a communication response, saving a file) freezes the screen meanwhile, so **hand it off with `Task.Run`.**
- Handlers may be `async void`. Exceptions inside them do not kill the process; they arrive at `App.HandlerError` (`Kind` = `"async"`, `Member` = null) ([HandlerError](dvml-language.md#handler-error)). `async` methods started from a communication thread lack this protection: make them return `Task` and handle exceptions yourself.

<a id="messagebox"></a>
## Message box

Signature: `Task<DvDialogResult> MessageBox(string text, string? caption = null, DvMessageButtons buttons = DvMessageButtons.Ok)`.

| `buttons` | Buttons (left → right) | Result |
|---|---|---|
| `Ok` (default) | [OK] | `Ok` |
| `OkCancel` | [Cancel] [OK] | `Cancel` · `Ok` |
| `YesNo` | [No] [Yes] | `No` · `Yes` |
| `YesNoCancel` | [Cancel] [No] [Yes] | `Cancel` · `No` · `Yes` |

- The main button (OK/Yes) is the rightmost `primary` button.
- Closed from outside, the result is `DvDialogResult.Cancel` — also for `Ok`/`YesNo` boxes that have no cancel button. **Compare with `== DvDialogResult.Yes`**; `!= DvDialogResult.No` mistakes a close from outside (`Cancel`) for Yes.
- `caption` `null` → no title row. Split the body into lines with `\n`.
- Width defaults to 360; long text widens it up to window width − 32 and then wraps (font `font.body`; the body is a single label — [text wrapping](dvml-layout.md#text-wrap)). If the window size changes while the box is shown, it re-wraps. Text taller than the window: only the body scrolls and the button row stays in the box (short text has no scrollbar).
- Button text comes from the [localization](dvml-app.md#localization) keys `dv.dialog.ok` `dv.dialog.cancel` `dv.dialog.yes` `dv.dialog.no`; if absent, the built-in English (or Korean with `Language = "ko"`) OK/Cancel/Yes/No.
- Each call creates a new stacked box. Several in a row do not overwrite one another; each returns its own result. Callable from any thread.
- The buttons have fixed ids (`yes`/`ok`, `no`, `cancel`) for [`BoundsOf`](#dialog-bounds).

<a id="builtin-boxes"></a>
## Built-in boxes: text input, number input, choosing from a list

When asking for **just one value** (a name, a setting, a mode), **do not build a dialog**: the app includes three boxes. Build your own ([defining a dialog](#dialog-define)) only when editing several values together or when validation/preview is needed.

| Signature | On OK | Appearance |
|---|---|---|
| `Task<string?> InputBox(string caption, string? value = null)` | The entered text | Title + one input box + [Cancel] [OK] |
| `Task<double?> InputNumber(string caption, double value, double? min = null, double? max = null)` | The entered number | Title + one numeric input box + [Cancel] [OK] |
| `Task<int?> SelectBox(string caption, IReadOnlyList<string> items, int selected = -1)` | Index of the chosen item | Title + item list + [Cancel] [OK] |

**Common**

- **A `null` result means cancel** (the cancel button, or closed from outside: `CloseDialogs()`, the dialog below closes, the window closes). Receive it with `is { } value` as in the example below. Do not erase the null with `?? 0` or `.Value`; cancel would become 0 or an exception.
- Width is fixed at 360 and the title is `caption`. OK is the rightmost `primary` button; button text uses the same localization keys as the message box, `dv.dialog.ok` and `dv.dialog.cancel` (if absent, OK/Cancel).
- Callable from any thread; each call stacks a new box that returns its own result. Thread rules after `await` are the same as for `ShowDialog` ([after `await`](#dialog-show)).
- The input box, the list and the buttons have fixed ids (`input`, `list`, `ok`, `cancel`) for [`BoundsOf`](#dialog-bounds).

**Input boxes (`InputBox`, `InputNumber`)**

- **Editing starts as soon as the box appears.** In touch mode the virtual keyboard comes up immediately; `InputNumber` shows the numeric keypad. If another dialog appears on top at the same time, editing starts when that dialog closes and the input box becomes topmost (a buried box is not edited).
- **Done (Enter) is OK.** Accepting input with the virtual keyboard's Done key, a physical Enter, or Enter in the native editor (`UseNativeEditor = true`) closes the box immediately with that value.
- Pressing the on-screen OK button while the virtual keyboard is shown counts as a press outside the keyboard: **editing is canceled and the press is not treated as OK** (the box stays with the pre-edit value). In touch mode, confirm with Done. In native editor mode, pressing OK while editing commits the typed text and then confirms.
- `InputBox` with `value` `null` starts empty. Confirming an empty field gives `""`, which is different from cancel (`null`).
- `InputNumber`'s `min`/`max` follow the `DvInput keyboard-type="numeric"` rules. Non-numeric or out-of-range input is rejected (error shown, editing continues), so **the OK result is always within range.** Done on an out-of-range value does not close the box. `min` or `max` `null` = no limit on that side.
- `InputNumber`'s initial `value` must be within range. NaN/infinity, `min >= max` (both given), or `value` outside `min`–`max` throws `ArgumentOutOfRangeException` at the call site and the box is not shown. `value == min` and `value == max` are allowed; `min == max` is not a range. Clamp a stored value with `Math.Clamp` first.

**Select box (`SelectBox`)**

- Items appear one per row (height 40; an item longer than the box width wraps and the row grows). Pressing a row selects it (selection color). Only **one** can be chosen (no multi-select).
- **OK can be pressed only when a row is selected.** `selected` is the initial index; -1 (default) or out of range = nothing selected.
- Empty `items` throws `ArgumentException` (`ArgumentNullException` if `null`) at the call site and the box is not shown. Check first if the list may be empty.
- `items` is copied at the call. Later list changes are not reflected, and the result index refers to the list as of the call (with duplicate texts, the index of the chosen row).
- The list area has a maximum height of 320 and scrolls when it overflows; with few items it is as tall as the items. In a short window the list shrinks so the buttons stay visible ([scroll box shrink](dvml-layout.md#scroll)), and it adjusts in place when the window resizes.

Example — rename, change temperature and choose mode, added to the recipe page of [pattern 1](#dialog-patterns) (another file of the same `partial class`):

```csharp
using Devinno.UI.App;

namespace MyApp;

public sealed partial class RecipePage
{
    public string[] Modes { get; } = ["Auto", "Manual", "Stop"];
    public int Mode { get; private set; }

    public async void RenameRecipe()
    {
        if (Current is not { } recipe)
            return;
        if (await App.InputBox("Recipe name", recipe.Name) is { } name)        // null = cancel
            Current = recipe with { Name = name };
    }

    public async void ChangeTemp()
    {
        if (Current is not { } recipe)
            return;
        if (await App.InputNumber("Setpoint (℃)", recipe.Temp, 0, 200) is { } temp) // always 0 to 200
            Current = recipe with { Temp = temp };
    }

    public async void ChooseMode()
    {
        if (await App.SelectBox("Operating mode", Modes, Mode) is { } index)
            Mode = index;
    }
}
```

<a id="dialog-bounds"></a>
## Finding a dialog element from code (BoundsOf)

`SKRect? BoundsOf(string id)` on any open dialog (`App.Dialogs.Top`, or an element of `App.Dialogs`) returns where the element with that `id` is, in window coordinates: the same coordinates as a handle's `Bounds` and as `App.Surface.PointerDown`. Use it in headless tests to press dialog buttons ([driving the app without a window](llm-rules.md#verify-targets)).

- It works for your own dialogs (any element with an `id`) and for the built-in boxes, whose elements have the fixed ids below.
- **Read it after a frame, on the thread that runs the frames.** It reflects the last completed layout and the current scroll position, like a handle's `Bounds` ([handles](dvml-code.md#handles)).
- **`null`** when the dialog is not open (closed, or closing requested), its screen is not laid out yet (`ShowDialog` was called but no frame has run since), no element has that `id`, or the element is not in the layout (`visible` false, or an ancestor hidden).
- Elements inside a `DvRepeat` and inside a component's own markup are not searched, the same limit as `id` handles. The `DvRepeat` itself and a component instance can be found by their own `id`. With duplicate ids, the first in markup order wins.
- `id` `null` throws `ArgumentNullException`.

Ids of the built-in boxes:

| Box | Id | Element | Present when |
|---|---|---|---|
| `MessageBox` | `yes` | Yes button (rightmost) | `buttons` is `YesNo` or `YesNoCancel` |
| | `ok` | OK button (rightmost) | `buttons` is `Ok` or `OkCancel` |
| | `no` | No button | `YesNo`, `YesNoCancel` |
| | `cancel` | Cancel button | `OkCancel`, `YesNoCancel` |
| `InputBox`, `InputNumber` | `input` | The input box | Always |
| | `ok` | OK button | Always |
| | `cancel` | Cancel button | Always |
| `SelectBox` | `list` | The item list (scroll box) | Always |
| | `ok` | OK button (disabled until a row is selected) | Always |
| | `cancel` | Cancel button | Always |

A button that is not present returns `null`. The rows of the `SelectBox` list have no ids of their own; they lie inside the `list` rectangle, in item order from the top.

<a id="dialog-lifecycle"></a>
## Lifecycle and input

| Step | When · which thread | What happens |
|---|---|---|
| `ShowDialog` call | Immediately · calling thread | Constructor runs → registered on top of `App.Dialogs` (`IsOpen` = true). From this moment the page and areas below receive no presses or wheel |
| View creation | Next frame · input thread | Values being edited on the page/areas are **committed** and open popups close. Dialog view is created → `OnLoaded()` once |
| Open | Input thread | Only the topmost dialog receives input. A theme or language change rebuilds it like a page |
| `Close` · `CloseDialogs` | Immediately · any thread | Result fixed, `IsOpen` = false, removed from `App.Dialogs` at once. Dialogs stacked above close first, as canceled |
| Cleanup | Next frame · input thread | View detached and **values being edited inside it are discarded** (the result is fixed) → `OnClosed()` → result delivered (`await` continues). With several, top → bottom order |
| Window closes | Immediately · thread closing the window | All open dialogs close as canceled (edits discarded, popups close, `OnClosed()` runs on this thread). Awaiting code continues immediately on this thread. After that, `ShowDialog` calls no constructor and immediately returns a cancel result on the calling thread |

- **Dialogs stay shown when the page changes.** A dialog is owned by the app, not the page. If it must close, app code closes it (`CloseDialogs()` or a dialog's own method).
- `ShowDialog` called while the app is not attached to a window waits and appears on the first frame (a login dialog at startup); `await` waits until then too.
- Switching WinForms `ViewControl.App` to another app is detaching, not closing: dialogs remain and appear unchanged when that app is attached again, and code awaiting their results keeps waiting. `Dispose` on the control or closing the OpenTK window is closing.
- After the window closes, `DvApp.Current` keeps returning the last app; its `ShowDialog` returns cancel immediately without calling the constructor, so messages that arrive during shutdown end quietly.
- **Input.** A press outside the dialog is swallowed and the dialog does not close — always put a close button inside. Lower dialogs are under the overlay (`scrim`), dimmed, and receive no input. A dialog appearing while a press is held cancels that press (releasing does not click).
- **Double taps.** Blocking starts at registration, so a fast double press on a button does not show the same dialog twice (and repeated ACK taps do not open two confirm boxes). Showing several of the same dialog with different arguments on purpose is allowed.
- **Exceptions.** Exceptions from handlers, setters and value reads in the dialog view arrive at `App.HandlerError`, as with pages. An exception in `OnClosed()` is reported with `Kind` = `"closed"` (`Member` = the dialog type name), and closing and result delivery continue. If the dvml cannot be read as XML, the exception is thrown as-is in the frame that builds the view (developer error), and that dialog closes as canceled so `await` does not hang.
- **Not available:** closing by an outside press, drag-to-move, open/close animation, a title-bar X button, closing/confirming with physical Esc/Enter (the only exception is the edit-complete Enter in the built-in input boxes — [built-in boxes](#builtin-boxes)). Build closing with buttons inside the dialog.

<a id="dialog-patterns"></a>
## Patterns

All code below is complete files in `namespace MyApp`, and the tests compile them together with `Recipe` and `RecipeEditDialog` from [defining a dialog](#dialog-define).

**Pattern 1 — Confirmation.** Ask first for irreversible actions such as delete or write. A message box is enough.

```xml
<DvPageFrame vm="MyApp.RecipePage" title="Recipes">
  <DvCard title="Current recipe">
    <DvLabel class="title" text="{CurrentText}"/>
    <DvRow gap="sm">
      <DvButton variant="primary" text="Edit" onclick="EditRecipe"/>
      <DvButton variant="danger" text="Delete" onclick="DeleteRecipe"/>
    </DvRow>
  </DvCard>
</DvPageFrame>
```

```csharp
using Devinno.UI.App;

namespace MyApp;

public sealed partial class RecipePage : DvPage
{
    public Recipe? Current { get; private set; } = new("Default", 80.0);

    public string CurrentText => Current is { } r ? $"{r.Name} · {r.Temp:0.0} ℃" : "(none)";

    public async void DeleteRecipe()
    {
        if (Current is null)
            return;
        var answer = await App.MessageBox($"Delete recipe '{Current.Name}'?", "Delete", DvMessageButtons.YesNo);
        if (answer != DvDialogResult.Yes)
            return;                 // No, or closed from outside (Cancel)
        Current = null;
    }
}
```

**Confirming an alarm ACK.** The alarm widgets have no confirm attribute: ask with `App.MessageBox` in the `onack` handler and ACK only on Yes. Which alarms to ask about, the wording and permission checks are decided in the handler ([DvAlarmList](dvml-instruments.md#dvalarmlist)).

```xml
<DvPageFrame vm="MyApp.AlarmPage" title="Alarms">
  <DvAlarmList items="{Log.Rows}" onack="Ack"/>
</DvPageFrame>
```

```csharp
using Devinno.UI.Alarms;
using Devinno.UI.App;

namespace MyApp;

public sealed partial class AlarmPage : DvPage
{
    public AlarmLog Log { get; } = new();

    public async void Ack(AlarmRow row)
    {
        if (row.Priority == AlarmPriority.Critical &&
            await App.MessageBox($"Acknowledge alarm '{row.Message}'?", "Acknowledge", DvMessageButtons.YesNo) != DvDialogResult.Yes)
            return;                                   // No, or closed from outside (Cancel) = no ACK
        Log.Ack(row.Id);
    }
}
```

**Pattern 2 — Edit a copy.** The dialog edits a copy of the received value and, on OK, returns the new value as its result. Applying it to the original is the caller's job (data is the app's responsibility). On cancel the original is unchanged, so no undo code is needed. The dialog is `RecipeEditDialog` from [defining a dialog](#dialog-define).

```csharp
using Devinno.UI.App;

namespace MyApp;

public sealed partial class RecipePage
{
    public async void EditRecipe()
    {
        if (Current is null)
            return;
        Recipe? edited = await App.ShowDialog<RecipeEditDialog, Recipe>(Current);
        if (edited is null)
            return;                 // cancel
        Current = edited;           // the caller applies it to the original (the screen re-reads automatically)
    }
}
```

**Pattern 3 — Communication alarms and duplicate prevention.** A communication class is not a page, so it reaches the app through `DvApp.Current`. Start communication after the window opens: `DvApp.Current` throws until an adapter holds the app. To show only one window even if the same alarm arrives several times, give the dialog class **its own key property** (`AlarmId`) and check `App.Dialogs`. Do not compare by title or text (it breaks with localization or wording changes).

```xml
<DvDialogFrame vm="MyApp.AlarmDialog" title="Alarm" subtitle="{Code}" style="width:400">
  <DvLabel class="body" text="{Text}"/>
  <DvRow gap="sm">
    <DvSpacer/>
    <DvButton variant="primary" text="Acknowledge (ACK)" onclick="Ack"/>
  </DvRow>
</DvDialogFrame>
```

```csharp
using Devinno.UI.App;

namespace MyApp;

public sealed partial class AlarmDialog : DvDialog<bool?>
{
    public AlarmDialog(int alarmId, string text)
    {
        AlarmId = alarmId;
        Text = text;
    }

    public int AlarmId { get; }             // key for the duplicate check, defined by the dialog itself
    public string Text { get; }
    public string Code => $"#{AlarmId}";

    public void Ack() => Close(true);       // the user acknowledged
    public void Dismiss() => Close(false);  // closed from outside (communication), pattern 5
}
```

```csharp
using Devinno.UI.App;

namespace MyApp;

public sealed class PlcClient
{
    // Called on a communication thread (communication starts after the window opens; until then DvApp.Current throws).
    public void OnAlarmRaised(int alarmId, string text)
    {
        var app = DvApp.Current;
        if (app.Dialogs.OfType<AlarmDialog>().Any(d => d.AlarmId == alarmId))
            return;                                             // already shown: compare by key
        _ = WaitAckAsync(app, alarmId, text);
    }

    // The PLC cleared the alarm: close only that alarm's window (pattern 5)
    public void OnAlarmCleared(int alarmId)
    {
        DvApp.Current.Dialogs.OfType<AlarmDialog>().FirstOrDefault(d => d.AlarmId == alarmId)?.Dismiss();
    }

    private async Task WaitAckAsync(DvApp app, int alarmId, string text)
    {
        try
        {
            bool? acked = await app.ShowDialog<AlarmDialog, bool?>(alarmId, text);
            if (acked == true)
                await Task.Run(() => SendAck(alarmId));         // the continuation is on the input thread: hand off communication
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.TraceError($"Alarm {alarmId} ACK failed: {ex.Message}");
        }
    }

    private void SendAck(int alarmId) { /* write the ACK to the PLC */ }
}
```

- Between the duplicate check and the show, another thread could show the same alarm. With one communication thread this is no problem; with several, wrap those two lines in a `lock`.
- An alarm that arrives after the window has closed (program shutdown): `ShowDialog` immediately returns a cancel result (`null`), so it ends quietly without an exception. `DvApp.Current` keeps returning the last app after closing.

**Pattern 4 — Dialog on top of a dialog.** Showing another dialog from a dialog handler stacks it on top. Only the topmost receives input; the dialog below appears dimmed under the overlay. If the lower dialog closes first, the one above closes with it as canceled.

```xml
<DvDialogFrame vm="MyApp.ValveDialog" title="{ValveName}" subtitle="Valve control" style="width:360">
  <DvRow gap="sm">
    <DvSpacer/>
    <DvButton variant="secondary" text="Close" onclick="Cancel"/>
    <DvButton variant="primary" text="Open" onclick="RequestOpen"/>
  </DvRow>
</DvDialogFrame>
```

```csharp
using Devinno.UI.App;

namespace MyApp;

public sealed partial class ValveDialog : DvDialog<bool?>
{
    public ValveDialog(string valveName) => ValveName = valveName;

    public string ValveName { get; }

    public async void RequestOpen()
    {
        // the message box appears on top of this dialog
        if (await App.MessageBox($"Open {ValveName}?", "Confirm", DvMessageButtons.YesNo) == DvDialogResult.Yes)
            Close(true);
    }

    public void Cancel() => Close();
}
```

**Pattern 5 — Closing one specific dialog from outside.** `Close` is `protected`, so a dialog that must be closable from outside provides **its own public method** wrapping `Close` (`AlarmDialog.Dismiss()` in pattern 3). Outside code finds it by key in `App.Dialogs` and calls that method (`PlcClient.OnAlarmCleared`). It may be called from any thread. Use `App.CloseDialogs()` only to close all (all results are cancel).

<a id="lock-idle"></a>
## Lock and idle (Lock, Unlock, IdleTimeout, Idle)

Locks the screen so that no one else can operate it while the operator is away. **The library handles only the topmost lock layer and idle detection; the lock screen's appearance and password/card/permission verification are built by the app.**

- `App.Lock<TDialog>(args…)` shows an app-made `DvDialog<bool>` (login screen) in the **lock layer above all dialogs**. Until unlocked, pages, areas and other dialogs receive no input. The returned `Task` completes when unlocked.
- **NEVER show a lock screen with `ShowDialog`** — dialogs shown after it stack on top and bypass the lock.
- **How to unlock:** `Close(true)` inside the lock dialog when authentication succeeds. `Close(false)`, `Close()` and `App.CloseDialogs()` do not unlock (the lock stays). To unlock from outside the dialog (card reader signal, remote release), call `App.Unlock()` — it unlocks without verification, so the app authenticates first.
- **While locked:** drawing continues (instruments, trends, alarm blinking; `App.Invalidate()` is reflected). Dialogs shown after locking (communication alarms) stack **below** the lock layer and receive input after unlocking. The lock dialog does not appear in `App.Dialogs`. If already locked, `Lock` shows nothing new and returns the current lock's `Task`.
- `App.IdleTimeout` — if there is no input (touch, mouse, key, editing) for this duration, `App.Idle` fires **once** (input thread). `TimeSpan.Zero` (default) = off. It fires again after the same duration following new input. It does not count while locked and starts counting again once unlocked. What to do on idle (lock, go to home) is decided by the app. For automatic lock, call `Lock` from `App.Idle`.
- When the app (window) closes, the lock closes too and the `Task` completes.

```xml
<DvDialogFrame vm="MyApp.LockDialog" title="Locked: log in" style="width:360">
  <DvInput value="{Bind Password}" keyboard-type="password"/>
  <DvLabel text="{Error}" visible="{HasError}"/>
  <DvRow gap="sm">
    <DvSpacer/>
    <DvButton variant="primary" text="OK" onclick="Login"/>
  </DvRow>
</DvDialogFrame>
```

```csharp
namespace MyApp;

using Devinno.UI.App;

public sealed partial class LockDialog : DvDialog<bool>
{
    private readonly Func<string, bool> _check; // authentication is the app's job: server, card, local password, ...
    public LockDialog(Func<string, bool> check) => _check = check;

    public string Password { get; set; } = "";
    public string Error { get; set; } = "";
    public bool HasError => !string.IsNullOrEmpty(Error); // visible takes true/false — bind a bool, not the text

    public void Login()
    {
        if (_check(Password))
            Close(true);                       // this is the unlock
        else
        {
            Error = "Wrong password";          // on failure the lock stays
            Password = "";
        }
    }
}

public static class LockSetup
{
    public static void Use(DvApp app, Func<string, bool> check)
    {
        app.IdleTimeout = TimeSpan.FromMinutes(5);
        app.Idle += (_, _) => _ = app.Lock<LockDialog>(check);   // 5 minutes without input → lock
    }
}
```
