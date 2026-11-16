using Devinno.UI;
using Devinno.UI.Alarms;
using Devinno.UI.Layout;
using Devinno.UI.Markup;
using Devinno.UI.Painting;
using Devinno.UI.Styling;
using SkiaSharp;
using Xunit;

// 등급 표시 한 벌(H8): 등급 칩 = 등급색 채움 + 대비 글자, 미확인만 깜빡임, 확인은 색 유지.
// 한 행의 색 표시는 칩 하나(왼쪽 막대 없음), ACK 는 중립 버튼, 깜빡임 주기 500 하나.
public class SeverityDisplayTests
{
    public sealed class Vm
    {
        public List<AlarmRow> Alarms { get; } =
        [
            new AlarmRow { Id = 1, Time = new DateTime(2026, 11, 16, 9, 0, 0), Priority = AlarmPriority.Critical, Message = "unacked" },
            new AlarmRow
            {
                Id = 2, Time = new DateTime(2026, 11, 16, 8, 0, 0), Priority = AlarmPriority.Major, Message = "acked",
                Acked = true, AckTime = new DateTime(2026, 11, 16, 8, 5, 0)
            },
        ];
        public List<AlarmRow> None { get; } = [];
        public void Ack(AlarmRow row) { }
    }

    private const string List = "<DvAlarmList items=\"{Alarms}\" onack=\"Ack\" style=\"width:600; height:200\"/>";

    private static (LayoutBox Box, SKBitmap Bmp) Render(string markup, long ts = 0, DvTheme? theme = null, int w = 600, int h = 200)
    {
        theme ??= DvTheme.Light;
        var root = new LayoutEngine(theme).Layout(MarkupParser.Parse(markup), new Vm(), w, h);
        var bmp = new SKBitmap(w, h, SKColorType.Rgba8888, SKAlphaType.Premul);
        using var canvas = new SKCanvas(bmp);
        canvas.Clear(SKColors.White);
        new Painter(theme).Paint(canvas, root, new Vm(), null, ts);
        return (Flatten(root).First(b => b.Node.Tag.StartsWith("DvAlarm", StringComparison.Ordinal)), bmp);
    }

    private static IEnumerable<LayoutBox> Flatten(LayoutBox b) => new[] { b }.Concat(b.Children.SelectMany(Flatten));

    private static (SKRect Row, SKRect? Ack) Row(LayoutBox box, int i)
    {
        var l = AlarmListLayout.Compute(box.Rect, box.AlarmItems, box.AlarmRowHeight, box.AlarmShowAck);
        return (l.Rows[i].Rect, l.Rows[i].AckButton);
    }

    private static SKColor C(string token) => ThemeResolver.Color(DvTheme.Light, token);
    private static bool Near(SKColor a, SKColor b, int tol = 6)
        => Math.Abs(a.Red - b.Red) <= tol && Math.Abs(a.Green - b.Green) <= tol && Math.Abs(a.Blue - b.Blue) <= tol;

    // 칩 왼쪽 안쪽 점(글자 앞) — 칩은 우선순위 열 시작(왼쪽 16 + 시간 열 78)에서
    private static SKColor ChipPixel(LayoutBox box, SKBitmap bmp, int row)
    {
        var (r, _) = Row(box, row);
        return bmp.GetPixel((int)(box.Rect.Left + 16 + 78 + 4), (int)r.MidY);
    }

    [Fact]
    public void Unacked_chip_is_filled_with_the_severity_color_and_blinks()
    {
        var (box, on) = Render(List, ts: 0);
        var (_, off) = Render(List, ts: Painter.AlarmBlinkMs);
        using var _1 = on;
        using var _2 = off;
        Assert.True(Near(ChipPixel(box, on, 0), C("alarm-critical")), $"on={ChipPixel(box, on, 0)}");
        Assert.False(Near(ChipPixel(box, off, 0), C("alarm-critical")), $"off={ChipPixel(box, off, 0)}");
    }

    [Fact]
    public void Acked_chip_keeps_its_color_without_blinking()
    {
        var (box, on) = Render(List, ts: 0);
        var (_, off) = Render(List, ts: Painter.AlarmBlinkMs);
        using var _1 = on;
        using var _2 = off;
        Assert.True(Near(ChipPixel(box, on, 1), C("alarm-major")), $"on={ChipPixel(box, on, 1)}");
        Assert.Equal(ChipPixel(box, on, 1), ChipPixel(box, off, 1));
    }

    [Fact]
    public void Row_has_no_left_priority_bar()
    {
        var (box, bmp) = Render(List, ts: 0);
        using var _ = bmp;
        var (r, _) = Row(box, 0);
        Assert.False(Near(bmp.GetPixel((int)r.Left + 5, (int)r.MidY), C("alarm-critical")));
    }

    [Fact]
    public void Ack_button_is_neutral()
    {
        var (box, bmp) = Render(List, ts: 0);
        using var _ = bmp;
        var ack = Row(box, 0).Ack!.Value;
        var px = bmp.GetPixel((int)ack.Left + 4, (int)ack.Top + 4);
        Assert.True(Near(px, C("surface-raised")), $"ack={px}");
    }

    [Fact]
    public void Severity_icon_token_draws_an_icon_before_the_text()
    {
        var withIcon = DvTheme.Light.With(new Dictionary<string, string> { ["alarm-critical-icon"] = "fa-bell" });
        var (box, plain) = Render(List, ts: 0);
        var (_, icon) = Render(List, ts: 0, theme: withIcon);
        using var _1 = plain;
        using var _2 = icon;
        static int ChipRight(LayoutBox box, SKBitmap bmp)
        {
            var (r, _) = Row(box, 0);
            var y = (int)r.MidY - 8; // 칩 위쪽 띠(글자 위)
            var x = (int)(box.Rect.Left + 16 + 78 + 10);
            var chip = bmp.GetPixel(x, y);
            while (x < bmp.Width - 1 && Near(bmp.GetPixel(x + 1, y), chip, 12))
                x++;
            return x;
        }
        Assert.True(ChipRight(box, icon) > ChipRight(box, plain) + 8, $"{ChipRight(box, icon)} vs {ChipRight(box, plain)}");
    }

    [Fact]
    public void Alarm_list_default_row_height_is_the_row_step()
    {
        var (box, bmp) = Render(List);
        using var _ = bmp;
        Assert.Equal(ThemeResolver.Number(DvTheme.Light, "row.md"), box.AlarmRowHeight);
    }

    [Fact]
    public void Status_item_lamp_blinks_with_the_alarm_period()
    {
        var root = new LayoutEngine(DvTheme.Light).Layout(
            MarkupParser.Parse("<DvScreen><DvStatusItem label=\"P\" state=\"fault\" blink=\"true\"/></DvScreen>"), null, 400, 100);
        var lamp = Flatten(root).First(b => b.Node.Tag == "DvLamp");
        Assert.Equal(Painter.AlarmBlinkMs, lamp.BlinkIntervalMs);
    }

    [Fact]
    public void Calm_banner_is_neutral_when_normal_color_is_off()
    {
        var theme = DvTheme.Light.With(new Dictionary<string, string> { ["look.normal-color"] = "off" });
        var (_, bmp) = Render("<DvAlarmBanner items=\"{None}\" style=\"width:400; height:40\"/>", theme: theme, w: 400, h: 40);
        using var _ = bmp;
        var bg = bmp.GetPixel(300, 4);
        Assert.True(Near(bg, ThemeResolver.Color(theme, "surface-raised")), $"bg={bg}");
    }
}
