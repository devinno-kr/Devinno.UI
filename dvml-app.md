# Pages and the app (DvApp, DvPage, DvBar)

Covers: the app object (`DvApp`), pages (`DvPage`), fixed areas (`DvBar`), the complete app example, page lifecycle, the thread contract, current-page highlighting, global settings and hot reload, localization, icons, and the hosts (`ViewWindow`, `ViewControl`).
Read first: [the minimal complete screen](llm-rules.md#screen-shape) · [markup basics](dvml-language.md#markup-basics) · [binding syntax](dvml-language.md#binding)
Not here: [dialogs](dvml-dialogs.md#dialog-flow) · [`id` handles and the generator](dvml-code.md#code-id-vm) · [which redraw call to use (`Invalidate` / `Repaint`)](llm-rules.md#updates-threads) · [`HandlerError` kinds](dvml-language.md#handler-error) · [logical px and display scaling](dvml-layout.md#logical-px) · [`DvPageFrame` tag](dvml-widgets.md#dvpageframe)

<a id="dvapp"></a>
## DvApp: the app

In a program with several screens, a single `DvApp` holds and switches all screens. One app is one window. There are three building blocks.

| Type | Role |
|---|---|
| `DvApp` | Manager that holds pages and changes the current page. Applies theme, localization and component registry to all screens |
| `DvPage` | One screen. The class itself is the markup's VM (target of bindings and `onclick`) — see [DvPage](#dvpage) |
| `DvBar` | Always-visible fixed area (top bar, sidebar). Placed in `DvApp.Top` `Bottom` `Left` `Right` — see [fixed areas](#dvbar) |

Dialogs shown over pages (`DvDialog`) are also held by the app ([dialogs](dvml-dialogs.md#dialog-flow)).

```csharp
var app = new DvApp { Theme = DvTheme.Dark, Strings = strings };
app.Pages.Add(new HomePage());          // the first page added = start page
app.Pages.Add(new MonitorPage());
app.Pages.Add(new AlarmPage());
app.Top = new MainTopBar();             // optional; with no areas the page fills the window
app.HandlerError += (_, e) => Log.Error(e.Exception, $"{e.Kind} {e.Member}");
using var window = new ViewWindow(app, 1280, 800, "My HMI");
window.Run();
```

- **`Pages.Add(page)`** — adds the page and connects it to this app (`page.App` is filled). Adding the same instance again is ignored; a page that belongs to another `DvApp` throws `InvalidOperationException`. A page once added cannot be removed (it stays retained).
- **`Pages.Get<T>()`** — returns the one page of type `T` (including derived types). None, or more than one, throws `InvalidOperationException` (with several pages of the same class, use `Show(page)` with the instance).
- **Start page** — the first page added to `app.Pages` is the start page: while there is no current page, the first added page is shown. To start on a different page, call `app.Show(page)` prior to opening the window.
- **`CurrentPage`** — the page currently shown. Read-only, and it is the value **after** `Show` is applied (from the next frame, not right after the `Show` call). `null` while nothing has been shown.
- **`Show<T>()`** / **`Show(page)`** — change the current page. `Show(page)` first adds a page that is not in the list (`Pages.Add`). Showing the current page does nothing; a page of another app throws.
- **`DvApp.Current`** — reaches the app from code outside pages (communication classes). It works from `new ViewWindow(app, …)` (or assigning `ViewControl.App`) on, and throws prior to that. After the window closes it keeps returning the last app ([dialogs at shutdown](dvml-dialogs.md#dialog-lifecycle)).
- **Redraw** — `app.Invalidate()` (rebuild) and `app.Surface.Repaint()` (redraw only): which one to call and from where is in [the update table](llm-rules.md#updates-threads). Pages, bars and dialogs have no `Invalidate()` of their own — inside them call `App.Invalidate()`.
- **Errors** — subscribe to `app.HandlerError` once; it receives VM exceptions from all pages, areas and dialogs ([HandlerError](dvml-language.md#handler-error)).

<a id="dvpage"></a>
## DvPage: one screen

- A page is a `partial class` deriving from `DvPage` + a dvml file. The class itself is the VM (bindings and `onclick` resolve on it).
- The two are connected only by `vm="Namespace.Class"` on the dvml root (the file name does not matter; giving both the same name is the convention). A class declared without a namespace uses the bare class name (`vm="HomePage"`).
- The generator fills in the markup connection and the `id` fields ([what is generated](dvml-code.md#generated)).
- The usual root of a page dvml is `<DvPageFrame vm="…">` ([DvPageFrame](dvml-widgets.md#dvpageframe)). The C# class `DvPage` and the markup tag `DvPageFrame` are different things.
- Bars (`DvBar`) and dialogs (`DvDialog<T>`) are built the same way.

<a id="app-example"></a>
## Complete example

The smallest app: a top bar + 2 pages (Home → Monitoring → "Back") + one dialog (Monitoring's "Change setpoint"). It is **the exact same code** as `samples/Devinno.UI.AppSample` (a test compares them), and runs with `dotnet run --project samples/Devinno.UI.AppSample`.

`Program.cs`

```csharp
using Devinno.UI.App;
using Devinno.UI.OpenTK;
using Devinno.UI.Styling;

var app = new DvApp { Theme = DvTheme.Dark };
app.Top = new TopBar();                 // fixed area at the top (optional)
app.Pages.Add(new HomePage());          // the first page added = start page
app.Pages.Add(new MonitorPage());

using var window = new ViewWindow(app, 1024, 640, "App Sample");
window.Run();
```

`Pages/TopBar.cs`

```csharp
using Devinno.UI.App;

public sealed partial class TopBar : DvBar
{
    public string HomeClass => Tab<HomePage>();
    public string MonitorClass => Tab<MonitorPage>();

    public void ShowHome() => App.Show<HomePage>();
    public void ShowMonitor() => App.Show<MonitorPage>();

    private string Tab<T>() where T : DvPage => App.CurrentPage is T ? "tab on" : "tab";
}
```

`Pages/TopBar.dvml`

```xml
<DvRow vm="TopBar" style="height:56; padding:0 16; gap:8; background: token(surface)">
  <DvStyle>
    .tab { padding:8 18; border-radius: token(radius.md); color: token(text-muted) }
    .on { background: token(primary); color: token(primary-fg) }
  </DvStyle>
  <DvLabel class="title" text="App Sample"/>
  <DvSpacer/>
  <DvButton class="{HomeClass}" text="Home" onclick="ShowHome"/>
  <DvButton class="{MonitorClass}" text="Monitoring" onclick="ShowMonitor"/>
</DvRow>
```

`Pages/HomePage.cs`

```csharp
using Devinno.UI.App;

public sealed partial class HomePage : DvPage
{
    public void OpenMonitor() => App.Show<MonitorPage>();
}
```

`Pages/HomePage.dvml`

```xml
<DvPageFrame vm="HomePage" title="Home" subtitle="Switch pages with the button or the top bar">
  <DvRow>
    <DvButton variant="primary" text="Open monitoring" onclick="OpenMonitor"/>
  </DvRow>
</DvPageFrame>
```

`Pages/MonitorPage.cs`

```csharp
using Devinno.UI.App;

public sealed partial class MonitorPage : DvPage
{
    public double Temperature { get; set; } = 24.5;
    public double Setpoint { get; private set; } = 25.0;
    public int ShownCount { get; private set; }

    public void Back() => App.Show<HomePage>();

    public async void EditSetpoint()
    {
        double? value = await App.ShowDialog<SetpointDialog, double?>(Setpoint);
        if (value is null)
            return;                 // Cancel button, or closed from outside
        Setpoint = value.Value;     // continues on the input thread: set the VM directly and the screen re-reads it in the same frame
    }

    protected override void OnShown() => ShownCount++;   // every time it is shown
}
```

`Pages/MonitorPage.dvml`

```xml
<DvPageFrame vm="MonitorPage" title="Monitoring" subtitle="Shown: {ShownCount}">
  <DvRow>
    <DvButton variant="ghost" text="← Back" onclick="Back"/>
  </DvRow>
  <DvCard title="Water temperature">
    <DvLabel class="title" text="{Temperature} ℃"/>
    <DvRow>
      <DvLabel class="body muted" text="Setpoint {Setpoint} ℃"/>
      <DvButton variant="secondary" text="Change setpoint" onclick="EditSetpoint"/>
    </DvRow>
  </DvCard>
</DvPageFrame>
```

`Pages/SetpointDialog.cs` — the dialog opened by "Change setpoint". It shows the pattern for building your own dialog; in a real app a single number like this should use the built-in `App.InputNumber` ([built-in boxes](dvml-dialogs.md#builtin-boxes)).

```csharp
using Devinno.UI.App;

public sealed partial class SetpointDialog : DvDialog<double?>
{
    public SetpointDialog(double current) => Value = current;   // edit a copy of the value, not the original

    public double Value { get; set; }

    public void Ok() => Close(Value);
    public void Cancel() => Close();                            // result = null (cancel)
}
```

`Pages/SetpointDialog.dvml`

```xml
<DvDialogFrame vm="SetpointDialog" title="Setpoint" subtitle="0 ~ 60 ℃" style="width:360">
  <DvInput value="{Bind Value}" keyboard-type="numeric" min="0" max="60" format="0.0" unit="℃"/>
  <DvRow gap="sm">
    <DvSpacer/>
    <DvButton variant="secondary" text="Cancel" onclick="Cancel"/>
    <DvButton variant="primary" text="OK" onclick="Ok"/>
  </DvRow>
</DvDialogFrame>
```

- The bar button highlight binds a computed property (`HomeClass`) that reads `App.CurrentPage` to `class` ([highlighting the current page](#current-page)).
- `MonitorPage` is retained, so each time it is shown again `ShownCount` increases and the screen re-reads it ([page lifecycle](#page-lifecycle)).
- "Change setpoint" opens `SetpointDialog` and waits for the result with `await`. OK returns the new value (`double`), Cancel returns `null`. `Setpoint` changed after `await` appears immediately in the screen's "Setpoint … ℃" without any notification ([after `await`](dvml-dialogs.md#dialog-show)).

<a id="page-lifecycle"></a>
## Page lifecycle

- **Retention.** A page's screen is built once, the first time it is shown, and retained. Going to another page and coming back keeps scroll position, input values, `Click` subscriptions and control values written from code. There is no operation to close or discard a page.
- **Re-reading.** Bindings re-read the VM every time the page is shown again, so values changed while hidden and values changed in `OnShown` appear immediately.
- **Notification order.** When `Show(b)` is applied: previous page `OnHidden` → (if b's screen is new: build → generator-made `OnLoaded()` once) → b's `OnShown`. `OnLoaded()` runs once in a page's lifetime.
- **Thread.** `OnShown`, `OnHidden` and `OnLoaded` are always called on the input (UI) thread.
- **Input in progress on the leaving page.** On a transition, values being edited on the leaving page are **committed** (`OnHidden` sees the committed values), and open popups and the virtual keyboard close. On return, input values are unchanged.
- **`App`.** Usable after `Pages.Add` (or `Show(page)`). Reading it prior to that (including inside the page constructor) throws `InvalidOperationException`, so put code that uses `App` in handlers, `OnLoaded` or `OnShown`. It is not null there; use it directly (`App.Show<T>()`).
- **Notification exceptions.** Exceptions thrown by `OnShown`/`OnHidden` are reported via `HandlerError` (`Kind` = `"shown"`/`"hidden"`, `Member` = page class name), and the transition continues.
- **Markup errors.** If a page's dvml cannot be read as XML, the exception is thrown as-is at the moment the page is first shown (developer error). The current page does not change, but the previous page's `OnHidden` has already run and values being edited have already been committed.

<a id="threads"></a>
## Threads and requests

This page owns the thread contract. [llm-rules](llm-rules.md#updates-threads) has the short "what to call" table only.

`Show`, area assignment (`Top`, etc.), setting `Theme`/`Strings`/`Registry`, and `Invalidate` can be called **from any thread**. Requests are queued and applied on the input thread at the adapter's next frame; if called several times in between, only the last one is applied.

```csharp
// on a communication thread
void OnAlarmReceived(AlarmPriority p, string msg)
{
    alarmLog.Raise(p, msg);
    app.Show<AlarmPage>();     // safe even while rendering; switches at the next frame
}
```

The **input thread** is the thread on which the adapter handles layout, drawing, pointer and keys (the UI thread of the OpenTK window or WinForms control).

| What | Which thread | Notes |
|---|---|---|
| `DvApp.Show`·`Show<T>` (including the transition argument)·`PageTransition`·`Top`/`Bottom`/`Left`/`Right`·`Theme`·`Strings`·`Registry`·`HotReload` | Any thread | Queued, applied on the input thread at the next frame |
| `DvApp.Invalidate`·`DvSurface.Invalidate`·`Repaint` | Any thread | Only sets a flag. Bindings are re-read at the next frame |
| `ShowDialog`·`MessageBox`·`InputBox`·`InputNumber`·`SelectBox`·`CloseDialogs`·`DvDialog.Close`·`DvDialog.IsOpen`·`Dialogs` (incl. `Dialogs.Top`) | Any thread | The result `Task` completes on the input thread. Awaiting on the input thread continues on the input thread |
| `Lock`·`Unlock`·`IsLocked`·`DvApp.Current`·`Pages` | Any thread | |
| Writing handle values (`Checked`·`Value`·`SelectedIndex`·`Expanded`·`Text`·`Enabled`, etc.) | Any thread | Schedules a repaint. If the value changes, the changed event (`ByUser = false`) fires **on the writing thread** |
| Events raised by user interaction, `onclick` handlers | Called on the input thread | Screen values may be changed directly inside the handler |
| `TrendBuffer.Add`·`AlarmLog` (all members, incl. `Raise`/`Ack`/`AckAll`) | Any thread | Internal locking. Enumerating `AlarmLog` gives a snapshot of that moment |
| `Post` on the input-thread `SynchronizationContext` (`SynchronizationContext.Current` inside handlers) | Any thread | |
| Writing view model properties | App's responsibility | The library only reads. `Invalidate` after writing reflects it at the next frame |
| `DvSurface.Layout`·`RenderTo`·`Pointer*`·`Key*`·`Text*`, `LayoutBox`·`DvEditState` | Input thread only | Called by the adapter. Apps normally never call them |
| `Send` on the input-thread `SynchronizationContext` | Input thread only | Calling from another thread throws `NotSupportedException` |

The "Any thread" items are verified by a stress test (`ThreadStressTests`): several threads call them concurrently while the input thread keeps running layout, drawing and pointer handling.

Rules that follow from the table:

- **After `await`.** Handler code (`onclick` etc.) that `await`s continues on the input thread in the frame the result arrives, so it may use the VM directly. Changed values are redrawn automatically in that frame; `App.Invalidate()` is not needed. Only values changed from timer or communication threads need `App.Invalidate()`.
- **Awaiting on a communication thread** also continues on the input thread. Hand off heavy work after that (communication, files) with `Task.Run`; otherwise the screen freezes meanwhile.
- **Alarms and trends** — `AlarmLog.Raise`/`Ack`/`AckAll` and `TrendBuffer.Add` are safe from any thread; after `Raise`/`Ack` from a communication thread call `Invalidate()`. All other UI work runs on the single UI thread.
- **Page switch from a communication thread** — `App.Show<AlarmPage>()` there is fine (queued).
- **Dialogs** — `ShowDialog`, `MessageBox`, `CloseDialogs`, reading `Dialogs`, and `Close` inside a dialog work from any thread; view creation/cleanup and result delivery happen on the UI thread at the next frame ([dialog lifecycle](dvml-dialogs.md#dialog-lifecycle)).
- **Handle writes** — in pages and areas inside `DvApp`, writing handle values (`btn.Enabled = false`) also wakes the window. Only hosts that use `DvSurface` directly must call `Invalidate()` separately after writing ([writing values](dvml-code.md#code-writes)).
- **Values that belong together.** Bindings are read on the UI thread during layout, possibly while a timer thread writes. When several properties belong together (value + state text + color), update them as one unit: write them under a `lock` that the getters also take, or put them in an immutable snapshot object and assign the whole object (`Live = new PlantData(...)`, bound as `{Live.Temp}`), then call `Invalidate()`. A single `double`/`bool`/`string` property needs nothing extra.
- **Grid lists** — do not change the list from another thread while the grid is drawing ([grid updates](dvml-grid.md#grid-scroll-updates)).
- **Process diagrams** follow the same rules: after a VM change on a communication/timer thread call `App.Invalidate()`; handlers and code after their `await` need no `Invalidate`.

<a id="dvbar"></a>
## Fixed areas (DvBar)

Place a `DvBar` in `app.Top`, `Bottom`, `Left` or `Right`. All are optional.

- **The bar's dvml root sets its size.** `Top`/`Bottom` are full window width × the root's **height**; `Left`/`Right` are the height between the top and bottom areas × the root's **width**. Give the root a size such as `style="height:56"` or `style="width:220"` (if not given, the content size). The page fills the remaining space.
- With no areas at all, the page is the whole window and `Bounds` stays relative to the window.
- **Areas larger than the window** receive only what remains, in the order `Top` → `Bottom`, `Left` → `Right`.
- **Bars do not contain pages.** Their buttons only **request** `App.Show<T>()`. Bars have `App` (from the moment they are assigned to an area; reading it prior to that or inside the constructor throws `InvalidOperationException`) and `OnLoaded()`, but no `OnShown`/`OnHidden` (always visible).
- **One instance = one slot.** Placing the same bar in two areas at once throws `InvalidOperationException` (to move it, first set the original area to `null`). A bar that belongs to another `DvApp` also throws.
- **A bar removed from an area belongs to that app.** It keeps its screen and state (input values, scroll); placing it again in an area of the same app shows it unchanged.
- Dropdown/picker popups and the virtual keyboard are drawn relative to the whole window, so a top bar's `DvSelect` list unfolds over the page area without clipping.
- The pointer goes to the screen of the cell where the press started; while dragging, that cell keeps receiving it until release.

<a id="current-page"></a>
## Highlighting the current page

Selection indication = a class binding that reads `App.CurrentPage`; page switching = `App.Show`. For a bar button to indicate the current page, bind a computed property that reads `App.CurrentPage` to its `class`. When the page changes (also via `App.Show` from code), the app re-reads the areas at the next frame. `DvApp.Invalidate()` rebuilds the current page, fixed areas and open dialogs.

```csharp
// MainTopBar.cs
public sealed partial class MainTopBar : DvBar
{
    public string MonitorClass => TabClass<MonitorPage>();
    public string AlarmClass   => TabClass<AlarmPage>();

    public void ShowMonitor() => App.Show<MonitorPage>();
    public void ShowAlarm()   => App.Show<AlarmPage>();

    private string TabClass<T>() where T : DvPage => App.CurrentPage is T ? "tab tab-active" : "tab";
}
```

Top bar `MainTopBar.dvml` — a single row of height 56, with the current page's tab highlighted.

```xml
<DvRow vm="MainTopBar" style="height:56; padding:0 16; gap:8; background: token(surface); border: 1 token(border)">
  <DvStyle>
    .tab { padding:8 18; border-radius: token(radius.md); color: token(text-muted); font-size: token(font.body) }
    .tab:hover { background: token(surface-raised); color: token(text) }
    .tab-active, .tab-active:hover { background: token(primary); color: token(primary-fg) }
  </DvStyle>
  <DvLabel class="title" text="Devinno.UI"/>
  <DvSpacer/>
  <DvButton class="{MonitorClass}" text="Monitoring" onclick="ShowMonitor"/>
  <DvButton class="{AlarmClass}" text="Alarms" onclick="ShowAlarm"/>
</DvRow>
```

A "Back" button on a page is a plain `onclick` (`Back()` is in the [complete example](#app-example)). To show it only when not on home, bind `visible` to a bool (`public bool ShowBack { get; set; }`).

```xml
<DvPageFrame vm="MonitorPage" title="Monitoring">
  <DvRow visible="{ShowBack}">
    <DvButton variant="ghost" text="← Back" onclick="Back"/>
  </DvRow>
  <DvCard title="Water temperature">
    <DvLabel class="title" text="{Temperature} ℃"/>
  </DvCard>
</DvPageFrame>
```

The left sidebar `MainSideBar.dvml` is a vertical list of width 220. The C# side follows the same rule (`NavClass<T>()` reads `App.CurrentPage is T`).

```xml
<DvStack vm="MainSideBar" gap="xs" padding="lg" style="width:220; background: token(surface); border: 1 token(border)">
  <DvStyle>
    .nav { padding:10 14; border-radius: token(radius.md); color: token(text-muted); font-size: token(font.body); content-align: middle-left }
    .nav:hover { background: token(surface-raised); color: token(text) }
    .nav-active, .nav-active:hover { background: token(primary); color: token(primary-fg) }
  </DvStyle>
  <DvLabel class="title" text="Devinno.UI"/>
  <DvBox style="height:12"/>
  <DvButton class="{OverviewClass}" text="Overview" onclick="ShowOverview"/>
  <DvButton class="{AlarmClass}" text="Alarms" onclick="ShowAlarm"/>
  <DvButton class="{TrendClass}" text="Trend" onclick="ShowTrend"/>
  <DvSpacer/>
  <DvLabel class="caption muted" text="{CurrentText}"/>
</DvStack>
```

`CurrentText` is `$"Current: {App.CurrentPage?.GetType().Name}"`. The complete bars example is `samples/Devinno.UI.Demo/Bars/`; the demo runs with `-- top` (top bar) or `-- nav` (sidebar).

<a id="global-settings"></a>
## Global settings and page transitions

- Changing `app.Theme`, `app.Strings` or `app.Registry` applies to **all** retained page and area screens. The same screens are rebuilt (no new screens), so scroll position, values being edited, input values, control values written from code (`btn.Enabled = false`) and `Click` subscriptions are kept. Callable from any thread; applied at the next frame.
- **Page transition animation:** with `app.PageTransition = DvPageTransition.Fade` (or `Slide`), changing pages animates for about 0.2 seconds. Default `None` (instant).
- One-off transition: `App.Show<SettingsPage>(DvPageTransition.Slide)` / `App.Show(page, DvPageTransition.Fade)`.
- `Fade` = the new page gradually becomes opaque over the old page; `Slide` = the new page pushes in from the right and the old page exits to the left. During the animation both pages are drawn, and input goes to the new page from the start. The first page does not animate. Areas (`DvBar`) and dialogs do not animate.

<a id="hot-reload"></a>
## Hot reload

With `app.HotReload = true`, the dvml files of pages, areas and dialogs are watched; on save that screen is swapped for the new markup immediately (no app restart). Enable it only in debug builds:

```csharp
#if DEBUG
app.HotReload = true;   // do not enable in release builds
#endif
```

- Applied at the next frame within about 0.15 seconds after saving. VM objects and values, `id` handles and values written by handles are kept; `OnLoaded()` is not called again. The screen is rebuilt as with `Invalidate()`.
- Only markup changes hot-reload. Anything that needs C# changes — new `id` handle fields, new VM properties or handlers — requires a rebuild (bindings to missing properties are empty values).
- Element-based state the screen remembers (tab index without `value`, expander/tree expansion) resets to initial.
- Saving invalid XML keeps the previous screen and reports via `HandlerError` (`Kind` = `"hotreload"`, `Member` = file path). Fix and save again to reapply.
- File paths are absolute paths embedded by the source generator at build time, so it works only on the PC that built the app. Components and styles registered from code are not watched.
- To see changes in the running app while developing, this is the tool ([verify in the app](llm-rules.md#verify-app)).

<a id="localization"></a>
## Localization

```xml
<DvPageFrame title="{L10n.title}" font-lang-ko="Malgun Gothic" font-lang-ja="Yu Gothic UI" font-lang-en="Segoe UI">
  <DvLabel class="body" text="@pump"/>                    <!-- shorthand for {L10n.pump}; "@@x" = the literal text "@x" -->
  <DvLabel class="body" text="{L10n.pump} #1 {Status}"/>  <!-- mixed with a VM binding -->
</DvPageFrame>
```

- Set `DvStrings` on the app (`app.Strings = strings`, from any thread): `{L10n.key}` and `@key` are replaced with strings in the current language. Changing the language redraws the screen automatically.
- Lookup order for app keys: current language → `FallbackLanguage` → shows `[key]` (never silently blank).
- `@key` works only when the whole attribute value has the form `@name` (name = ASCII letters, digits, `_`, `.`). `@@x` is the literal text `@x`.
- `font-lang-{language}` on the root = the default font for that language (elements that set `font-family` keep their own).
- **Built-in widget text** (alarm columns, input errors, picker and dialog buttons, filter popup, virtual keyboard "Done") ships in **English (default) and Korean**. With no `DvStrings`, or with `Language = "en"`, it is English; set `Language = "ko"` for Korean (`app.Strings = new DvStrings { Language = "ko" }`).
- For another language, or to change a built-in text, add the `dv.*` key to your table. Your entries in the current language win over the built-in text. Lookup order for `dv.*` keys: your table (current language) → built-in (current language) → your table (`FallbackLanguage`) → built-in (`FallbackLanguage`) → English. So an entry only in your fallback language loses to built-in Korean when `Language = "ko"`.
- The virtual keyboard's Korean/English switch key is always shown.

<!-- data:ko -->
| Key | English (default) | Korean (`ko`) |
|---|---|---|
| `dv.dialog.ok` `dv.dialog.cancel` `dv.dialog.yes` `dv.dialog.no` | OK / Cancel / Yes / No | 확인 / 취소 / 예 / 아니요 |
| `dv.alarm.critical` `dv.alarm.major` `dv.alarm.minor` | Critical / Major / Minor | 긴급 / 경고 / 주의 |
| `dv.alarm.none` `dv.alarm.empty` | No active alarms / No alarms to show | 활성 알람 없음 / 표시할 알람이 없습니다 |
| `dv.alarm.unacked` `dv.alarm.acked` | {0} unacknowledged / Acked {0} | 미확인 {0}건 / 확인 {0} |
| `dv.alarm.comm` | Communication fault | 통신 이상 |
| `dv.alarm.saturated` (the alarm `AlarmLog` adds when its buffer is full; other alarm messages are shown as the app wrote them) | Alarm buffer saturated: acknowledge (ACK) the unacknowledged alarms | 알람 버퍼 포화 — 미확인 알람을 확인(ACK)하세요 |
| `dv.alarm.col.time` `dv.alarm.col.priority` `dv.alarm.col.message` `dv.alarm.col.ack` | Time / Priority / Message / Ack | 시간 / 등급 / 메시지 / 확인 |
| `dv.alarm.ack` (row button) | ACK | 확인 |
| `dv.trend.empty` | No data | 데이터 없음 |
| `dv.input.required` `dv.input.number` `dv.input.integer` `dv.input.unsigned` | Enter a value / Not a number / Only whole numbers are allowed / Negative numbers are not allowed | 값을 입력하세요 / 숫자 형식이 아닙니다 / 정수만 입력할 수 있습니다 / 음수는 입력할 수 없습니다 |
| `dv.input.range` `dv.input.overflow` `dv.input.rejected` | Allowed range: {0} to {1} / Out of the allowed range / This value is not allowed | 허용 범위: {0} ~ {1} / 입력할 수 있는 범위를 벗어났습니다 / 허용되지 않는 값입니다 |
| `dv.input.write-failed` (VM setter exception) | Could not save the value | 값을 저장하지 못했습니다 |
| `dv.picker.ok` `dv.picker.cancel` | OK / Cancel | 확인 / 취소 |
| `dv.picker.month` `dv.picker.dow` | {0}-{1:00} / Sun,Mon,Tue,Wed,Thu,Fri,Sat | {0}년 {1}월 / 일,월,화,수,목,금,토 |
| `dv.picker.hex` | Not in #RRGGBB format | #RRGGBB 형식이 아닙니다 |
| `dv.kbd.done` | Done | 완료 |
| `dv.filter.ok` `dv.filter.cancel` `dv.filter.clear` `dv.filter.all` | OK / Cancel / Clear / Select all | 확인 / 취소 / 지우기 / 전체 선택 |
| `dv.filter.empty` `dv.filter.true` `dv.filter.false` `dv.filter.more` | (Empty) / Yes / No / Too many values; showing the first {0} | (빈 값) / 예 / 아니오 / 값이 많아 처음 {0}개만 표시 |
| `dv.filter.min` `dv.filter.max` `dv.filter.from` `dv.filter.to` | Min / Max / From / To | 최소 / 최대 / 시작 / 끝 |

<!-- /data:ko -->

<a id="icons"></a>
## Icons (Font Awesome 6 Free)

Used by `DvIcon icon`, `DvButton icon`, `DvOption icon`, `item-icon` of `DvSelect`/`DvSegment`/`DvListBox`, and `DvColumn icon`.

| Notation | Font |
|---|---|
| `fa-gear`, `fas fa-gear`, `fa-solid fa-gear` | Solid |
| `far fa-star`, `fa-regular fa-star` | Regular (some icons only) |
| `fab fa-windows`, `fa-brands fa-windows` | Brands |

- Names are Font Awesome 6.1.2 Free names (https://fontawesome.com/search?o=r&m=free).
- Unknown names are not drawn (one Trace warning).
- Size = the element's `font-size`; color = `color`.
- Use brand logos only to refer to that brand.

```xml
<DvIcon icon="fa-triangle-exclamation" style="font-size:32; color:token(tone-warning)"/>
<DvButton text="Save" icon="fa-floppy-disk" variant="primary" onclick="Save"/>
```

<a id="hosts"></a>
## Hosts

The adapter (window or control) decides the window/control size; the markup does not decide the window, and the `DvApp` areas divide the space inside it. For detailed host usage, see each type's XML documentation.

| Host | Purpose | Refresh |
|---|---|---|
| `ViewWindow` (Devinno.UI.OpenTK) | OpenTK window. Touch-first (virtual keyboard). Sleeps when there is no input, using almost no CPU. `Gpu = true` draws with OpenGL. Constructor options `gles`, `fullscreen`, `fit` for embedded panels — see [ViewWindow](#viewwindow) | `Invalidate()`, `Repaint()` |
| `ViewControl` (Devinno.UI.WinForms) | WinForms control. `App = app`. Edits through a hidden TextBox by default; virtual keyboard in `TouchMode`. CPU raster by default; `Gpu = true` draws with OpenGL — see [ViewControl](#viewcontrol) | `MarkUpdated()`, `MarkRepaint()` |
| `DvSurface` | For hosts you build yourself (engine API): drawing to a canvas and pointer input | `Invalidate()`, `Repaint()` |
| `DvRenderer` | Markup → PNG (check output without a window) | — |

```csharp
// OpenTK
using var window = new ViewWindow(app, 1280, 800, "My HMI") { Gpu = true };   // Gpu is optional
window.Run();

// OpenTK: a 1280×750 screen shown fullscreen on a GLES touch panel (1024×600)
using var panel = new ViewWindow(app, 1280, 750, "HMI", gles: true, fullscreen: true, fit: true);
panel.Run();

// WinForms
var view = new ViewControl { Dock = DockStyle.Fill, TouchMode = false, App = app };
form.Controls.Add(view);
```

- Both adapters take a single `DvApp`: `new ViewWindow(app, width, height, title)` / `ViewControl.App`.
- The engine APIs `new DvSurface(markup, vm …)` and `DvRenderer` are available for custom hosts and headless output (pass the generated `Markup` constant — [what is generated](dvml-code.md#generated)).
- Localization is `app.Strings`; the theme is `app.Theme`.
- **One adapter per app, one app per process.** An app attaches to only one window/control at a time; giving it to a second adapter throws `InvalidOperationException`. Only one app per process can be attached (attaching another app while one is attached throws `InvalidOperationException`). When the window closes the app detaches and can attach to another adapter.
- Changing `ViewControl.App` or closing the window detaches the previous app. Closing the window or calling `Dispose` on the control closes open dialogs as canceled; merely switching the app leaves dialogs open ([dialog lifecycle](dvml-dialogs.md#dialog-lifecycle)).
- `DvApp.Surface` (the root screen to draw) and `DvApp.RenderRequested` (repaint notification) are public for adapters; ordinary apps only use `ViewWindow`/`ViewControl` and never touch them.
- All three hosts report VM code exceptions through the `HandlerError` event. With `DvApp`, `app.HandlerError` alone is enough ([HandlerError](dvml-language.md#handler-error)).
- Display scaling (DPI) and logical px: [logical px](dvml-layout.md#logical-px).

<a id="viewwindow"></a>
### ViewWindow (OpenTK)

- `window.Run()` blocks until the window closes. Start communication between `new ViewWindow(...)` and `Run()`, and stop it after `Run()` returns.
- `gles: true` creates an OpenGL ES 3.0 context instead of desktop OpenGL 3.3 (boards that only have GLES, such as linux-arm64 touch panels).
- `fullscreen: true` opens a borderless window filling the current monitor (kiosk; there is no close button, so the app provides its own way to exit). With `fullscreen`, `width`/`height` are used only as the `fit` layout size.
- `fit: true` always lays the screen out at `width`×`height` and scales it to the window keeping the aspect ratio. When the ratios differ it is centered with margins in the theme `background` color (the same as the screen canvas). Pointer coordinates and `Bounds` stay in that layout size, so pages behave as if the window were exactly `width`×`height`.
- Use `fit` only to show one fixed screen on a panel of a different resolution. For windows that change size, keep the layout responsive (`fit` shrinks text along with everything else).

<a id="viewcontrol"></a>
### ViewControl (WinForms)

- Assign the app with `ViewControl.App = app`. Edits go through a hidden TextBox by default; `TouchMode = true` uses the virtual keyboard.
- Draws with the CPU raster by default; `Gpu = true` draws with OpenGL.
- Display scaling: WinForms receives per-monitor scaling when the app is `PerMonitorV2` (`<ApplicationHighDpiMode>`). Resizing the Form for the scale is the form's job. For a form built in code, do it like designer code: `SuspendLayout()` → `AutoScaleDimensions = new SizeF(96, 96)`, `AutoScaleMode = AutoScaleMode.Dpi`, `ClientSize` (at 100%) → `ResumeLayout(false)`. Otherwise, at 150% the window keeps its size while the content grows and gets clipped.
