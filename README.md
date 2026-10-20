# Devinno.UI

English | [한국어](README.ko.md)

A SkiaSharp-based .NET UI library (.NET 9 or later). It targets industrial HMIs (touch panels) as well as regular app screens.

Screens are written in XML markup with a CSS-style inline `style` and bound to a C# view model (VM). Markup is parsed at runtime, so it renders immediately without a build.

> The first version is currently in development. The API may change.

## Layout

| Path | Description |
|---|---|
| `src/Devinno.UI` | Core library (markup parser, layout, rendering, components) |
| `src/Devinno.UI.OpenTK` | OpenTK host (`ViewWindow`) |
| `src/Devinno.UI.WinForms` | WinForms host (`ViewControl`) |
| `tests/Devinno.UI.Tests` | xUnit tests |
| `samples/Devinno.UI.Demo` | OpenTK demo app |
| `samples/Devinno.UI.Demo.WinForms` | WinForms demo app |

## Getting started

One screen = a class deriving from `DvPage` + a `.dvml` with the same name. `DvApp` holds the pages and puts them on a window (OpenTK `ViewWindow` or WinForms `ViewControl`).

```csharp
using Devinno.UI.App;
using Devinno.UI.OpenTK;
using Devinno.UI.Styling;

var app = new DvApp { Theme = DvTheme.Dark };
app.Pages.Add(new HomePage());          // 현재 페이지가 없는 동안에는 처음 넣은 페이지가 보임(시작 페이지)
app.Pages.Add(new AlarmPage());
app.Top = new MainTopBar();             // 선택: 상단 바·사이드바(Left) 같은 고정 영역
using var window = new ViewWindow(app, 1280, 800, "My HMI");
window.Run();
```

Inside a page, switch with `App.Show<AlarmPage>()`. For details see [`dvml-ui-markup.md`](dvml-ui-markup.md) section 16; the demo is `samples/Devinno.UI.Demo` (`-- top`, `-- nav`).

## Documentation

- [`llm-rules.md`](llm-rules.md): authoring rules — principles, differences from CSS, common mistakes, how to verify screens (read first)
- [`dvml-ui-markup.md`](dvml-ui-markup.md): markup reference — tags, attributes, style keys, theme tokens
- [`dvml-process.md`](dvml-process.md): process diagram (P&ID) authoring guide — `DvProcess` equipment, connections, layout rules, user symbols
- C# API: XML doc comments in the code (IntelliSense, `Devinno.UI.xml` in the package)
- The three md files above are also included in `docs/` of the NuGet package.
