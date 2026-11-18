using Devinno.UI;
using Devinno.UI.App;
using Devinno.UI.Components;
using Devinno.UI.Interaction;
using Devinno.UI.Layout;
using Devinno.UI.Markup;
using Devinno.UI.Painting;
using Devinno.UI.Styling;
using Devinno.UI.Widgets;
using Devinno.UI.WinForms;
using SkiaSharp;

// 편집 캔버스 시험 — 화면 편집기가 쓰는 라이브러리 기능을 실제 창에서 확인한다.
//   MarkupDocument(원문 보존 편집) · StyleParser.SetDeclaration(left/top 만) · HitTester.HitPath + LayoutBox.Origin(누른 요소)
//   DvWidget 포인터 확장(hover·leave·오른쪽·Shift·두 번 누름·Ctrl+휠) · 커서는 앱이 ViewControl.Cursor 로
//   dotnet run --project samples/Devinno.UI.Demo.WinForms -- editor
internal static class EditorDemo
{
    public const string Sample = """
        <!-- 편집 시험 화면 -->
        <DvScreen style="background:token(surface)">
            <DvLabel text="Tank 1" style="position:absolute; left:24; top:16; font-size:20"/>
            <DvGauge value="42" style="position:absolute; left:24; top:60; width:160; height:160"/>
            <DvButton text="Start" style="position:absolute; left:220; top:80; width:120"/>
            <DvToggle style="position:absolute; left:220; top:150"/>
        </DvScreen>
        """;

    public static Form Create(DvTheme theme)
    {
        var state = new EditorState { Doc = MarkupDocument.Parse(Sample.Replace("\n", "\r\n")), Theme = theme };
        var registry = new ComponentRegistry();
        registry.RegisterWidget<EditCanvas>("EditCanvas");
        EditCanvas.State = state;

        var app = new DvApp { Theme = theme, Registry = registry };
        app.Pages.Add(new EditorPage());
        var view = new ViewControl { Dock = DockStyle.Fill, App = app };
        state.View = view;

        var code = new TextBox { Dock = DockStyle.Fill, Multiline = true, ScrollBars = ScrollBars.Both, WordWrap = false, Font = new Font("Consolas", 10f), Text = state.Doc.Text };
        var status = new Label { Dock = DockStyle.Bottom, Height = 24, Text = "Click to select, drag to move, corner = resize, right click, Shift+click, double click, Ctrl+wheel = zoom" };
        var split = new SplitContainer { Dock = DockStyle.Fill };
        split.Panel1.Controls.Add(view);
        split.Panel2.Controls.Add(code);

        var syncing = false;
        state.Changed += () =>
        {
            syncing = true;
            code.Text = state.Doc.Text;
            if (state.Selected is { } sel)
            {
                var span = state.Doc.SpanOf(sel);
                code.Select(span.Start, span.Length); // 선택 요소를 코드에서 강조
            }
            syncing = false;
            app.Surface.Invalidate();
            view.Invalidate();
        };
        state.Log += m => status.Text = m;
        code.TextChanged += (_, _) =>
        {
            if (syncing)
                return;
            try
            {
                state.Doc = MarkupDocument.Parse(code.Text);
                state.Selected = null;
                app.Surface.Invalidate();
                view.Invalidate();
            }
            catch (Exception ex) when (ex is System.Xml.XmlException or ArgumentException) { status.Text = "markup: " + ex.Message; }
        };

        var form = new Form { Text = "Devinno.UI Demo — Edit canvas", ClientSize = new Size(1180, 560), StartPosition = FormStartPosition.CenterScreen };
        form.Load += (_, _) => split.SplitterDistance = split.Width * 6 / 10;
        form.Controls.Add(split);
        form.Controls.Add(status);
        return form;
    }
}

internal sealed class EditorPage : DvPage
{
    protected override string MarkupSource => """<DvScreen style="padding:16"><EditCanvas/></DvScreen>""";
}

internal sealed class EditorState
{
    public required MarkupDocument Doc { get; set; }
    public required DvTheme Theme { get; init; }
    public ViewControl? View { get; set; }
    public ElementNode? Selected { get; set; }
    public event Action? Changed;
    public event Action<string>? Log;
    public void NotifyChanged() => Changed?.Invoke();
    public void Write(string m) => Log?.Invoke(m);
}

// 편집 중인 페이지를 그대로 그리는 캔버스 — 굽기와 같은 LayoutEngine → Painter 경로
internal sealed class EditCanvas : DvWidget
{
    public static EditorState State = null!;
    private const float PageW = 480, PageH = 300, Handle = 8;

