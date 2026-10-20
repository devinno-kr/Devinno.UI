# Devinno.UI

English | [한국어](https://github.com/devinno-kr/Devinno.UI/blob/master/README.ko.md)

A SkiaSharp-based .NET UI library (.NET 9 or later). It targets industrial HMIs (touch panels) as well as regular app screens.

Screens are written in XML markup with a CSS-style inline `style` and bound to a C# view model (VM). Markup is parsed at runtime, so it renders immediately without a build.

> The first version is currently in development. The API may change.

![Dashboard screen (light theme)](https://raw.githubusercontent.com/devinno-kr/Devinno.UI/master/assets/readme-dashboard.png)

![Process diagram screen (dark theme)](https://raw.githubusercontent.com/devinno-kr/Devinno.UI/master/assets/readme-process.png)

## Install

An app references the core package and one host adapter that puts screens on a window.

```bash
dotnet add package Devinno.UI
dotnet add package Devinno.UI.OpenTK      # own window (ViewWindow)
# or
dotnet add package Devinno.UI.WinForms    # inside a WinForms form (ViewControl)
```

A WinForms app must target `net9.0-windows10.0.19041.0` or later (the WinForms adapter, like SkiaSharp's WinForms views, ships only that TFM). With the default `net9.0-windows` no assembly is referenced and the build fails with "type or namespace 'Devinno' not found".

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

- `llm-rules.md`: authoring rules — principles, differences from CSS, common mistakes, how to verify screens (read first)
- `dvml-ui-markup.md`: markup reference — tags, attributes, style keys, theme tokens
- `dvml-process.md`: process diagram (P&ID) authoring guide — `DvProcess` equipment, connections, layout rules, user symbols
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

## License

MIT (`LICENSE`). Third-party notices (Font Awesome Free 6.1.2 icon font: fonts SIL OFL 1.1, icons CC BY 4.0): `THIRD-PARTY-NOTICES.md`, also included in the package.
