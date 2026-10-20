using System.Resources;
using Microsoft.CodeAnalysis;

namespace Devinno.UI.Generators;

/// <summary>
/// dvml 생성기가 내는 진단. 위치는 모두 dvml 파일 안입니다.
/// 문구는 Resources.resx(영어, 기본) · Resources.ko.resx(한국어) — 컴파일러의 UI 언어(VS 언어·빌드 환경)를 따른다.
/// </summary>
internal static class Diagnostics
{
    private const string Category = "Devinno.UI.Dvml";

    private static readonly ResourceManager Res = new("Devinno.UI.Generators.Resources", typeof(Diagnostics).Assembly);

    /// <summary>리소스 문구 — 진단 인자로 넘기면 메시지를 만들 때 그 언어로 풀린다.</summary>
    public static LocalizableString Text(string key, params string[] args) => new LocalizableResourceString(key, Res, typeof(Diagnostics), args);

    private static DiagnosticDescriptor D(string id, DiagnosticSeverity severity, string messageKey = "Message") =>
        new(id, Text(id + "_Title"), Text(id + "_" + messageKey), Category, severity, isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor BadXml = D("DVML001", DiagnosticSeverity.Error);
    public static readonly DiagnosticDescriptor DuplicateId = D("DVML002", DiagnosticSeverity.Error);
    public static readonly DiagnosticDescriptor InvalidId = D("DVML003", DiagnosticSeverity.Error);
    public static readonly DiagnosticDescriptor IdInTemplate = D("DVML004", DiagnosticSeverity.Warning);
    public static readonly DiagnosticDescriptor VmNotFound = D("DVML005", DiagnosticSeverity.Error);
    public static readonly DiagnosticDescriptor VmNotPartial = D("DVML006", DiagnosticSeverity.Error);
    public static readonly DiagnosticDescriptor VmUsedTwice = D("DVML007", DiagnosticSeverity.Error);
    public static readonly DiagnosticDescriptor ProcessNameNotFound = D("DVML008", DiagnosticSeverity.Error);
    public static readonly DiagnosticDescriptor ProcessBadPort = D("DVML009", DiagnosticSeverity.Error);
    public static readonly DiagnosticDescriptor ProcessDuplicateName = D("DVML010", DiagnosticSeverity.Error);
    public static readonly DiagnosticDescriptor ProcessUnknownValue = D("DVML011", DiagnosticSeverity.Warning);
    public static readonly DiagnosticDescriptor StyleUnknownKey = D("DVML012", DiagnosticSeverity.Warning);
    public static readonly DiagnosticDescriptor StyleUnknownToken = D("DVML013", DiagnosticSeverity.Warning);
    public static readonly DiagnosticDescriptor StyleUnknownClass = D("DVML014", DiagnosticSeverity.Warning);
    public static readonly DiagnosticDescriptor RadioNoOption = D("DVML015", DiagnosticSeverity.Warning);
    public static readonly DiagnosticDescriptor ListBoxValueAttr = D("DVML016", DiagnosticSeverity.Warning);
    public static readonly DiagnosticDescriptor WidgetUnknownValue = D("DVML017", DiagnosticSeverity.Warning);

    // DVML018 — 핸들러 속성(onclick·onbeforecommit·onack·onrowclick)이 VM 의 public 인스턴스 메서드를 가리키지 않음(실행 때 조용히 아무 일도 없다)
    public static readonly DiagnosticDescriptor HandlerNotFound = D("DVML018", DiagnosticSeverity.Error, "NotFound");
    public static readonly DiagnosticDescriptor HandlerBinding = D("DVML018", DiagnosticSeverity.Error, "Binding");
    public static readonly DiagnosticDescriptor HandlerCallForm = D("DVML018", DiagnosticSeverity.Error, "CallForm");
    public static readonly DiagnosticDescriptor HandlerNotName = D("DVML018", DiagnosticSeverity.Error, "NotName");
}
