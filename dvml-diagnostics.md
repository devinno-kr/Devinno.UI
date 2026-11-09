# DVML diagnostics

Covers: build diagnostics DVML001–021 (severity, meaning, what is checked, how to fix), the runtime Trace warning index (message form → cause → page that owns the rule), and when the screen draws magenta.
Read first: [markup basics](dvml-language.md#markup-basics) · [build, then check the output](llm-rules.md#verify-build)
Not here: [process diagram runtime messages in full](dvml-process-layout.md#process-diagnostics) · [handler exceptions and `HandlerError` kinds](dvml-language.md#handler-error) · [symptom → cause → fix table](llm-rules.md#symptoms) · [theme token list](dvml-style.md#theme-tokens) · [project setup and the generator](dvml-code.md#project-setup)

<a id="build-diagnostics"></a>
## Build diagnostics

The source generator reports problems in each `*.dvml` file at compile time. The location points inside the dvml file. Messages are in English; when the compiler's UI language is Korean (Korean Visual Studio, or `dotnet build` on a Korean OS), they are shown in Korean.

| ID | Severity | Title | Checked in |
|---|---|---|---|
| [DVML001](#dvml001) | Error | dvml XML syntax error | every dvml |
| [DVML002](#dvml002) | Error | Duplicate id | every dvml |
| [DVML003](#dvml003) | Error | id is not a C# identifier | every dvml |
| [DVML004](#dvml004) | Warning | id inside a repeat or component | every dvml |
| [DVML005](#dvml005) | Error | vm type not found | dvml with `vm` |
| [DVML006](#dvml006) | Error | vm type is not partial | dvml with `vm` |
| [DVML007](#dvml007) | Error | Several dvml files name the same vm | dvml with `vm` |
| [DVML008](#dvml008) | Error | Process diagram name not found | every dvml |
| [DVML009](#dvml009) | Error | Process diagram port error | every dvml |
| [DVML010](#dvml010) | Error | Duplicate process diagram name | every dvml |
| [DVML011](#dvml011) | Warning | Unknown value in process diagram | every dvml |
| [DVML012](#dvml012) | Warning | Unknown style property | every dvml |
| [DVML013](#dvml013) | Warning | Unknown theme token | every dvml |
| [DVML014](#dvml014) | Warning | Unknown class | every dvml |
| [DVML015](#dvml015) | Warning | DvRadio without option | every dvml |
| [DVML016](#dvml016) | Warning | DvListBox value attribute does not match its mode | every dvml |
| [DVML017](#dvml017) | Warning | Unknown widget attribute value | every dvml |
| [DVML018](#dvml018) | Error | Handler method not found | dvml with `vm` |
| [DVML019](#dvml019) | Warning | Unknown attribute | every dvml |
| [DVML020](#dvml020) | Warning | Attribute value is not valid for its kind | every dvml |
| [DVML021](#dvml021) | Error | Component name is a built-in tag | every dvml |

Scope rules:
- "every dvml" includes dvml files without `vm`.
- An error from DVML001–003 stops VM code generation for that file (no fields, no DVML005/006/018 check). The other errors (DVML008–010, DVML021) do not block VM code generation.
- Values containing a binding (`{…}`) are decided at run time and are not checked, except where an ID says otherwise.
- Process diagrams: only what is certain at build time is checked. Values with bindings and equipment whose ports are unknown at build time (`DvSymbol` registered in code, component units) are skipped and reported only through Trace at run time ([process diagnostics](dvml-process-layout.md#process-diagnostics)).
- Tokens of a theme built in C#, `<DvStyle>` of components registered in code, and components registered in C# are not visible to the build. Turn the affected check off in `.editorconfig` with `dotnet_diagnostic.DVML013.severity = none` (or `DVML014`, `DVML018`; see each ID).

What to do after every markup edit: build, then fix every error and every DVML012, DVML013 and DVML014 warning — at run time those are only silently ignored or drawn magenta ([verify by building](llm-rules.md#verify-build)).

<a id="dvml001"></a>
### DVML001 — dvml XML syntax error (Error)

- Meaning: the dvml could not be read as XML (unclosed tag, `&` not written as `&amp;`, missing attribute quotes).
- Effect: no VM code is generated for that file.
- Fix: correct the XML at the reported line.

<a id="dvml002"></a>
### DVML002 — Duplicate id (Error)

- Meaning: the same `id` appears twice in one dvml. An `id` can be used only once per file.
- Fix: give one of them another name ([`id` and handles](dvml-code.md#code-id-vm)).

<a id="dvml003"></a>
### DVML003 — id is not a C# identifier (Error)

- Meaning: the `id` cannot be a C# field name (it must start with a letter or `_`, and must not be a keyword).
- Fix: use a valid identifier such as `pump1`.

<a id="dvml004"></a>
### DVML004 — id inside a repeat or component (Warning)

- Meaning: an `id` inside a `DvRepeat` or `DvComponent` template is copied per item/instance, so no field is generated.
- Fix: use bindings, or an `onclick` method that takes the item as its parameter (`onclick="Select"` → `public void Select(Item item)`). An `id` on a component **instance** works ([handle limits](dvml-code.md#code-limits)).

<a id="dvml005"></a>
### DVML005 — vm type not found (Error)

- Meaning: the `vm` type does not exist in the compilation, or it is nested, generic or not a class.
- Fix: give a top-level, non-nested, non-generic class as `vm="Namespace.Class"`.

<a id="dvml006"></a>
### DVML006 — vm type is not partial (Error)

- Meaning: the `vm` type is not `partial`, so the generated fields cannot be added.
- Fix: declare it as `partial class`.

<a id="dvml007"></a>
### DVML007 — Several dvml files name the same vm (Error)

- Meaning: more than one dvml names the same `vm` type. One VM is connected to only one dvml.
- Fix: give each dvml its own VM class.

<a id="dvml008"></a>
### DVML008 — Process diagram name not found (Error)

- Meaning: a name referenced in a process diagram does not exist in that diagram (or unit): a `DvLine`/`DvSignal` `from`/`to`, a `DvInstrument on`/`tap`, or a signal end naming no equipment or instrument.
- Also reported: a `DvInstrument` with neither `on` nor `tap`; a `DvInstrument on` that is not equipment; an instrument used as a pipe end (instruments connect with `DvSignal`).
- A missing `name` is not checked at build time.
- Fix: match the name exactly ([connections](dvml-process.md#connections)).

<a id="dvml009"></a>
### DVML009 — Process diagram port error (Error)

- Meaning: a nonexistent port; a port required but not chosen; a face that is not `top` `bottom` `left` `right`, or a position after `@` that is not a number 0–1; a port or endpoint on a signal line (signal lines use names only); a terminal or unit port on a signal line; connecting directly from outside to equipment inside a unit.
- Fix: use a port the message lists; connect into a unit through its `<DvPort>` as `unit.port` ([ports](dvml-process-equipment.md#ports)).

<a id="dvml010"></a>
### DVML010 — Duplicate process diagram name (Error)

- Meaning: two pieces of equipment or instruments share a name within one process diagram or one unit.
- Fix: make names unique within the diagram (unit).

<a id="dvml011"></a>
### DVML011 — Unknown value in process diagram (Warning)

- Meaning: an invalid literal value in a process diagram; the default is drawn at run time:
  - a word not in its list: `kind`, `medium`, `rotate`, `direction`, `look`, `align` (case and surrounding spaces are ignored, as at run time);
  - a `DvSignal color` that is not a color, a `DvSignal thickness` that is not a number greater than 0;
  - a `DvLine via` not written as `x,y x,y …`;
  - a `DvInstrument x`/`y` that is not a number.
- Second form: a `{` in a process name or reference (`name`, `DvLine`/`DvSignal` `from`/`to`, `DvInstrument on`/`tap`). These are never bindings; the text is used as written, and it is checked as written (DVML008 too if nothing has that name).
- These attributes are reported only here, not under DVML020.
- Fix: use a listed value ([process attributes](dvml-process.md#dvprocess-attributes)); write names literally.

<a id="dvml012"></a>
### DVML012 — Unknown style property (Warning)

- Meaning: a style property name that does not exist, in `style`, a `*-style` attribute or a `<DvStyle>` (e.g. `colr`, or an unsupported one such as `margin`). Silently ignored at run time. A similar name is suggested when one exists.
- Bound styles: a style that is entirely a binding (`style="{S}"`) is skipped; for a declaration whose value only is bound (`color:{C}`), only the name is checked. The same skipping applies to DVML013 and DVML014.
- Fix: use a key from the [style key table](dvml-style.md#style-keys).

<a id="dvml013"></a>
### DVML013 — Unknown theme token (Warning)

- Meaning: the name in `token(name)` or `var(--name)` is not in the built-in themes. It draws [magenta](#magenta) at run time.
- Skipped: token names built from component parameters (`token(tone-{tone}-fg)`).
- Also checks token names inside color attributes (DVML020 does not).
- A theme built in C# (`new DvTheme(tokens, styleSheet)`) is not visible at build time. If the app uses such tokens, turn the check off: `dotnet_diagnostic.DVML013.severity = none`.
- Fix: correct the token name ([theme tokens](dvml-style.md#theme-tokens)).

<a id="dvml014"></a>
### DVML014 — Unknown class (Warning)

- Meaning: a `class` name is neither a built-in class nor defined in a `<DvStyle>` of any dvml in the project. Silently ignored at run time.
- Skipped: bound classes (`class="{C}"`).
- Classes from a theme style sheet or a component registered in C# are not visible at build time; turn the check off with `dotnet_diagnostic.DVML014.severity = none`.
- Fix: define the class in a `<DvStyle>` or correct the name ([classes](dvml-style.md#classes)).

<a id="dvml015"></a>
### DVML015 — DvRadio without option (Warning)

- Meaning: a `DvRadio` has no `option`, so it selects no value and never turns on (`value` is the group value, `option` is the value this button selects).
- Fix: add `option` ([DvRadio](dvml-widgets.md#dvradio)).

<a id="dvml016"></a>
### DVML016 — DvListBox value attribute does not match its mode (Warning)

- Meaning: `value` on a multi-select `DvListBox` (`selection-mode="multi"`), or `selected-items` on a single-select one. Ignored at run time.
- Skipped when `selection-mode` is a binding (`{…}`): the mode is not known at build time.
- Fix: multi-select binds `selected-items`; single-select binds `value` ([DvListBox](dvml-inputs.md#dvlistbox)).

<a id="dvml017"></a>
### DVML017 — Unknown widget attribute value (Warning)

- Meaning: a value not allowed for `DvListBox selection-mode` (`single`, `multi`) or `DvSlider direction` (`right`, `left`, `up`, `down`). Case and surrounding spaces are ignored, as at run time. The default is used at run time.
- Skipped when the value is a binding (`{…}`). These attributes are reported only here, not under DVML020.
- Fix: use a listed value.

<a id="dvml018"></a>
### DVML018 — Handler method not found (Error)

- Meaning: a handler attribute (`onclick`, `onbeforecommit`, `onack`, `onrowclick`) is not the name of a public instance method of the VM type or its base classes. At run time the handler would silently never run.
- Four forms: a misspelled, private or static method; a binding (`{…}` — handler attributes take a method name, bindings are not resolved); a call form (`M(item)` — write only the name, the item or row is passed automatically); a value that is not a method name.
- Checked only in dvml with `vm` (the VM type is the target), including handlers inside `DvRepeat` and on `DvColumn`. Only the name is checked; parameter rules are in [click rules](dvml-language.md#click) and [commit gate rules](dvml-language.md#onbeforecommit).
- Skipped: inside a `<DvComponent>` definition (which screen VM it runs on is not known at build time), and `onclick` on instances of a project-dvml component that declares an `onclick` parameter.
- A component registered in C# that declares an `onclick` parameter is not visible at build time; if you use one, turn the check off with `dotnet_diagnostic.DVML018.severity = none`.
- Fix: write the exact public method name.

<a id="dvml019"></a>
### DVML019 — Unknown attribute (Warning)

- Meaning: an attribute a built-in tag does not have — not in that tag's attributes or the common ones (`id`, `class`, `style`, `grow`, `width`, `height`, `visible`, `disabled`, `onclick`, `hold`, …), e.g. `<DvLabel txet="a"/>`. Silently ignored at run time. A similar name is suggested when one exists; an old attribute name is answered with the attribute that replaces it.
- A tag has only the attributes it uses; attributes of another tag of the same family are not accepted (`sweep` on `DvGauge`, `show-value` on `DvSparkline`, `placeholder` on `DvTabs`/`DvSegment`, `row-height` on `DvAlarmBanner`, `text` on equipment other than `DvTerminal`).
- Child elements that are not boxes (`DvOption`, `DvColumn`, `DvTrendChannel`, `DvSummary`, `DvPort`, `DvLine`, `DvSignal` …) take no common attribute except `id` ([which tag takes which common attribute](dvml-language.md#common-attribute-matrix)).
- Checked in dvml with or without `vm`, built-in tags only. Skipped: user component instances (their attributes are parameters), widgets registered in C#, built-in components (`DvCard`, `DvStat` …), everything inside a `<DvSymbol>` definition. `DvScreen` is checked (common attributes only).
- `DvPageFrame`/`DvDialogFrame` can be replaced by the app (also from C#, which the build cannot see), so unknown attributes are not reported on them; only common attribute values are checked (DVML020), and not at all when any dvml of the project defines `<DvComponent name="DvPageFrame">` (or `DvDialogFrame`).
- On the root element, `vm` and `font-lang-{lang}` are also accepted. Attributes in another namespace (`xmlns:…`, `x:…`) are skipped.
- Fix: correct the name, or move the attribute to the tag that uses it.

<a id="dvml020"></a>
### DVML020 — Attribute value is not valid for its kind (Warning)

- Meaning: a literal value that does not fit the attribute's kind; the default is used at run time, with a Trace warning:
  - true/false attribute not `true`, `false` or a number (`<DvLamp on="yes"/>`);
  - number attribute not an invariant-culture number (`max="abc"`, `blink-interval="1,5"`);
  - word attribute with a word not in its list (`<DvProgress direction="sideways"/>`, `variant="foo"`; case is ignored);
  - color attribute not `#RGB`/`#RRGGBB`/`#RRGGBBAA`/`transparent`/`token(name)`/`var(--name)` (`<DvLamp color="red"/>`; color names are not supported).
- Checks the same tags as DVML019, literal values only: a value containing `{` (a binding or interpolation, including component parameters inside a template) is skipped.
- Empty text is valid (false for true/false attributes, "not given" for the others).
- Reported elsewhere, not here: process `direction` `look` `align` `kind` `rotate` `medium`, `DvSignal kind` `color` `thickness`, `DvInstrument x` `y` ([DVML011](#dvml011)); `DvListBox selection-mode`, `DvSlider direction` ([DVML017](#dvml017)); a token name inside a color ([DVML013](#dvml013)).
- Fix: follow the [value rules](dvml-language.md#value-rules).

<a id="dvml021"></a>
### DVML021 — Component name is a built-in tag (Error)

- Meaning: `<DvComponent name="…">` uses the name of a built-in tag (`DvGauge`, `DvStack`, `DvScreen` …). `ComponentRegistry.Register` throws `ArgumentException` for it.
- Built-in components (`DvCard`, `DvPageFrame`, `DvSwitch` …) may be replaced by a component of the same name; that is not this error.
- Fix: choose another name ([component name rules](dvml-components.md#component-names)).

<a id="trace-warnings"></a>
## Runtime Trace warnings

Some problems are reported only at run time through `System.Diagnostics.Trace`. Keep a Trace listener on during development ([verifying the app](llm-rules.md#verify)).

- Messages start with `Devinno.UI:` (process diagrams: `Devinno.UI process:`).
- A markup warning is written once per screen (surface) for each attribute and expression; it is written again after the markup is reloaded. After 4096 different warnings on one screen, one line `Devinno.UI: too many markup warnings on this screen (4096); …` is written and further ones are not.
- An unknown token inside a style string is traced each time the style is resolved (`Devinno.UI: unresolved token 'name'`).

| Message form | Cause | Rule lives in |
|---|---|---|
| `{tag} {attr}='…' is not true or false (true, false, 1, 0); using …` | true/false attribute with other text (`yes`) | [true/false values](dvml-language.md#value-bool) |
| `{tag} {attr}='…' is not a number; …` | Number attribute not an invariant-culture number | [numbers](dvml-language.md#value-number) |
| `{tag} {attr}='…' is not one of …; using …` | Word attribute with a word not in its list (`direction="sideways"`, `variant="foo"`) | [words](dvml-language.md#value-word) |
| `{tag} {attr}='…' has no theme token {prefix}-{word}; using …` | `tone`/`state` word the theme has no token for (`DvRing tone="ok"`, `DvLamp state="go"`, `DvStatusItem state`) | [theme words](dvml-language.md#value-theme-word) · [magenta](#magenta) |
| `{tag} {attr}='…' is not a color (#RRGGBB or token(name)); …` | Unreadable color (`color="red"`) | [colors](dvml-language.md#value-color) |
| `{tag} {attr}='…' uses a token the theme does not have; drawing #FF00FF (magenta)` | Unknown token in a color attribute | [magenta](#magenta) |
| `unresolved token '…'; drawing #FF00FF (magenta)` · `token '…' is not a color` | Theme lacks a token a built-in part draws with, or the token value is not a color | [magenta](#magenta) |
| `{tag} format='…' is not a .NET format for …; using the default display` | `format` not usable for the value type | [value rules](dvml-language.md#value-rules) |
| `{tag} min='…' is not below max='…'; using 0..100` | `min >= max` | [value rules](dvml-language.md#value-rules) |
| `{tag} alarm-zones zone '…' is not valid (…); the zone is ignored` | Unreadable `alarm-zones` zone (only that zone is dropped) | [alarm zones](dvml-instruments.md#alarm-zones) |
| `unknown icon '…'; not drawn` | Unknown icon name | [icons](dvml-app.md#icons) |
| `content-align '…' ignored; …` | Unparsed `content-align` | [content-align](dvml-style.md#content-align) |
| `unsupported selector '…' ignored` | `DvStyle` selector the engine does not support | [classes](dvml-style.md#classes) |
| `could not parse transform '…'` · `could not parse transition '…'` | Unreadable `transform`/`transition` | [transition](dvml-style.md#transition) |
| `{tag} '{name}' was renamed to '{new}'; the old name is not read` | Attribute name the library does not read; use the name given | [common attributes](dvml-language.md#common-attributes) |
| `DvDataGrid field '…': … has no such property; shown as an empty cell` | Column `field` path not on the row type | [DvColumn](dvml-grid.md#dvcolumn) |
| `DvColumn cell-type="…" field '…': type … does not match; display only` | Property type does not fit the cell kind | [edit cells](dvml-grid.md#grid-edit-cells) |
| `DvDataGrid items is not an IList; it is copied …` · `DvDataGrid frozen applies only to …` · `DvColumnGroup '…' spans the frozen column boundary …` · `DvSummary field '…': no matching column; ignored` | Grid setup | [DvDataGrid](dvml-grid.md#dvdatagrid) |
| `DvDataGrid selected-items '…': unsupported property type …` · `DvListBox selected-items '…': unsupported property type …` | Selection bound to an unsupported type | [grid selection](dvml-grid.md#grid-selection) · [value widgets](dvml-language.md#value-widgets) |
| `{tag} onbeforecommit '…': no bool …(…) found …; not written` | Gate method missing or wrong signature (value rejected) | [commit gate](dvml-language.md#onbeforecommit) |
| `{kind} '…' ({tag}) threw: …` (Trace error) | A handler, setter or getter threw; also raised as `HandlerError` | [HandlerError](dvml-language.md#handler-error) |
| `id '…': no element with this id in the markup …` · `… does not match the … handle type …` | Code handle not connected | [handles](dvml-code.md#handles) |
| `<{tag}> is a custom widget; its child elements are not used` | Children inside a custom widget | [custom widgets](dvml-components.md#dvwidget) |
| `DvVideo …` · `DvImage could not load image '…'` | Media file missing or unreadable, FFmpeg not found, `source` not an `IFrameSource` | [DvVideo](dvml-widgets.md#dvvideo) · [DvImage](dvml-widgets.md#dvimage) |
| `string key '…' not found (language=…)` | Missing localization key | [localization](dvml-app.md#localization) |
| `Devinno.UI process: …` · `{tag} {attr}='…': process names and references are not bindable; used as written` | Process diagram connection, port, placement or layout problem | [process diagnostics](dvml-process-layout.md#process-diagnostics) |

<a id="magenta"></a>
## Magenta (`#FF00FF`)

Magenta means the theme has no such token (or the token value is not a color). It is never a chosen color.

**Draws magenta**
- An unknown token inside a style string: `style`, `class` rules, `hover-style`/`press-style`/`disabled-style`, `<DvStyle>` — `token(name)` or `var(--name)`. A literal name is caught at build time by [DVML013](#dvml013).
- An unknown token in a color attribute: `DvLamp color`, `DvTrendChannel color`, `DvSignal color`, zone colors in `alarm-zones`, `DvDataGrid` `*-color` attributes, `DvColumn on-color`. These also write a Trace warning.
- `tone` on `DvBadge` and `DvStat` with a word the theme has no token for: these built-in components build a token name from the word (`DvBadge tone="ok"` → `token(tone-ok-fg)`).
- A built-in part whose token is missing from the theme: widgets draw their own parts from theme tokens (lamp off color `lamp-off`, tank liquid `tank-fill`, chart colors `chart-1`…`chart-6`, keyboard, grid …). A theme made from scratch with `new DvTheme(...)` that lacks one of them shows magenta there.

**Does not draw magenta** (default + Trace warning instead)
- `DvStatusItem state` with an unknown word: the lamp and the status chip both use `off`, with one warning.
- `tone` on `DvRing`, `DvProgress`, `DvSparkline`, `DvBarChart` with an unknown word: `info`.
- `DvLamp state` with an unknown word: no state color (the lamp uses `color` or its style color).
- A word ending in `-fg` or `-soft` is never a valid `tone`/`state` word.
- A custom theme's own words work: a `state-idle` token makes `state="idle"` valid.
- `DvDataGrid` `lamp` cell with a string/enum value the theme has no `state-{value}` token for: off, no warning.
- An unreadable color (not a token): the attribute default + Trace warning ([colors](dvml-language.md#value-color)).

**Fix**
- Correct the token name (`token(surface-2)` is a typical misspelling) — see the [token list](dvml-style.md#theme-tokens).
- Use a valid `tone` (`neutral` `info` `success` `warning` `danger`) or `state` (`run` `stop` `fault` `warn` `manual` `off`) — see [DvBadge](dvml-widgets.md#dvbadge) and [DvStat](dvml-widgets.md#dvstat).
- Derive custom themes with `DvTheme.Dark.With(...)` / `DvTheme.Light.With(...)` so every built-in token exists ([built-in themes](dvml-style.md#built-in-themes)).
