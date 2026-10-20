using Devinno.UI.Markup;

namespace Devinno.UI.Components;

/// <summary>
/// A component definition (name, parameters, template) that <see cref="ComponentRegistry.Register"/> builds by parsing <c>&lt;DvComponent&gt;</c> markup.
/// </summary>
/// <remarks>Read by the layout engine when it expands a component tag. App code usually does not create this directly; use <see cref="ComponentRegistry.Register"/>.</remarks>
public sealed class ComponentDef
{
    /// <summary>Gets the component name (<c>&lt;DvComponent name="…"&gt;</c>). Markup uses this name as the tag (case-sensitive).</summary>
    public required string Name { get; init; }
    /// <summary>Gets the parameters declared with <c>&lt;DvParam&gt;</c> (in declaration order).</summary>
    public List<ComponentParam> Params { get; } = new();
    /// <summary>Gets the template root element expanded at each use of the component (the first child that is not <c>&lt;DvParam&gt;</c> or <c>&lt;DvStyle&gt;</c>).</summary>
    public required ElementNode Template { get; init; }

    // 정의 안 DvStyle 의 CSS(문서 순서). 레이아웃 엔진이 화면 DvStyle 보다 먼저 전역 스타일시트에 더한다.
    internal List<string> Styles { get; } = new();
}
