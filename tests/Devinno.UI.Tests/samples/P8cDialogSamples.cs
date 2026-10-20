using Devinno.UI;
using Devinno.UI.App;
using Devinno.UI.Editing;
using Devinno.UI.Layout;
using Devinno.UI.Styling;
using SkiaSharp;
using Xunit;

[Trait("Category", "Sample")] // PNG 다시 찍기 — 기본 실행 제외, `dotnet test -p:RunSamples=true --filter Category=Sample`
public class P8cDialogSamples
{
    private const float W = 900, H = 560;

    private const string PageMarkup = """
        <DvPageFrame title="레시피" subtitle="생산 레시피 목록">
          <DvGrid columns="3">
            <DvCard title="레시피 01" badge="사용 중">
              <DvLabel class="label muted" text="온도 62.5 ℃ · 자동"/>
            </DvCard>
            <DvCard title="레시피 02">
              <DvLabel class="label muted" text="온도 70.0 ℃ · 수동"/>
            </DvCard>
            <DvCard title="레시피 03">
              <DvLabel class="label muted" text="온도 55.0 ℃ · 자동"/>
            </DvCard>
          </DvGrid>
          <DvRow>
            <DvSpacer/>
            <DvButton text="편집" variant="primary"/>
          </DvRow>
        </DvPageFrame>
        """;

    private const string RecipeMarkup = """
        <DvDialogFrame title="레시피 편집" subtitle="레시피 01 의 설정값을 바꿉니다" style="width:440">
          <DvStack gap="sm">
            <DvLabel class="label" text="이름"/>
            <DvInput id="name" value="{Bind Data.Name}" placeholder="레시피 이름"/>
            <DvLabel class="label" text="온도"/>
            <DvInput value="{Bind Data.Temp}" keyboard-type="numeric" min="0" max="120" unit="℃"/>
            <DvLabel class="label" text="운전 모드"/>
            <DvSelect id="mode" value="{Bind Data.Mode}">
              <DvOption value="auto" text="자동" icon="fa-robot"/>
              <DvOption value="manual" text="수동" icon="fa-hand"/>
              <DvOption value="stop" text="정지" icon="fa-stop"/>
            </DvSelect>
          </DvStack>
          <DvRow>
            <DvSpacer/>
            <DvButton text="취소" variant="secondary"/>
            <DvButton text="저장" variant="primary"/>
          </DvRow>
        </DvDialogFrame>
        """;

    public sealed class Page() : MarkupPageBase(PageMarkup);

    public class MarkupPageBase(string markup) : DvPage
    {
        protected override string MarkupSource => markup;
    }

    public sealed class Dlg(string markup) : MarkupDialog<string>(markup)
    {
        public string Name { get; set; } = "레시피 01";
        public double Temp { get; set; } = 62.5;
        public string Mode { get; set; } = "auto";
    }

    private static (DvApp App, DvSurface Root) Start(DvTheme theme)
    {
        var app = new DvApp { Theme = theme };
        app.Pages.Add(new Page());
        app.Surface.Layout(W, H);
        return (app, app.Surface);
    }

    private static Dlg Open(DvApp app, string markup)
    {
        app.ShowDialog<Dlg, string>(markup);
        app.Surface.Layout(W, H);
        return Assert.IsType<Dlg>(app.Dialogs.Top);
    }

    private static void ClickId(DvSurface root, Dlg d, string id)
    {
        var slot = root.Slots.Single(s => ReferenceEquals(s.Child, d.Surface)).Rect;
        var r = Flatten(d.Surface!.Root!).First(b => b.Node.Attributes.GetValueOrDefault("id") == id).Rect;
        var p = new SKPoint(slot.Left + r.MidX, slot.Top + r.MidY);
        root.PointerDown(p);
        root.PointerUp(p);
        root.Layout(W, H);
    }

    private static void Save(DvSurface root, string file, float w = W, float h = H)
    {
        using var bmp = new SKBitmap((int)w, (int)h);
        using (var c = new SKCanvas(bmp))
            root.RenderTo(c, w, h);
        using var fs = File.Create(Path.Combine(ReportsDir(), file));
        bmp.Encode(fs, SKEncodedImageFormat.Png, 100);
    }

    private static IEnumerable<LayoutBox> Flatten(LayoutBox b) => new[] { b }.Concat(b.Children.SelectMany(Flatten));

