<!-- en-hash: e3b0c44298fc1c14 -->
# Devinno.UI
<!-- en-hash: 3d339c6b3fe688d0 -->

[English](README.md) | 한국어

SkiaSharp 기반 .NET UI 라이브러리입니다(.NET 9 이상). 산업용 HMI(터치 패널)와 일반 앱 화면을 대상으로 합니다.

화면은 CSS 스타일 인라인 `style`을 쓰는 XML 마크업으로 작성하고, C# 뷰모델(VM)에 바인딩합니다. 마크업은 런타임에 파싱되므로 빌드 없이 바로 렌더링됩니다.

> 현재 최초 버전을 개발 중입니다. API는 바뀔 수 있습니다.

## 구성
<!-- en-hash: b350800029f05762 -->

| 경로 | 설명 |
|---|---|
| `src/Devinno.UI` | 코어 라이브러리 (마크업 파서, 레이아웃, 렌더링, 컴포넌트) |
| `src/Devinno.UI.OpenTK` | OpenTK 호스트 (`ViewWindow`) |
| `src/Devinno.UI.WinForms` | WinForms 호스트 (`ViewControl`) |
| `tests/Devinno.UI.Tests` | xUnit 테스트 |
| `samples/Devinno.UI.Demo` | OpenTK 데모 앱 |
| `samples/Devinno.UI.Demo.WinForms` | WinForms 데모 앱 |

## 시작하기
<!-- en-hash: c687156ca18f50a6 -->

화면 한 장 = `DvPage` 를 상속한 클래스 + 같은 이름의 `.dvml`. `DvApp` 이 페이지를 담아 창(OpenTK `ViewWindow` 또는 WinForms `ViewControl`)에 올립니다.

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

페이지 안에서는 `App.Show<AlarmPage>()` 로 전환합니다. 자세한 내용은 [`dvml-ui-markup.md`](dvml-ui-markup.md) 16장, 데모는 `samples/Devinno.UI.Demo`(`-- top`, `-- nav`) 입니다.

## 문서
<!-- en-hash: 41472f71292db370 -->

- [`llm-rules.md`](llm-rules.md): 작성 규칙 — 원칙, CSS 와 다른 점, 자주 하는 실수, 화면 확인 방법 (먼저 읽기)
- [`dvml-ui-markup.md`](dvml-ui-markup.md): 마크업 레퍼런스 — 태그·속성·스타일 키·테마 토큰
- [`dvml-process.md`](dvml-process.md): 공정도(P&ID) 작성 가이드 — `DvProcess` 장비·연결·배치 규칙·사용자 심볼
- C# API: 코드의 XML 문서 주석 (IntelliSense, 패키지의 `Devinno.UI.xml`)
- 위 세 md 는 NuGet 패키지의 `docs/` 에도 들어 있습니다.



