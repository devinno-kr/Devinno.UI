using Microsoft.CodeAnalysis;

namespace Devinno.UI.Generators;

/// <summary>dvml 생성기가 내는 진단. 위치는 모두 dvml 파일 안입니다.</summary>
internal static class Diagnostics
{
    private const string Category = "Devinno.UI.Dvml";

    public static readonly DiagnosticDescriptor BadXml = new(
        "DVML001", "dvml XML 문법 오류",
        "dvml 을 XML 로 읽지 못했습니다: {0}",
        Category, DiagnosticSeverity.Error, isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor DuplicateId = new(
        "DVML002", "id 중복",
        "id '{0}' 가 이 dvml 안에 이미 있습니다 — id 는 파일 안에서 하나만 쓸 수 있습니다",
        Category, DiagnosticSeverity.Error, isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor InvalidId = new(
        "DVML003", "C# 식별자가 아닌 id",
        "id '{0}' 는 C# 필드 이름으로 쓸 수 없습니다(영문자·_ 로 시작, 키워드 불가)",
        Category, DiagnosticSeverity.Error, isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor IdInTemplate = new(
        "DVML004", "반복·컴포넌트 안의 id",
        "id '{0}' 는 <{1}> 안에 있어 여러 번 복제되므로 필드를 만들지 않습니다 — 바인딩이나 onclick=\"M(항목)\" 으로 다루세요",
        Category, DiagnosticSeverity.Warning, isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor VmNotFound = new(
        "DVML005", "vm 형식을 찾을 수 없음",
        "vm 형식 '{0}' 을 찾을 수 없습니다(중첩되지 않은 최상위 비제네릭 클래스를 '네임스페이스.클래스' 로 지정하세요)",
        Category, DiagnosticSeverity.Error, isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor VmNotPartial = new(
        "DVML006", "vm 형식이 partial 이 아님",
        "vm 형식 '{0}' 이 partial 이 아닙니다 — 생성된 필드를 붙이려면 'partial class' 로 선언하세요",
        Category, DiagnosticSeverity.Error, isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor VmUsedTwice = new(
        "DVML007", "같은 vm 을 지정한 dvml 이 여럿",
        "vm 형식 '{0}' 을 지정한 dvml 이 둘 이상입니다 — VM 하나에 dvml 하나만 연결할 수 있습니다",
        Category, DiagnosticSeverity.Error, isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor ProcessNameNotFound = new(
        "DVML008", "공정도 이름을 찾을 수 없음",
        "{0}: '{1}' 이(가) 이 공정도에 없습니다{2}",
        Category, DiagnosticSeverity.Error, isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor ProcessBadPort = new(
        "DVML009", "공정도 포트 오류",
        "{0}: {1}",
        Category, DiagnosticSeverity.Error, isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor ProcessDuplicateName = new(
        "DVML010", "공정도 이름 중복",
        "이름 '{0}' 이(가) 이 공정도에 이미 있습니다 — 장비·계기 이름은 공정도(유닛) 안에서 하나씩입니다",
        Category, DiagnosticSeverity.Error, isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor ProcessUnknownValue = new(
        "DVML011", "공정도에 없는 값",
        "{0}='{1}' 은(는) 없는 값입니다({2}) — 실행 때 기본값으로 그립니다",
        Category, DiagnosticSeverity.Warning, isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor StyleUnknownKey = new(
        "DVML012", "없는 스타일 속성",
        "스타일 속성 '{0}' 은(는) 지원하지 않습니다 — 실행 때 조용히 무시됩니다{1}",
        Category, DiagnosticSeverity.Warning, isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor StyleUnknownToken = new(
        "DVML013", "없는 테마 토큰",
        "토큰 '{0}' 은(는) 내장 테마에 없습니다(실행 때 자홍색 #FF00FF) — C# 에서 만든 테마(new DvTheme)의 토큰이면 .editorconfig 에서 dotnet_diagnostic.DVML013.severity = none 으로 끕니다",
        Category, DiagnosticSeverity.Warning, isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor StyleUnknownClass = new(
        "DVML014", "없는 클래스",
        "클래스 '{0}' 은(는) 내장 클래스·프로젝트 dvml 의 <DvStyle> 에 없습니다(실행 때 조용히 무시) — C# 으로 등록한 스타일(테마 스타일시트·컴포넌트)의 클래스면 .editorconfig 에서 dotnet_diagnostic.DVML014.severity = none 으로 끕니다",
        Category, DiagnosticSeverity.Warning, isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor RadioNoOption = new(
        "DVML015", "DvRadio 에 option 없음",
        "DvRadio 에 option 이 없습니다 — 이 버튼은 아무 값도 고르지 않고 켜지지 않습니다(value 는 묶음 값, option 은 이 버튼이 고르는 값)",
        Category, DiagnosticSeverity.Warning, isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor ListBoxValueAttr = new(
        "DVML016", "DvListBox 값 속성이 모드와 맞지 않음",
        "{0} — 실행 때 무시됩니다",
        Category, DiagnosticSeverity.Warning, isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor WidgetUnknownValue = new(
        "DVML017", "위젯 속성에 없는 값",
        "{0} {1}='{2}' 은(는) 없는 값입니다({3}) — 실행 때 {4} 로 동작",
        Category, DiagnosticSeverity.Warning, isEnabledByDefault: true);
}