    [Theory]
    [InlineData("light")]
    [InlineData("dark")]
    public void Render_dialogs(string themeName)
    {
        var theme = themeName == "light" ? DvTheme.Light : DvTheme.Dark;

        // (0) 페이지 위 레시피 편집 다이얼로그
        var (app, root) = Start(theme);
        Open(app, RecipeMarkup);
        Save(root, $"phase8c-dialog-{themeName}-0.png");

        // (1) 그 위 메시지 박스(YesNo) — 아래 다이얼로그도 막 아래로 어두워진다
        _ = app.MessageBox("변경한 설정값을 저장할까요?", "저장", DvMessageButtons.YesNo);
        root.Layout(W, H);
        Save(root, $"phase8c-dialog-{themeName}-1.png");

        // (2) 다이얼로그 안 DvSelect 열림
        (app, root) = Start(theme);
        var d = Open(app, RecipeMarkup);
        ClickId(root, d, "mode");
        Save(root, $"phase8c-dialog-{themeName}-2.png");

        // (3) 다이얼로그 안 입력 + 가상 키보드(슬라이드가 끝난 뒤)
        (app, root) = Start(theme);
        d = Open(app, RecipeMarkup);
        ClickId(root, d, "name");
        Save(root, $"phase8c-dialog-{themeName}-3.png");
        Thread.Sleep((int)SoftKeyboard.AnimMs + 50);
        Save(root, $"phase8c-dialog-{themeName}-3.png");

        // (4) 내장 입력 박스 — 뜨자마자 편집 + 가상 키보드(슬라이드가 끝난 뒤)
        (app, root) = Start(theme);
        _ = app.InputBox("레시피 이름", "레시피 01");
        root.Layout(W, H);
        Save(root, $"phase8c-dialog-{themeName}-4.png");
        Thread.Sleep((int)SoftKeyboard.AnimMs + 50);
        Save(root, $"phase8c-dialog-{themeName}-4.png");

        // (5) 내장 선택 박스 — 12개(목록 스크롤), 4번 선택
        (app, root) = Start(theme);
        _ = app.SelectBox("운전 모드", ["자동", "수동", "정지", "세척", "건조", "예열", "냉각", "대기", "점검", "보정", "시운전", "비상 정지"], 4);
        root.Layout(W, H);
        root.Layout(W, H);
        Save(root, $"phase8c-dialog-{themeName}-5.png");

        // (6) 긴 글 메시지 박스 — 본문 라벨 하나가 창 폭 − 32 안에서 줄바꿈(엔진 줄바꿈)
        (app, root) = Start(theme);
        _ = app.MessageBox("레시피 01 의 설정값을 저장하면 현재 운전 중인 2공정 라인에 바로 적용됩니다. 온도·운전 모드가 바뀌므로 " +
            "진행 중인 배치가 끝난 뒤 저장하는 것을 권장합니다. 저장 후에는 이전 값으로 되돌릴 수 없으며, 변경 이력은 감사 로그에 남습니다. " +
            "계속하려면 예를, 값을 다시 확인하려면 아니요를 누르세요.", "설정 저장", DvMessageButtons.YesNo);
        root.Layout(W, H);
        Save(root, $"phase8c-dialog-{themeName}-6.png");

        // (7) 낮은 창(900×360)의 선택 박스 — 목록(스크롤 상자)이 줄어 버튼이 칸 안
        var app7 = new DvApp { Theme = theme };
        app7.Pages.Add(new Page());
        app7.Surface.Layout(W, 360);
        _ = app7.SelectBox("운전 모드", ["자동", "수동", "정지", "세척", "건조", "예열", "냉각", "대기", "점검", "보정", "시운전", "비상 정지"], 4);
        app7.Surface.Layout(W, 360);
        Save(app7.Surface, $"phase8c-dialog-{themeName}-7.png", W, 360);

        // (8) 낮은 창(900×360)에 창보다 긴 메시지 — 본문만 스크롤, 버튼 줄은 박스 안
        var app8 = new DvApp { Theme = theme };
        app8.Pages.Add(new Page());
        app8.Surface.Layout(W, 360);
        _ = app8.MessageBox(string.Join("\n", Enumerable.Range(1, 40).Select(i => $"{i}. 2공정 라인 점검 항목 — 밸브 V-{100 + i} 개도 확인 후 기록")), "점검 목록", DvMessageButtons.OkCancel);
        app8.Surface.Layout(W, 360);
        Save(app8.Surface, $"phase8c-dialog-{themeName}-8.png", W, 360);
    }

    // 줄바꿈 라벨이 든 카드 — 같은 폭 3열 그리드 칸 안에서 긴 설명이 여러 줄, 한 줄 강제(nowrap) 라벨은 한 줄
    private const string WrapCardMarkup = """
        <DvPageFrame title="설비 안내" subtitle="카드 안 긴 글은 칸 폭에서 줄바꿈">
          <DvGrid columns="3">
            <DvCard title="필터 교체" badge="점검">
              <DvLabel class="body" text="필터 교체 주기가 지났습니다. 운전을 멈춘 뒤 필터를 교체하고, 교체 후에는 누적 운전 시간을 초기화하세요."/>
              <DvLabel class="caption muted" text="TAG-1001 · 마지막 교체 2026-09-01" style="white-space:nowrap"/>
            </DvCard>
            <DvCard title="펌프 A">
              <DvLabel class="body" text="정상 운전 중입니다."/>
              <DvLabel class="caption muted" text="유량 32.8 L/min"/>
            </DvCard>
            <DvCard title="세척 예약">
              <DvLabel class="body" text="다음 CIP 세척은 오늘 22:00 에 시작합니다.&#10;세척 동안 2공정 라인은 정지합니다."/>
            </DvCard>
          </DvGrid>
        </DvPageFrame>
        """;

    [Theory]
    [InlineData("light")]
    [InlineData("dark")]
    public void Render_wrapped_card(string themeName)
    {
        var theme = themeName == "light" ? DvTheme.Light : DvTheme.Dark;
        var path = Path.Combine(ReportsDir(), $"textwrap-card-{themeName}.png");
        DvRenderer.RenderToFile(WrapCardMarkup, null, 900, 360, path, theme);
        Assert.True(new FileInfo(path).Length > 1000);
    }

    private static string ReportsDir()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var marker = Path.Combine(dir.FullName, "docs", "superpowers");
            if (Directory.Exists(marker))
            {
                var reports = Path.Combine(marker, "reports");
                Directory.CreateDirectory(reports);
                return reports;
            }

            dir = dir.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate docs/superpowers from test output.");
    }
}