    private float _zoom = 1f;
    private LayoutBox? _page;           // 마지막으로 그린 페이지 트리
    private SKRect _bounds;
    private ElementNode? _hover;
    private (SKPoint Start, SKRect Rect, bool Resize)? _drag;
    private SKPoint _delta;

    public override SKSize Measure(DvWidgetContext ctx, float? availableWidth) => new(PageW * 1.25f, PageH * 1.25f);

    public override void Paint(SKCanvas canvas, SKRect bounds, DvWidgetContext ctx)
    {
        _bounds = bounds;
        _page = new LayoutEngine(State.Theme).Layout(State.Doc.Root, null, PageW, PageH);
        canvas.Save();
        canvas.Translate(bounds.Left, bounds.Top);
        canvas.Scale(_zoom);
        using (var bg = new SKPaint { Color = ctx.Color("background") })
            canvas.DrawRect(0, 0, PageW, PageH, bg);
        new Painter(State.Theme).Paint(canvas, _page, null);

        if (_hover is { } h && !ReferenceEquals(h, State.Selected) && BoxOf(h) is { } hb)
            Outline(canvas, hb.Rect, ctx.Color("tone-info"), dashed: true);
        if (State.Selected is { } s && BoxOf(s) is { } sb)
        {
            var r = Moved(sb.Rect);
            Outline(canvas, r, ctx.Color("primary"), dashed: false);
            using var fill = new SKPaint { Color = ctx.Color("primary") };
            canvas.DrawRect(HandleRect(r), fill);
        }
        canvas.Restore();
    }

    public override bool OnPointerHover(DvWidgetContext ctx, DvPointerArgs e)
    {
        var p = ToPage(e.Point);
        var over = ElementAt(p);
        var cursor = State.Selected is { } s && BoxOf(s) is { } sb
            ? HandleRect(sb.Rect).Contains(p) ? Cursors.SizeNWSE : sb.Rect.Contains(p) && IsAbsolute(s) ? Cursors.SizeAll : Cursors.Default
            : Cursors.Default;
        if (State.View is { } v && v.Cursor != cursor)
        {
            v.Cursor = cursor;
            State.Write($"cursor: {(cursor == Cursors.SizeNWSE ? "resize" : cursor == Cursors.SizeAll ? "move" : "default")}");
        }
        if (ReferenceEquals(over, _hover))
            return false;
        _hover = over;
        return true;
    }

    public override void OnPointerLeave(DvWidgetContext ctx)
    {
        _hover = null;
        if (State.View is { } v)
            v.Cursor = Cursors.Default;
    }

    public override bool OnPointerDown(DvWidgetContext ctx, DvPointerArgs e)
    {
        var p = ToPage(e.Point);
        var el = ElementAt(p);
        var name = el is null ? "(nothing)" : $"{el.Tag} {(el.Attributes.TryGetValue("text", out var t) ? t : "")}".Trim();
        if (e.Button == DvPointerButton.Right)
        {
            State.Write($"right click on {name} at ({p.X:0}, {p.Y:0}) — an editor would open its context menu here");
            return false;
        }

        if (e.ClickCount >= 2)
        {
            State.Write($"double click on {name} — an editor would start editing its text");
            return false;
        }

        if (State.Selected is { } s && BoxOf(s) is { } sb && HandleRect(sb.Rect).Contains(p))
        {
            _drag = (p, sb.Rect, true);
            _delta = default;
            return true;
        }

        State.Selected = el;
        State.Write((el is null ? "nothing selected" : $"selected {name}") + (e.Modifiers != DvModifiers.None ? $" (keys: {e.Modifiers})" : ""));
        State.NotifyChanged();
        if (el is not null && IsAbsolute(el) && BoxOf(el) is { } b)
        {
            _drag = (p, b.Rect, false);
            _delta = default;
            return true;
        }

        return false;
    }

    public override void OnPointerMove(DvWidgetContext ctx, DvPointerArgs e)
    {
        if (_drag is { } d)
            _delta = ToPage(e.Point) - d.Start;
    }

