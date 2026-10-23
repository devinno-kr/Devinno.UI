# Devinno.UI

English | [한국어](https://github.com/devinno-kr/Devinno.UI/blob/master/README.ko.md)

A SkiaSharp-based .NET UI library (.NET 9 or later). It targets industrial HMIs (touch panels) as well as regular app screens.

Screens are written in XML markup with a CSS-style inline `style` and bound to a C# view model (VM). Markup is parsed at runtime, so it renders immediately without a build.

> The first version is currently in development. The API may change.

![Plant dashboard (dark theme)](https://raw.githubusercontent.com/devinno-kr/Devinno.UI/master/assets/readme-dashboard.webp)

![Process diagram, P&ID (dark theme)](https://raw.githubusercontent.com/devinno-kr/Devinno.UI/master/assets/readme-process.webp)

## A screen in markup

A screen is a `.dvml` file and a C# class with the same name. The class is the view model: `{Temperature}` shows its property, and `onclick="EditSetpoint"` calls its method. This page comes from `samples/Devinno.UI.AppSample` (a test checks that the README and the sample match).

`MonitorPage.dvml`

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

`MonitorPage.cs`

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

## Design: a UI library that works well with AI

Devinno.UI assumes that screens will be written with AI (LLM) coding tools. It tries to do two things: help the AI make fewer mistakes, and make sure the mistakes it does make show up right away instead of passing silently. People writing screens by hand get the same help.

**Syntax the AI already knows**
- A screen is XML markup (`.dvml`) that looks a lot like HTML, styled with a CSS-like `style` attribute. Flex, grid, `box-shadow` and similar patterns behave the way the AI expects, so what it learned from the web carries over.
- The places where Devinno.UI differs from CSS (defaults, unsupported properties) are listed in one document, `llm-rules.md`.

**Mistakes you can see**
In UI code, the usual failure is a mistake that does nothing: no error, no warning, just a screen that is slightly wrong. Devinno.UI reports these in three places.
- The build reports diagnostics DVML001 to DVML018 at the line and column in the dvml file. They cover unknown style properties, theme tokens and classes, values that are not allowed, handler methods the VM does not have, broken process diagram connections and duplicate `id`s.
- The compiler catches misspelled names in code. When an element has an `id`, the source generator adds a typed handle field to the VM.
- At run time, a bad color token that got past the build is drawn in magenta (`#FF00FF`), so it is hard to miss.

**The AI can check its own work**
- `DvRenderer` draws a screen to a PNG without opening a window. An AI can look at the screen it just wrote and fix what is wrong.
- Together this gives the AI a short loop: (1) write or change a `.dvml` file and its VM, (2) build and fix the DVML diagnostics, which point at a file, line and column, (3) render the screen to a PNG, look at it, and fix what looks wrong.

**Docs that match the code**
- If a feature is not in the docs, it does not exist. The markup reference lists every tag, attribute and value.
- The tests parse, render and compile the examples in the docs, so a wrong example makes the tests fail.
- The docs ship inside the package. On build they are copied into `.devinno/` in your project, where AI tools can find them. `llms.txt` is the place to start.

**Good-looking screens by default**
- Semantic components (`DvCard`, `DvStat`, `DvBadge` and others) and theme tokens (`token(surface)` and others) give consistent screens in both the dark and light themes. You do not have to pick colors by hand.
- In a process diagram (P&ID) you list the equipment and how it connects. The library places the equipment and routes the pipes at right angles, so the AI never has to work out coordinates.

**Built for industrial HMIs**
- Data and logic live in the VM (the app). Widgets only show values and take input.
- Alarms (based on ISA-18.2), trends, a data grid, a virtual keyboard, long-press protection (`hold`) and screen lock come built in.

## Install

An app references the core package and one host adapter that puts screens on a window.

```bash
dotnet add package Devinno.UI
dotnet add package Devinno.UI.OpenTK      # own window (ViewWindow)
# or
dotnet add package Devinno.UI.WinForms    # inside a WinForms form (ViewControl)
```

A WinForms app must target `net9.0-windows10.0.19041.0` or later (the WinForms adapter, like SkiaSharp's WinForms views, ships only that TFM). With the default `net9.0-windows` no assembly is referenced and the build fails with "type or namespace 'Devinno' not found". The project file of a WinForms app looks like this:

```xml
<PropertyGroup>
  <OutputType>WinExe</OutputType>
  <TargetFramework>net9.0-windows10.0.19041.0</TargetFramework>
  <UseWindowsForms>true</UseWindowsForms>
</PropertyGroup>
```

**Platforms.** Tested on Windows 11 (x64) with both adapters. The WinForms adapter runs on Windows only. The core library and the OpenTK adapter do not call Windows-specific APIs, but they have not been tested on Linux (including Raspberry Pi touch panels) yet. On Linux, SkiaSharp also needs the `SkiaSharp.NativeAssets.Linux` package in the app.

## Getting started

One screen = a class deriving from `DvPage` + a `.dvml` with the same name. `DvApp` holds the pages and puts them on a window (OpenTK `ViewWindow` or WinForms `ViewControl`).

```csharp
using Devinno.UI.App;
using Devinno.UI.OpenTK;
using Devinno.UI.Styling;

var app = new DvApp { Theme = DvTheme.Dark };
app.Pages.Add(new HomePage());          // while no page is current, the first page added is shown (start page)
app.Pages.Add(new AlarmPage());
app.Top = new MainTopBar();             // optional: fixed areas such as a top bar or a sidebar (Left)
using var window = new ViewWindow(app, 1280, 800, "My HMI");
window.Run();
```

Inside a page, switch with `App.Show<AlarmPage>()`. For details see section 16 of `dvml-ui-markup.md` (listed below).

## Documentation

These documents ship in the package's `docs/` folder (after a build they are also copied to `.devinno/Devinno.UI/` in your project, see below).

- `llm-rules.md`: authoring rules (principles, differences from CSS, common mistakes, how to check a screen). Read this first.
- `dvml-ui-markup.md`: markup reference with every tag, attribute, style key and theme token.
- `dvml-process.md`: guide to process diagrams (P&ID) with `DvProcess`: equipment, connections, layout rules and user symbols.
- `llms.txt`: entry point for AI tools (reading order of the files above)
- C# API: XML doc comments (IntelliSense, `Devinno.UI.xml` in the package)

## Documentation for AI tools

When a project that references Devinno.UI is built, the package documents and the English IntelliSense XML are copied to `.devinno/Devinno.UI/` in the project folder (SDK-style projects; the folder appears after the first `dotnet build`, not after `restore` alone). Each Devinno package uses its own subfolder.

- Add this line to your AI instruction file (`CLAUDE.md`, `AGENTS.md`, etc.): `Devinno library docs: read .devinno/*/llms.txt first.`
- To turn the copy off: `<DevinnoCopyDocs>false</DevinnoCopyDocs>` in the project file.
- Adding `.devinno/` to `.gitignore` is recommended (it is recreated on every build).

## Repository (repository only)

| Path | Description |
|---|---|
| `src/Devinno.UI` | Core library (markup parser, layout, rendering, components) |
| `src/Devinno.UI.OpenTK` | OpenTK host (`ViewWindow`) |
| `src/Devinno.UI.WinForms` | WinForms host (`ViewControl`) |
| `tests/Devinno.UI.Tests` | xUnit tests |
| `samples/Devinno.UI.Demo` | OpenTK demo app (`-- top`, `-- nav` show the app model) |
| `samples/Devinno.UI.Demo.WinForms` | WinForms demo app |

## Disclaimer

Devinno.UI is provided "as is", without warranty of any kind (see `LICENSE`).

It is a library for display and operator input, and it is not designed or certified for safety functions. Emergency stops, interlocks and other safety functions must be implemented in separate safety-rated equipment (PLC, safety relays, etc.), never in the HMI screen alone.

You are responsible for testing and validating any system you build with it.

## License

MIT (`LICENSE`). Third-party notices (Font Awesome Free 6.1.2 icon font: fonts SIL OFL 1.1, icons CC BY 4.0): `THIRD-PARTY-NOTICES.md`, also included in the package.
