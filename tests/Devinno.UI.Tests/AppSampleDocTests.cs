using System.Runtime.CompilerServices;
using Devinno.UI.App;
using Devinno.UI.Layout;
using SkiaSharp;
using Xunit;

// 문서 16.1 전체 예제 = samples/Devinno.UI.AppSample — 문서에 파일이 그대로 실려 있는지, 그 코드가 실제로 동작하는지.
[Collection("AttachedApp")] // 다이얼로그를 띄우므로 앱을 붙이는 테스트와 같은 비병렬 묶음에
public class AppSampleDocTests
{
    private const float W = 1024, H = 640;

    [Theory]
    [InlineData("Program.cs")]
    [InlineData("Pages/TopBar.cs")]
    [InlineData("Pages/TopBar.dvml")]
    [InlineData("Pages/HomePage.cs")]
    [InlineData("Pages/HomePage.dvml")]
    [InlineData("Pages/MonitorPage.cs")]
    [InlineData("Pages/MonitorPage.dvml")]
    [InlineData("Pages/SetpointDialog.cs")]
    [InlineData("Pages/SetpointDialog.dvml")]
    public void Doc_contains_sample_file_verbatim(string file)
    {
        var root = RepoRoot();
        var code = Normalize(File.ReadAllText(Path.Combine(root, "samples", "Devinno.UI.AppSample", file)));
        var doc = Normalize(File.ReadAllText(Path.Combine(root, "dvml-ui-markup.md")));
        Assert.True(doc.Contains($"\n{code}\n```", StringComparison.Ordinal),
            $"dvml-ui-markup.md 16.1 이 samples/Devinno.UI.AppSample/{file} 와 다릅니다 — 문서 블록을 파일 내용 그대로 고치세요.");
    }

    [Fact]
    public void Sample_app_switches_pages_like_the_doc_says()
    {
        // Program.cs 와 같은 구성
        var app = new DvApp();
        app.Top = new TopBar();
        var home = new HomePage();
        var monitor = new MonitorPage();
        app.Pages.Add(home);
        app.Pages.Add(monitor);
        var root = app.Surface;

        Render(root);
        Assert.Same(home, app.CurrentPage);                 // 처음 넣은 페이지 = 시작 페이지
        Assert.Equal("tab on", ((TopBar)TopBarOf(app)).HomeClass);

        Tap(root, ButtonRect(home.Surface!, "모니터링 열기", offsetY: 56));   // 페이지 버튼
        Render(root);
        Assert.Same(monitor, app.CurrentPage);
        Assert.Equal(1, monitor.ShownCount);

        Tap(root, ButtonRect(monitor.Surface!, "← 이전", offsetY: 56));
        Render(root);
        Assert.Same(home, app.CurrentPage);

        Tap(root, ButtonRect(TopBarOf(app).Surface!, "모니터링", offsetY: 0));  // 상단 바 버튼
        Render(root);
        Assert.Same(monitor, app.CurrentPage);
        Assert.Equal(2, monitor.ShownCount);                // 보관된 페이지가 다시 보일 때마다
        Assert.Equal("tab on", ((TopBar)TopBarOf(app)).MonitorClass);
        Assert.Equal("2번째 표시", Find(monitor.Surface!.Root!, b => b.ResolvedText == "2번째 표시")?.ResolvedText);
    }

    [Fact]
    public void Sample_setpoint_dialog_returns_value_like_the_doc_says()
    {
        var app = new DvApp();
        app.Top = new TopBar();
        var monitor = new MonitorPage();
        app.Pages.Add(monitor);
        var root = app.Surface;
        Render(root);

        Tap(root, ButtonRect(monitor.Surface!, "설정 변경", offsetY: 56));
        var dialog = Assert.IsType<SetpointDialog>(app.Dialogs.Top);   // 요청 순간부터 목록에 있다
        Assert.Equal(25.0, dialog.Value);                                // 생성자 인자 = 현재 설정값(복사본)
        Render(root);                                                    // 다음 프레임에 화면이 생긴다
        Assert.NotNull(dialog.Surface);

        Tap(root, ButtonRect(monitor.Surface!, "설정 변경", offsetY: 56));  // 아래 페이지는 입력을 받지 않는다
        Render(root);
        Assert.Single(app.Dialogs);

        dialog.Value = 30;                                               // 사용자가 입력한 것으로
        Tap(root, DialogButtonRect(root, dialog, "확인"));
        Assert.Empty(app.Dialogs);                                       // 닫기 요청 즉시 빠진다
        Assert.Equal(25.0, monitor.Setpoint);                            // 결과는 다음 프레임
        Render(root);
        Assert.Equal(30.0, monitor.Setpoint);
        Assert.NotNull(Find(monitor.Surface!.Root!, b => b.ResolvedText == "설정 30 ℃"));  // await 뒤 바꾼 값도 같은 프레임에 다시 읽힌다

        Tap(root, ButtonRect(monitor.Surface!, "설정 변경", offsetY: 56));
        Render(root);
        var again = Assert.IsType<SetpointDialog>(app.Dialogs.Top);
        Assert.NotSame(dialog, again);                                   // 매번 새 인스턴스
        Tap(root, DialogButtonRect(root, again, "취소"));
        Render(root);
        Assert.Equal(30.0, monitor.Setpoint);                            // 취소 = null → 그대로
    }

    // 다이얼로그 칸(창 가운데) 기준 버튼 위치 → 창 좌표
    private static SKRect DialogButtonRect(Devinno.UI.DvSurface root, DvDialog dialog, string text)
    {
        var slot = root.Slots.Single(s => ReferenceEquals(s.Child, dialog.Surface)).Rect;
        var box = Find(dialog.Surface!.Root!, b => b.Node.Tag == "DvButton" && b.ResolvedText == text)
                  ?? throw new InvalidOperationException($"버튼 '{text}' 없음");
        var r = box.Rect;
        r.Offset(slot.Left, slot.Top);
        return r;
    }

    private static DvBar TopBarOf(DvApp app) => app.Top!;

    private static void Render(Devinno.UI.DvSurface s)
    {
        using var bmp = new SKBitmap((int)W, (int)H);
        using var canvas = new SKCanvas(bmp);
        s.RenderTo(canvas, W, H);
    }

    private static void Tap(Devinno.UI.DvSurface s, SKRect r)
    {
        var p = new SKPoint(r.MidX, r.MidY);
        s.PointerDown(p);
        s.PointerUp(p);
    }

    // 자식 화면 좌표 + 칸 위치(상단 바 높이 56) = 창 좌표
    private static SKRect ButtonRect(Devinno.UI.DvSurface child, string text, float offsetY)
    {
        var box = Find(child.Root!, b => b.Node.Tag == "DvButton" && b.ResolvedText == text)
                  ?? throw new InvalidOperationException($"버튼 '{text}' 없음");
        var r = box.Rect;
        r.Offset(0, offsetY);
        return r;
    }

    private static LayoutBox? Find(LayoutBox b, Func<LayoutBox, bool> pred)
    {
        if (pred(b)) return b;
        foreach (var c in b.Children)
            if (Find(c, pred) is { } f) return f;
        return null;
    }

    private static string Normalize(string s) => s.Replace("\r\n", "\n").TrimStart('﻿').Trim('\n');

    private static string RepoRoot([CallerFilePath] string source = "")
    {
        var dir = new DirectoryInfo(Path.GetDirectoryName(source)!);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "dvml-ui-markup.md")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("저장소 루트를 찾지 못했습니다");
    }
}
