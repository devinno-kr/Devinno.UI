using Devinno.UI.Components;
using Devinno.UI.Widgets;
using SkiaSharp;

// 커스텀 위젯 데모 — 앱이 C# 클래스로 그리는 태그 세 개.
//   BatteryGauge = 셀 막대 + 퍼센트(테마 토큰 색, DvRepeat 안에서도 요소마다 인스턴스)
//   Fan = 운전 중이면 도는 팬(FrameInterval 로 초당 30프레임, 정지면 다시 그리지 않음)
//   SetpointTank = 설정값 선을 끌어 바꾸는 탱크(포인터 입력 + 뗄 때 Commit → {Bind Setpoint})
public sealed partial class CustomWidgetPage : DemoPage
{
    public const int Width = 1100;
    public const int Height = 620;

    public static void Register(ComponentRegistry registry)
    {
        registry.RegisterWidget<BatteryGauge>("BatteryGauge");
        registry.RegisterWidget<Fan>("Fan");
        registry.RegisterWidget<SetpointTank>("SetpointTank");
    }

    public double Level { get; set; } = 58;
    public double Setpoint { get; set; } = 75;
    public string SetpointText => $"Setpoint {Setpoint:0} %";

    public List<Rack> Racks { get; } =
    [
        new("Rack 1", 86),
        new("Rack 2", 54),
        new("Rack 3", 31),
        new("Rack 4", 12),
    ];

    public double Soc { get; set; } = 64;
    public bool FanRun { get; set; } = true;
    public string FanText => FanRun ? "Running" : "Stopped";

    public sealed record Rack(string Name, double Soc);
}

// 배터리 셀 게이지 — value(0–100)만큼 셀을 채우고 오른쪽에 퍼센트. 20% 미만 위험, 40% 미만 주의 색
public sealed class BatteryGauge : DvWidget
{
    private const float Label = 52;

    public override SKSize Measure(DvWidgetContext ctx, float? availableWidth) => new(240, 28);

    public override void Paint(SKCanvas canvas, SKRect bounds, DvWidgetContext ctx)
    {
        var soc = Math.Clamp(ctx.Number("value"), 0, 100);
        var cells = Math.Clamp((int)ctx.Number("cells", 10), 1, 40);
        var tone = soc < 20 ? "tone-danger" : soc < 40 ? "tone-warning" : "tone-success";

        var bar = new SKRect(bounds.Left, bounds.Top, bounds.Right - Label, bounds.Bottom);
        const float gap = 3;
        var w = (bar.Width - gap * (cells - 1)) / cells;
        var lit = (int)Math.Ceiling(soc / 100 * cells);
        using var on = new SKPaint { IsAntialias = true, Color = ctx.Color(tone) };
        using var off = new SKPaint { IsAntialias = true, Color = ctx.Color("gauge-track") };
        for (var i = 0; i < cells; i++)
        {
            var x = bar.Left + i * (w + gap);
            canvas.DrawRoundRect(new SKRect(x, bar.Top, x + w, bar.Bottom), 3, 3, i < lit ? on : off);
        }

        ctx.DrawText(canvas, $"{soc:0}%", bounds.Right, bounds.MidY + 6, 16, ctx.TextColor, SKTextAlign.Right, bold: true);
    }
}

// 설정값 탱크 — level(읽기) 만큼 채우고, value(설정값, {Bind …})는 선을 위아래로 끌어 바꾼다.
// 끄는 동안은 필드(_drag)만 바꿔 그리고, 뗄 때 한 번 Commit(내장 노브·슬라이더와 같은 방식)
public sealed class SetpointTank : DvWidget
{
    private double? _drag;

    public override SKSize Measure(DvWidgetContext ctx, float? availableWidth) => new(140, 220);