    public override void OnPointerUp(DvWidgetContext ctx, DvPointerArgs e)
    {
        if (_drag is not { } d || State.Selected is not { } sel)
            return;
        _drag = null;
        _delta = ToPage(e.Point) - d.Start; // 이동 없이 바로 뗀 경우도
        if (_delta == default)
            return;
        var style = sel.Attributes.GetValueOrDefault("style") ?? "";
        style = d.Resize
            ? StyleParser.SetDeclaration(StyleParser.SetDeclaration(style, "width", $"{MathF.Round(MathF.Max(8, d.Rect.Width + _delta.X))}"), "height", $"{MathF.Round(MathF.Max(8, d.Rect.Height + _delta.Y))}")
            : StyleParser.SetDeclaration(StyleParser.SetDeclaration(style, "left", $"{MathF.Round(d.Rect.Left + _delta.X)}"), "top", $"{MathF.Round(d.Rect.Top + _delta.Y)}");
        var path = PathOf(State.Doc.Root, sel)!;
        State.Doc = State.Doc.SetAttribute(sel, "style", style);
        State.Selected = NodeAt(State.Doc.Root, path); // 새 문서에서 같은 자리의 요소
        _delta = default;
        State.Write($"{(d.Resize ? "resized" : "moved")} {sel.Tag} — only its style changed in the text");
        State.NotifyChanged();
    }

    public override void OnPointerCancel(DvWidgetContext ctx)
    {
        _drag = null;
        _delta = default;
    }

    public override bool OnPointerWheel(DvWidgetContext ctx, DvPointerArgs e, float delta)
    {
        if ((e.Modifiers & DvModifiers.Ctrl) == 0)
        {
            State.Write($"wheel {delta:+0.#;-0.#} keys: {e.Modifiers} — no Ctrl, the page scrolls as usual");
            return false;
        }
        _zoom = Math.Clamp(_zoom * (delta > 0 ? 1.1f : 1 / 1.1f), 0.5f, 1.25f);
        State.Write($"zoom {_zoom:P0}");
        return true;
    }

    private SKPoint ToPage(SKPoint p) => new((p.X - _bounds.Left) / _zoom, (p.Y - _bounds.Top) / _zoom);

    private SKRect Moved(SKRect r) => _drag is not { } d ? r
        : d.Resize ? SKRect.Create(r.Left, r.Top, MathF.Max(8, d.Rect.Width + _delta.X), MathF.Max(8, d.Rect.Height + _delta.Y))
        : SKRect.Create(d.Rect.Left + _delta.X, d.Rect.Top + _delta.Y, r.Width, r.Height);

    private static SKRect HandleRect(SKRect r) => SKRect.Create(r.Right - Handle / 2, r.Bottom - Handle / 2, Handle, Handle);

    // 누른 점의 편집 파일 요소 — 경로를 끝에서 거슬러 올라가며 Origin 이 문서 요소인 첫 상자(루트 DvScreen 은 고르지 않음)
    private ElementNode? ElementAt(SKPoint p)
    {
        if (_page is null)
            return null;
        var path = HitTester.HitPath(_page, p, wholeBox: true);
        for (var i = path.Count - 1; i > 0; i--)
            if (PathOf(State.Doc.Root, path[i].Box.Origin) is not null)
                return path[i].Box.Origin;
        return null;
    }

    private LayoutBox? BoxOf(ElementNode node) => _page is null ? null : Find(_page, node);

    private static LayoutBox? Find(LayoutBox b, ElementNode node)
    {
        if (ReferenceEquals(b.Origin, node))
            return b;
        foreach (var c in b.Children)
            if (Find(c, node) is { } f)
                return f;
        return null;
    }

    private static bool IsAbsolute(ElementNode n) =>
        string.Equals(StyleParser.GetDeclaration(n.Attributes.GetValueOrDefault("style"), "position"), "absolute", StringComparison.OrdinalIgnoreCase);

    private static List<int>? PathOf(ElementNode root, ElementNode target)
    {
        if (ReferenceEquals(root, target))
            return [];
        for (var i = 0; i < root.Children.Count; i++)
            if (PathOf(root.Children[i], target) is { } rest)
                return [i, .. rest];
        return null;
    }

    private static ElementNode NodeAt(ElementNode root, List<int> path) => path.Aggregate(root, (n, i) => n.Children[i]);

    private static void Outline(SKCanvas canvas, SKRect r, SKColor color, bool dashed)
    {
        using var pen = new SKPaint { Color = color, IsStroke = true, StrokeWidth = 1.5f, IsAntialias = true };
        if (dashed)
            pen.PathEffect = SKPathEffect.CreateDash([4, 3], 0);
        canvas.DrawRect(r, pen);
    }
}
