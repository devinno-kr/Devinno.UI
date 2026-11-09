# Devinno.UI Guide: Building a Screen From a Picture

<a id="image-page-head"></a>
Covers: building a screen whose look comes from a picture (mockup, screenshot, AI-generated design) that the screen should look like, with plates, buttons and backgrounds as image files.
Read first: [the rules every screen follows](llm-rules.md) · [markup basics](dvml-language.md#markup-basics) · [style syntax](dvml-style.md#style-syntax)
Not here: [a picture of a process diagram](dvml-process.md#placement-mode) · [the general verification steps](llm-rules.md#verify) · [the `DvImage` tag](dvml-widgets.md#dvimage) · [custom widgets (`DvWidget`)](dvml-components.md#dvwidget) · [built-in dialog boxes](dvml-dialogs.md#builtin-boxes)

This guide is for a screen whose look comes from a picture: someone hands you a mockup, a screenshot or an AI-generated design, and the screen should **look like it**, with its plates, buttons and backgrounds as image files. It explains **how to work out that screen yourself**.

Whether a picture is used that way is decided with the user first; this guide starts after that decision. A sketch or wireframe, or a picture given only as a reference for the layout, is not built this way: follow [`llm-rules.md`](llm-rules.md#choose-widget) with the standard components and theme tokens.

It is a method, not a template. Every screen needs its own answers. Numbers and names in the snippets only show how something is calculated or written, so do not reuse them as values, and do not treat any look or control mentioned here as a requirement.

- A picture of a process diagram follows [how to choose the process placement mode](dvml-process.md#placement-mode). **NEVER** draw one with `DvImage`.
- Any tag, attribute or value not in these documents **does not exist**. [What is not available](#image-not-available) lists what people most often assume exists.

<a id="image-method"></a>
## The method

1. [Understand the request](#image-understand)
2. [Inventory the picture](#image-inventory)
3. [Decide how to build each element](#image-build-choice)
4. [Fix the design size](#image-design-size)
5. [Define the image files](#image-files)
6. [Separate what is seen from what is touched](#image-touch)
7. [Connect states and values](#image-states)
8. [Write a widget only when needed](#image-widget)
9. [Verify](#image-verify)
10. [Report what you assumed](#image-report)

<a id="image-understand"></a>
## Understand the request

A picture shows one moment: shapes and colors. It does not show behavior, and a wrong guess looks correct on screen, so it is found late.

**MUST** find out whatever the request does not say:

| Find out | Why it matters |
|---|---|
| How is each control operated, and what does it change? | The shape does not say |
| Range, unit, initial value and source of each value | The position of a control does not say |
| Which other states exist? | Each state may need its own appearance |
| Panel resolution, OS and host | [Fix the design size](#image-design-size) |
| Do separated image files or a layered source exist? | [Define the image files](#image-files) |

If nobody can answer, choose the simplest reading and list it in your final message ([report what you assumed](#image-report)).

**Build only what the picture or the user shows.** Do not add behavior, limits, animation, states or controls because they seem natural for that kind of device. What you add looks intentional, and the next reader will treat it as required.

Some things a working screen needs even when the picture does not show them. Add only the minimum, and report each one ([report what you assumed](#image-report)):

- a visible response to a press;
- the ends of a range, when a control changes a value;
- a way to leave the app, when it runs full screen.

When the effect of a control is unknown, give it a touch area and a handler that does nothing, and report it. Do not make up an effect.

Do not copy a third-party logo or brand name without confirmation that the user may use it. Use a neutral placeholder and say so.

<a id="image-inventory"></a>
## Inventory the picture

**Separate the design from everything else.** Captures often include things that are not part of the screen: buttons and watermarks added by a browser or search engine, cursors, and the page or frame around the screen. The values shown are sample data.

**Convert coordinates.** Measure from the top-left corner of the screen itself, then:

```text
design coordinate = measured pixels × (design width ÷ screen width in the picture)
```

Check the height ratio too. If it differs noticeably, the picture does not match the panel, so ask which to follow rather than stretching it.

**Write down every element first, then write markup:** position, size, what it is, how it is operated and what can change. Zoom in and sample pixels with an image tool rather than estimating by eye. Note which elements repeat, since they may share one template.

**A flat capture is a measuring source, not an image source.** Its text and values are baked in and its resolution is low, so slices cut from it cannot show other values or states.

<a id="image-build-choice"></a>
## Decide how to build each element

For each element, ask these in order and stop at the first yes:

| Question | Build it with |
|---|---|
| Can a built-in widget show it as designed, with its classes restyled if needed? | That widget |
| Can style draw it? | `DvBox`, `DvLabel`, `DvButton` with `style` |
| Does it need a look that style cannot draw, with a shape that does not depend on a value? | A picture file, one per appearance: `background-image` on the element itself ([separate what is seen from what is touched](#image-touch)), or a `DvImage` for a picture that belongs to no element |
| Does its drawing depend on a value, or is it dragged, and no built-in can show it? | `DvWidget` ([write a widget only when needed](#image-widget)) |

**Judge each answer on a render.** "Can show it" means the render matches the picture. When the request is to reproduce the look, a visible difference means no, so go to the next row. Do not settle for the nearest built-in look, and do not treat fewer images as a goal. Keep only the differences the library cannot avoid, and report what differs and why ([report what you assumed](#image-report)).

**Mixing is normal.** One screen can use built-ins, style, images and widgets side by side.

**Restyling built-ins.** Built-in widgets carry classes such as `.btn` and `.segment-item`, and `DvStyle` can redefine them ([classes and `DvStyle`](dvml-style.md#dvstyle)). Check each property on a render.

**What style cannot draw:** `box-shadow` is one shadow per element, with no spread and no inset. Gradients are `linear-gradient` only, and only on backgrounds. `border-radius` is one value for all corners. A border is one color. There are no polygons. Built-in widgets have no image attributes, but every element, a `DvButton` included, takes `background-image` (one picture per element).

**Text and numbers stay in labels** so they can change. Only text that is part of fixed artwork, such as a logo, belongs in an image.

**How it is operated decides the rest:**

| Operation | Build it with |
|---|---|
| Tap | A touch area ([separate what is seen from what is touched](#image-touch)) |
| Enter one value or choose one item | Built-in boxes such as `App.InputNumber` and `App.SelectBox`, as [the dialog rules](llm-rules.md#code-dialogs) require |
| Drag | `DvSlider` or `DvKnob` if their look is acceptable, otherwise `DvWidget` |

**When reproducing a picture, some general rules in `llm-rules.md` give way to it:**

- Placement may use `position:absolute` in design coordinates across the whole screen.
- Colors matched to the picture may be fixed.
- If selection items must be images, `DvSegment` and `DvRadio` cannot draw them, so build the selection from buttons with a `background-image` per state.
- If the design places − and + apart, `DvInput` cannot draw them, so use two buttons and clamp the range in the VM.

Every other rule in `llm-rules.md` applies.

**Images do not follow the theme.** Tell the user if they expect theme switching.

<a id="image-design-size"></a>
## Fix the design size

Choose one design size, normally the panel's native resolution, and use it for every coordinate.

What the library does:

- Absolute coordinates do not scale. Without scaling, a smaller window crops the screen and a larger one leaves empty space.
- The OpenTK `ViewWindow` scales the whole screen with `fit: true` ([`ViewWindow`](dvml-app.md#viewwindow)). It lays out at the size you give, scales to the window keeping the aspect ratio, and converts pointer coordinates to match.
- When the ratios differ, `fit` leaves margins in the theme `background` color. Change the `background` token with `DvTheme.With` to match your background.
- `fullscreen: true` opens a borderless window for a kiosk panel. See the `ViewWindow` XML docs. The app must then provide its own way to leave ([understand the request](#image-understand)).
- The WinForms `ViewControl` has no `fit` ([`ViewControl`](dvml-app.md#viewcontrol)). There, the design size must be the panel's resolution. If the ratio changes, measure the picture again rather than multiplying old coordinates.
- `font-family` picks from installed fonts, and fonts cannot be bundled ([font keys](dvml-style.md#style-keys)). Among the fonts on the target, render the same text in each candidate and compare its width and height with the picture. `font-weight` gives two weights: normal and bold.

<a id="image-files"></a>
## Define the image files

**Each file must:**

- be a PNG with alpha where it needs transparency;
- contain nothing that can change, so no text or values;
- show one appearance, so each state is its own file;
- be made at the size it is drawn. There is no 9-slice: `DvImage fit` and `background-size` scale with `fill`, `contain` or `cover`, and `fill` stretches corners too. `background-size: natural` draws the file at its own size.

**Plate and margin.** Shadows and glows spread beyond the visible plate, so the PNG can be larger than the plate. Keep the two sizes separate:

```text
PNG size   = plate size + margins
PNG left   = plate left - left margin
PNG top    = plate top  - top margin
```

Margins can differ per side and per file. Measure them from the actual file. With `background-size: natural` ([separate what is seen from what is touched](#image-touch)) the element is the plate and the margins spread outside it: equal margins need nothing more, because the picture is centered; unequal ones are given as `background-image-offset` with the negative left and top margins in px, X then Y, for example `background-image-offset: -16 -16` (one value sets both).

**When asking a designer for files,** list each file's name, PNG size, margins, content and state.

**When there are no files yet,** make temporary images under the final file names, so that replacing them changes nothing else. Their job is to prove sizes, placement, layering and touch, so get the shape, size, margins and main colors right and leave the finish to the real files. Colors and sizes may be measured from a capture, but do not crop from it ([inventory the picture](#image-inventory)). Say that they are temporary.

If you draw them with SkiaSharp, `SKColor.Parse` reads eight digits as `#AARRGGBB`, while markup reads `#RRGGBBAA` ([color values](dvml-language.md#value-color)).

**How files are read:**

- `src` and `background-image: url(…)` take a file path. A relative path resolves against the **current working directory, not the exe folder**, so set it at startup and copy the files to the output. Embedded resources are not supported.
- A missing file draws an **empty box with no exception and no Trace warning**, so check that every file exists.
- A file is decoded once and cached by path. Changing it while running does not reload it.

<a id="image-touch"></a>
## Separate what is seen from what is touched

The touch area of an element is its rectangle. A picture must not make it larger, or its transparent margin presses too.

**The usual form: one element per plate.** Make the element the size of the visible plate (the part that should be pressed) and give it the picture as `background-image`. Text and values go inside it.

- A picture without a margin: `background-size: fill` (or `cover`) fills the element.
- A picture with a glow or shadow margin: `background-size: natural` draws the file at its own size, centered on the element, and does **not** cut what spreads outside. Unequal margins: `background-image-offset: -16 -16` (px, X then Y, one value sets both; see [define the image files](#image-files)).
- The picture is part of the element, not a child, so it never covers a press, and the margin outside the element does not press.

The numbers only show the calculation from the formula in [define the image files](#image-files) (a 200×80 plate in a 232×112 PNG, 16 px margins):

```xml
<DvScreen style="padding:0; gap:0; background-image:url(Assets/screen.png); background-size:fill">
  <DvButton onclick="OnPlate" style="position:absolute; left:100; top:100; width:200; height:80; padding:0; background:#00000000; background-image:url({PlateImg}); background-size:natural">
    <DvLabel text="{ValueText}" style="width:200; height:80; color:#ffffff; content-align:middle-center"/>
  </DvButton>
</DvScreen>
```

`padding:0` on the root makes coordinates start at the window corner. `background:#00000000` removes the button's own theme background. A screen or panel background is `background-image` on that element too; it covers the padding area, so there is no extra box.

**Check what lies on top.** Elements are drawn in markup order, and the press goes to the element drawn last at that point:

| Situation | What happens |
|---|---|
| An element drawn after a button overlaps it, even a transparent box or repeat with no `onclick` | The overlapped part of the button does not respond. If the overlapping element is inside another button, that button fires instead |
| A `natural` picture spreads over a neighbor drawn after it | The neighbor is drawn on top of the spread part. Order the markup so the part that must show is drawn last |
| A `natural` picture spreads over a neighbor drawn ahead of it | The spread part is drawn over the neighbor, but presses there go to the neighbor |
| An image child extends outside its button | Its transparent margin also presses the button |
| An image with `onclick` has transparent pixels | They press too. Hits use the rectangle, not the pixels |
| A button has `border-radius` | The corners press too. The hit area stays a rectangle |

Keep touch elements off the rectangle of a drag widget, because the element on top takes the press. In a `DvRepeat`, size the repeat to its own items; if its items are placed absolutely, give it `width:0; height:0` so that it covers nothing.

**When one element per plate is not enough,** separate the layers: `DvImage`s first, then values and text, then transparent touch elements last in the markup, so nothing covers them. This is for a picture that must be drawn above other elements, more than one picture per plate, or a picture that belongs to no single element. Repeated items then use one `DvRepeat` for images and another with the same `items` and layout for touch elements; each touch element receives its item.

**Pressed feedback.** A state style (`press-style`, `.name:press`; see [state styles](dvml-style.md#state-styles)) changes the element while it is pressed:

- another picture: `press-style="background-image:url(Assets/plate_down.png)"`;
- `transform: scale(0.95)` or `opacity`;
- a translucent `background` overlay works only on an element without a picture. A background color is drawn **under** the picture (as in CSS), so it does not darken it.

Check the pressed look on a render. A bound `src` or `background-image` changes the picture after a click and keeps it, which suits toggles and selections ([connect states and values](#image-states)).

<a id="image-states"></a>
## Connect states and values

For each state found in [understand the request](#image-understand), decide where it lives in the VM and what it changes:

| What changes | How |
|---|---|
| The image | Bind `src`, or `background-image: url({X})`, to a VM property that returns the file path |
| A color or size | Bind a value inside `style`, per item in a `DvRepeat` too. Gradient strings work |
| Text | Bind `text`. Format numbers in the VM |

When the screen redraws after a VM change is in [what to call after an update](llm-rules.md#updates-threads).

**Text details:**

- `DvLabel` defaults to `top-left` and `#222222`, so give it a `color`, and a size with `content-align` when it must sit in a region.
- `align-items:baseline` does not exist. For a value with a unit, use `align-items:end` and lift the small text with `padding-bottom`. The amount depends on the font sizes, so tune it on a render.

**Dialogs.** Built-in boxes and the keypad use the theme look, not your images ([built-in boxes](dvml-dialogs.md#builtin-boxes)). They work on top of an image screen but may not match it, so show the user early. `App.InputNumber` opens the keypad at once, and the keypad covers the lower part of the screen. While it slides, it takes all presses.

<a id="image-widget"></a>
## Write a widget only when needed

[Decide how to build each element](#image-build-choice) decides this. The pattern is in [custom widgets (`DvWidget`)](dvml-components.md#dvwidget). Write the behavior the user asked for and nothing more.

| You need | Use |
|---|---|
| To read a value or file path from the markup | `ctx.Number`, `ctx.Text` |
| To draw a file | `ImageCache.Get(path)`. The cache owns the image, so do not dispose it |
| To draw part of something | `canvas.Save`, `canvas.ClipRect`, `canvas.Restore` |
| To take a drag | `OnPointerDown` returning `true`, then `OnPointerMove`, `OnPointerUp`, `OnPointerCancel` |
| To write the value | `ctx.Commit`, with a two-way `value="{Bind X}"` |
| To use it in markup | `registry.RegisterWidget<T>("Tag")`, then set `Registry` on the `DvApp` |

**A color that follows a value** can be done with two images of the same shape: draw one fully, then draw the other clipped at the position the value gives.

```csharp
using Devinno.UI.Widgets;
using SkiaSharp;

public sealed class ValueFill : DvWidget
{
    public override void Paint(SKCanvas canvas, SKRect b, DvWidgetContext ctx)
    {
        // The edge formula depends on your artwork and your range
        var edge = b.Bottom - (float)(ctx.Number("value") / ctx.Number("max", 100)) * b.Height;
        using var paint = new SKPaint { IsAntialias = true };
        var sampling = new SKSamplingOptions(SKFilterMode.Linear);

        if (ImageCache.Get(ctx.Text("base-src")) is { } baseImage)
            canvas.DrawImage(baseImage, b.Left, b.Top, sampling, paint);

        canvas.Save();
        canvas.ClipRect(new SKRect(b.Left, edge, b.Right, b.Bottom));
        if (ImageCache.Get(ctx.Text("fill-src")) is { } fillImage)
            canvas.DrawImage(fillImage, b.Left, b.Top, sampling, paint);
        canvas.Restore();
    }
}
```

**Decide when to commit.** The usual pattern commits once on release. If another element must show the value during the drag, commit whenever the rounded value changes. The screen rebuilds on each commit and the drag continues.

**Size the widget for its images,** because the canvas is clipped to the element.

<a id="image-verify"></a>
## Verify

Run [the general verification](llm-rules.md#verify) (build without warnings, render PNGs including every state and pressed look, open the real app, drive input without a window). Then add these picture checks, derived from your own [inventory](#image-inventory):

1. **Compare the render with the picture.** Place it next to the picture at the same size and compare position, size and text, one region at a time. The eye misses small differences in text size, so measure text boxes where size matters.
2. **Check that every image file exists.**
3. **Test every touch rectangle:**
   - press just inside each edge and corner;
   - press between adjacent rectangles, once with each neighbor selected, or a press that leaks to the selected one goes unnoticed;
   - press just outside, where only a shadow or glow is drawn;
   - look at every spread margin of a `natural` picture: a neighbor drawn after it covers it.
4. **Test every drag:** both ends of the range, during the drag, and after release.
5. **With `fit`,** resize the real window and press something.

<a id="image-report"></a>
## Report what you assumed

Your final message must state:

- everything you assumed because nobody could answer;
- what you added because the screen needed it, and which controls have no effect ([understand the request](#image-understand));
- which images are temporary, and which files are missing;
- anything from the picture you replaced, such as a logo;
- what differs from the picture and why, such as dialogs, fonts or what the library cannot draw;
- what you could not verify, such as the real panel.

<a id="image-not-available"></a>
## Not available

- `background-position` and `background-repeat`, and more than one background picture per element.
- Image attributes on built-in widgets (use `background-image` on them). `DvButton icon` is Font Awesome only.
- A background color drawn over a picture (a pressed overlay on a picture; see [separate what is seen from what is touched](#image-touch)).
- 9-slice scaling.
- Touch areas that follow transparency or rounded corners.
- A way to exclude an element from hit testing.
- Embedded image resources.
- Bundling fonts with the app.
- `fit` on the WinForms `ViewControl`.