    public override void Paint(SKCanvas canvas, SKRect bounds, DvWidgetContext ctx)
    {
        var tank = Tank(bounds);
        var level = Math.Clamp(ctx.Number("level"), 0, 100);
        var sp = _drag ?? Math.Clamp(ctx.Number("value"), 0, 100);

        using var shell = new SKPaint { IsAntialias = true, Color = ctx.Color("gauge-track") };
        canvas.DrawRoundRect(tank, 10, 10, shell);
        using var liquid = new SKPaint { IsAntialias = true, Color = ctx.Color("tone-info") };
        canvas.Save();
        canvas.ClipRoundRect(new SKRoundRect(tank, 10, 10), antialias: true);
        canvas.DrawRect(new SKRect(tank.Left, Y(tank, level), tank.Right, tank.Bottom), liquid);
        canvas.Restore();

        // 설정값 선 + 손잡이(끄는 중이면 손잡이가 커진다 — 색은 액체와 겹치지 않게 그대로)
        var y = Y(tank, sp);
        using var line = new SKPaint { IsAntialias = true, StrokeWidth = 3, Color = ctx.Color("tone-warning") };
        canvas.DrawLine(tank.Left - 6, y, tank.Right + 6, y, line);
        canvas.DrawCircle(tank.Right + 10, y, _drag is null ? 8 : 11, line);
        ctx.DrawText(canvas, $"{sp:0}", tank.Right + 22, y + 5, 14, ctx.TextColor, bold: true);
    }

    public override bool OnPointerDown(DvWidgetContext ctx, SKPoint p)
    {
        if (!ctx.CanCommit)
            return false; // 단방향 value 면 보통 요소처럼(클릭 등)
        _drag = At(ctx, p);
        return true;
    }

    public override void OnPointerMove(DvWidgetContext ctx, SKPoint p) => _drag = At(ctx, p);

    public override void OnPointerUp(DvWidgetContext ctx, SKPoint p)
    {
        ctx.Commit(Math.Round(At(ctx, p)));
        _drag = null;
    }

    public override void OnPointerCancel(DvWidgetContext ctx) => _drag = null;

    // 오른쪽에 손잡이·숫자 자리를 남긴 탱크 몸통
    private static SKRect Tank(SKRect b) => new(b.Left + 8, b.Top + 8, b.Right - 48, b.Bottom - 8);

    private static float Y(SKRect tank, double percent) => tank.Bottom - (float)(percent / 100) * tank.Height;

    private static double At(DvWidgetContext ctx, SKPoint p)
    {
        var tank = Tank(ctx.Bounds);
        return Math.Clamp((tank.Bottom - p.Y) / tank.Height * 100, 0, 100);
    }
}

// 팬 — 날개 3개, running 이면 1초에 반 바퀴. 색은 state-run / state-stop 토큰
public sealed class Fan : DvWidget
{
    public override SKSize Measure(DvWidgetContext ctx, float? availableWidth) => new(120, 120);

    public override int? FrameInterval(DvWidgetContext ctx) => ctx.Flag("running") ? 33 : null;

    public override void Paint(SKCanvas canvas, SKRect bounds, DvWidgetContext ctx)
    {
        var running = ctx.Flag("running");
        var r = MathF.Min(bounds.Width, bounds.Height) / 2;
        var c = new SKPoint(bounds.MidX, bounds.MidY);
        var color = ctx.Color(running ? "state-run" : "state-stop");

        using var ring = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 4, Color = ctx.Color("border-strong") };
        canvas.DrawCircle(c, r - 2, ring);

        var angle = running ? ctx.TimestampMs % 2000 / 2000f * 360f : 0f;
        using var blade = new SKPaint { IsAntialias = true, Color = color };
        using var path = new SKPath();
        path.MoveTo(0, 0);
        path.CubicTo(r * 0.15f, -r * 0.35f, r * 0.55f, -r * 0.55f, r * 0.8f, -r * 0.2f);
        path.CubicTo(r * 0.55f, -r * 0.05f, r * 0.25f, 0, 0, 0);
        for (var i = 0; i < 3; i++)
        {
            canvas.Save();
            canvas.Translate(c.X, c.Y);
            canvas.RotateDegrees(angle + i * 120);
            canvas.DrawPath(path, blade);
            canvas.Restore();
        }

        using var hub = new SKPaint { IsAntialias = true, Color = ctx.Color("border-strong") };
        canvas.DrawCircle(c, r * 0.14f, hub);
    }
}
