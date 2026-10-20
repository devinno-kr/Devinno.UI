using Devinno.UI.Markup;

namespace Devinno.UI.Process;

/// <summary>
/// Built-in equipment symbols, defined in markup of the same form as a user <c>&lt;DvSymbol&gt;</c> (same parser, same rules).
/// Selected by tag (<c>DvVessel</c>, <c>DvPump</c>, <c>DvValve</c>, <c>DvInline</c>) and <c>kind</c>; attachments (<c>DvAgitator</c>, <c>DvMotor</c>) are selected by tag.
/// </summary>
internal static class BuiltInSymbols
{
    /// <summary>The kind list per tag (the first entry is the default).</summary>
    internal static readonly IReadOnlyDictionary<string, string[]> Kinds = new Dictionary<string, string[]>(StringComparer.Ordinal)
    {
        ["DvVessel"] = ["tank", "reactor", "silo", "hopper"],
        ["DvPump"] = ["centrifugal", "gear", "compressor", "blower"],
        ["DvValve"] = ["gate", "ball", "butterfly", "control", "check"],
        ["DvInline"] = ["flowmeter", "filter", "strainer", "sight"],
        ["DvValve3"] = ["ball", "control"],
        ["DvExchanger"] = ["shell", "plate"],
    };

    /// <summary>The range of a vessel face over which ports share positions (relative to the face length).</summary>
    internal const float FaceStart = 0.1f, FaceEnd = 0.9f;

    private static readonly Lazy<Dictionary<string, SymbolDef>> Defs = new(Create);

    /// <summary>The symbol for an equipment tag + kind. If kind is absent, the default kind is used; an unknown kind gives <see langword="null"/>.</summary>
    internal static SymbolDef? Get(string tag, string? kind)
    {
        if (!Kinds.TryGetValue(tag, out var kinds))
            return Defs.Value.GetValueOrDefault(tag); // 부속
        var k = string.IsNullOrEmpty(kind) ? kinds[0] : kind;
        return Defs.Value.GetValueOrDefault($"{tag}:{k}");
    }

    internal static bool IsEquipmentTag(string tag) => Kinds.ContainsKey(tag) || tag == "DvTerminal";

    internal static bool IsAccessoryTag(string tag) => tag is "DvAgitator" or "DvMotor";

    private static string Circle(float cx, float cy, float r) =>
        FormattableString.Invariant($"M{cx - r} {cy} A{r} {r} 0 1 0 {cx + r} {cy} A{r} {r} 0 1 0 {cx - r} {cy} Z");

    private const string Bowtie = "M4 20 L30 32 L4 44 Z M56 20 L30 32 L56 44 Z";
    private const string Flanges = "M0 19 H4 V45 H0 Z M56 19 H60 V45 H56 Z";
    private const string ValvePorts = """
        <DvParam name="open" type="bool"/>
        <DvPort name="in" side="left" at="0.6667" role="in"/>
        <DvPort name="out" side="right" at="0.6667" role="out"/>
        """;

    private static Dictionary<string, SymbolDef> Create()
    {
        var d = new Dictionary<string, SymbolDef>(StringComparer.Ordinal);

        void Add(string key, string markup)
        {
            var def = SymbolDef.Parse(MarkupParser.Parse(markup));
            d[key] = def;
        }

        // ── 용기: top(면, 여러 줄) · bottom(가운데) · side(좌우 중 상대 쪽 면, 여러 줄) ──
        const string vesselPorts = """
            <DvParam name="level" type="number" default="0"/>
            <DvFill clip="vessel" value="level"/>
            <DvPort name="top" side="top" at="0.5" role="inout"/>
            <DvPort name="bottom" side="bottom" at="0.5" role="inout"/>
            <DvPort name="side" side="left" at="0.5" role="inout"/>
            """;
        Add("DvVessel:tank", $"""
            <DvSymbol name="tank" view-box="0 0 100 150">
              {vesselPorts}
              <DvShape class="accent" path="M43 0 H57 V6 H43 Z M43 144 H57 V150 H43 Z"/>
              <DvShape class="vessel" path="M6 26 Q6 4 50 4 Q94 4 94 26 V124 Q94 146 50 146 Q6 146 6 124 Z"/>
            </DvSymbol>
            """);
        Add("DvVessel:reactor", $"""
            <DvSymbol name="reactor" view-box="0 0 100 160">
              {vesselPorts}
              <DvShape class="accent" path="M44 0 H56 V16 H44 Z M44 150 H56 V160 H44 Z"/>
              <DvShape class="accent" path="M2 52 V124 Q2 154 50 154 Q98 154 98 124 V52 Z"/>
              <DvShape class="vessel" path="M12 34 Q12 14 50 14 Q88 14 88 34 V122 Q88 144 50 144 Q12 144 12 122 Z"/>
            </DvSymbol>
            """);
        Add("DvVessel:silo", $"""
            <DvSymbol name="silo" view-box="0 0 100 160">
              {vesselPorts}
              <DvShape class="accent" path="M14 96 H20 V160 H14 Z M80 96 H86 V160 H80 Z M45 0 H55 V8 H45 Z M43 140 H57 V160 H43 Z"/>
              <DvShape class="vessel" path="M12 18 L50 4 L88 18 V96 L58 132 V146 H42 V132 L12 96 Z"/>
            </DvSymbol>
            """);
        Add("DvVessel:hopper", $"""
            <DvSymbol name="hopper" view-box="0 0 100 110">
              {vesselPorts}
              <DvShape class="accent" path="M0 0 H100 V7 H0 Z M42 100 H58 V110 H42 Z"/>
              <DvShape class="vessel" path="M4 7 H96 V44 L58 86 V102 H42 V86 L4 44 Z"/>
            </DvSymbol>
            """);

        // ── 회전기: in(왼쪽) → out(오른쪽), 운전 중일 때만 통함 ──
        Add("DvPump:centrifugal", $"""
            <DvSymbol name="centrifugal" view-box="0 0 64 64">
              <DvParam name="running" type="bool"/>
              <DvPort name="in" side="left" at="0.5625" role="in"/>
              <DvPort name="out" side="right" at="0.3125" role="out"/>
              <DvPass from="in" to="out" when="running"/>
              <DvShape class="accent" path="M16 54 L11 64 H49 L44 54 Z"/>
              <DvShape class="state" when="running" path="M30 14 H64 V26 H49.6 A22 22 0 1 1 8.83 42 H0 V30 H8.83 A22 22 0 0 1 30 14 Z"/>
              <DvShape class="detail" path="M30 36 Q36 28 32 22 M30 36 Q40 40 44 34 M30 36 Q24 44 26 50 M30 36 Q20 32 16 36"/>
              <DvShape class="accent" path="{Circle(30, 36, 5)}"/>
            </DvSymbol>
            """);
        Add("DvPump:gear", $"""
            <DvSymbol name="gear" view-box="0 0 72 52">
              <DvParam name="running" type="bool"/>
              <DvPort name="in" side="left" at="0.5" role="in"/>
              <DvPort name="out" side="right" at="0.5" role="out"/>
              <DvPass from="in" to="out" when="running"/>
              <DvShape class="accent" path="M0 20 H6 V32 H0 Z M66 20 H72 V32 H66 Z"/>
              <DvShape class="state" when="running" path="M10 6 H62 Q66 6 66 10 V42 Q66 46 62 46 H10 Q6 46 6 42 V10 Q6 6 10 6 Z"/>
              <DvShape class="detail" path="{Circle(27, 26, 10)} {Circle(45, 26, 10)}"/>
            </DvSymbol>
            """);
        Add("DvPump:compressor", $"""
            <DvSymbol name="compressor" view-box="0 0 72 64">
              <DvParam name="running" type="bool"/>
              <DvPort name="in" side="left" at="0.5" role="in"/>
              <DvPort name="out" side="right" at="0.5" role="out"/>
              <DvPass from="in" to="out" when="running"/>
              <DvShape class="accent" path="M0 26 H11 V38 H0 Z M61 26 H72 V38 H61 Z"/>
              <DvShape class="state" when="running" path="{Circle(36, 32, 26)}"/>
              <DvShape class="detail" path="M16 15 L57 24 M16 49 L57 40"/>
            </DvSymbol>
            """);
        Add("DvPump:blower", $"""
            <DvSymbol name="blower" view-box="0 0 72 64">
              <DvParam name="running" type="bool"/>
              <DvPort name="in" side="left" at="0.5625" role="in"/>
              <DvPort name="out" side="right" at="0.2969" role="out"/>
              <DvPass from="in" to="out" when="running"/>
              <DvShape class="state" when="running" path="M32 12 H72 V26 H53.8 A24 24 0 1 1 8.76 42 H0 V30 H8.76 A24 24 0 0 1 32 12 Z"/>
              <DvShape class="detail" path="M32 36 Q42 24 34 16 M32 36 Q46 44 52 36 M32 36 Q22 50 30 56 M32 36 Q18 26 12 34"/>
              <DvShape class="accent" path="{Circle(32, 36, 4)}"/>
            </DvSymbol>
            """);

        // ── 밸브: 열리면 통함, check 는 항상 한 방향 ──
        Add("DvValve:gate", $"""
            <DvSymbol name="gate" view-box="0 0 60 48">
              {ValvePorts}
              <DvPass from="in" to="out" when="open"/>
              <DvShape class="accent" path="{Flanges} M20 4 H40 V9 H20 Z"/>
              <DvShape class="detail" path="M30 32 V9"/>
              <DvShape class="state" when="open" path="{Bowtie}"/>
            </DvSymbol>
            """);
        Add("DvValve:ball", $"""
            <DvSymbol name="ball" view-box="0 0 60 48">
              {ValvePorts}
              <DvPass from="in" to="out" when="open"/>
              <DvShape class="accent" path="{Flanges} M30 8 H50 V13 H30 Z"/>
              <DvShape class="detail" path="M30 25 V12"/>
              <DvShape class="state" when="open" path="{Bowtie}"/>
              <DvShape class="accent" path="{Circle(30, 32, 7)}"/>
            </DvSymbol>
            """);
        Add("DvValve:butterfly", $"""
            <DvSymbol name="butterfly" view-box="0 0 60 48">
              {ValvePorts}
              <DvPass from="in" to="out" when="open"/>
              <DvShape class="accent" path="{Flanges} M22 2 H38 V12 H22 Z"/>
              <DvShape class="detail" path="M30 22 V12"/>
              <DvShape class="state" when="open" path="M6 22 H54 V42 H6 Z"/>
              <DvShape class="detail" path="M24 41 L36 23"/>
              <DvShape class="accent" path="{Circle(30, 32, 3)}"/>
            </DvSymbol>
            """);
        Add("DvValve:control", $"""
            <DvSymbol name="control" view-box="0 0 60 48">
              {ValvePorts}
              <DvPass from="in" to="out" when="open"/>
              <DvShape class="accent" path="{Flanges}"/>
              <DvShape class="detail" path="M30 32 V14"/>
              <DvShape class="accent" path="M13 14 Q30 -5 47 14 Z"/>
              <DvShape class="state" when="open" path="{Bowtie}"/>
            </DvSymbol>
            """);
        Add("DvValve:check", $"""
            <DvSymbol name="check" view-box="0 0 60 48">
              {ValvePorts}
              <DvPass from="in" to="out"/>
              <DvShape class="accent" path="{Flanges}"/>
              <DvShape class="vessel" path="M6 22 H54 V42 H6 Z"/>
              <DvShape class="accent" path="M15 25 L40 32 L15 39 Z M41 24 H44 V40 H41 Z"/>
            </DvSymbol>
            """);

        // ── 3방 밸브: a = 공통(왼쪽), b = 직진(오른쪽), c = 가지(아래). open-b·open-c 가 참인 길만 양방향으로 통함(분기·혼합) ──
        // 가로 줄(a·b)은 DvValve 와 같은 높이(y=32) — 일반 밸브와 직선이 맞는다. a 쪽 삼각형은 공통이라 상태색 없음
        const string valve3Common = """
            <DvParam name="open-b" type="bool"/>
            <DvParam name="open-c" type="bool"/>
            <DvPort name="a" side="left" at="0.5" role="inout"/>
            <DvPort name="b" side="right" at="0.5" role="inout"/>
            <DvPort name="c" side="bottom" at="0.5" role="inout"/>
            <DvPass from="a" to="b" when="open-b"/>
            <DvPass from="b" to="a" when="open-b"/>
            <DvPass from="a" to="c" when="open-c"/>
            <DvPass from="c" to="a" when="open-c"/>
            """;
        const string valve3Body = """
            <DvShape class="accent" path="M0 19 H4 V45 H0 Z M56 19 H60 V45 H56 Z M17 60 H43 V64 H17 Z"/>
            <DvShape class="vessel" path="M4 20 L30 32 L4 44 Z"/>
            <DvShape class="state" when="open-b" path="M56 20 L30 32 L56 44 Z"/>
            <DvShape class="state" when="open-c" path="M18 60 L30 32 L42 60 Z"/>
            """;
        Add("DvValve3:ball", $"""
            <DvSymbol name="valve3-ball" view-box="0 0 60 64">
              {valve3Common}
              <DvShape class="accent" path="M30 8 H50 V13 H30 Z"/>
              <DvShape class="detail" path="M30 25 V12"/>
              {valve3Body}
              <DvShape class="accent" path="{Circle(30, 32, 7)}"/>
            </DvSymbol>
            """);
        Add("DvValve3:control", $"""
            <DvSymbol name="valve3-control" view-box="0 0 60 64">
              {valve3Common}
              <DvShape class="detail" path="M30 32 V14"/>
              <DvShape class="accent" path="M13 14 Q30 -5 47 14 Z"/>
              {valve3Body}
            </DvSymbol>
            """);

        // ── 열교환기: in → out(주 공정, 좌→우), in2 → out2(열매체, 위 → 아래). 두 길은 섞이지 않고 늘 통함 ──
        Add("DvExchanger:shell", """
            <DvSymbol name="exchanger-shell" view-box="0 0 100 48">
              <DvPort name="in" side="left" at="0.5" role="in"/>
              <DvPort name="out" side="right" at="0.5" role="out"/>
              <DvPort name="in2" side="top" at="0.3" role="in"/>
              <DvPort name="out2" side="bottom" at="0.7" role="out"/>
              <DvPass from="in" to="out"/>
              <DvPass from="in2" to="out2"/>
              <DvShape class="accent" path="M0 18 H6 V30 H0 Z M94 18 H100 V30 H94 Z M25 0 H35 V8 H25 Z M65 40 H75 V48 H65 Z"/>
              <DvShape class="vessel" path="M12 8 H88 Q94 8 94 14 V34 Q94 40 88 40 H12 Q6 40 6 34 V14 Q6 8 12 8 Z"/>
              <DvShape class="detail" path="M20 9 V39 M80 9 V39 M20 17 H80 M20 24 H80 M20 31 H80"/>
            </DvSymbol>
            """);
        Add("DvExchanger:plate", """
            <DvSymbol name="exchanger-plate" view-box="0 0 64 80">
              <DvPort name="in" side="left" at="0.3" role="in"/>
              <DvPort name="out" side="right" at="0.3" role="out"/>
              <DvPort name="in2" side="top" at="0.3" role="in"/>
              <DvPort name="out2" side="bottom" at="0.7" role="out"/>
              <DvPass from="in" to="out"/>
              <DvPass from="in2" to="out2"/>
              <DvShape class="accent" path="M0 18 H6 V30 H0 Z M58 18 H64 V30 H58 Z M15 0 H23 V4 H15 Z M41 76 H49 V80 H41 Z"/>
              <DvShape class="vessel" path="M10 4 H54 V76 H10 Z"/>
              <DvShape class="detail" path="M18 8 V72 M24 8 V72 M30 8 V72 M36 8 V72 M42 8 V72 M48 8 V72"/>
              <DvShape class="accent" path="M6 2 H12 V78 H6 Z M52 2 H58 V78 H52 Z"/>
            </DvSymbol>
            """);

        // ── 인라인: 항상 통함 ──
        const string inlinePass = """<DvPass from="in" to="out"/>""";
        Add("DvInline:flowmeter", $"""
            <DvSymbol name="flowmeter" view-box="0 0 64 48">
              <DvPort name="in" side="left" at="0.6667" role="in"/>
              <DvPort name="out" side="right" at="0.6667" role="out"/>
              {inlinePass}
              <DvShape class="accent" path="M0 21 H6 V43 H0 Z M58 21 H64 V43 H58 Z M28 16 H36 V24 H28 Z"/>
              <DvShape class="vessel" path="M6 24 H58 V40 H6 Z"/>
              <DvShape class="vessel" path="M18 2 H46 Q50 2 50 6 V14 Q50 18 46 18 H18 Q14 18 14 14 V6 Q14 2 18 2 Z"/>
              <DvShape class="screen" path="M20 6 H44 V14 H20 Z"/>
            </DvSymbol>
            """);
        Add("DvInline:filter", $"""
            <DvSymbol name="filter" view-box="0 0 60 60">
              <DvPort name="in" side="left" at="0.5" role="in"/>
              <DvPort name="out" side="right" at="0.5" role="out"/>
              {inlinePass}
              <DvShape class="vessel" path="M30 6 L54 30 L30 54 L6 30 Z"/>
              <DvShape class="dash" path="M30 10 V50"/>
            </DvSymbol>
            """);
        Add("DvInline:strainer", $"""
            <DvSymbol name="strainer" view-box="0 0 64 56">
              <DvPort name="in" side="left" at="0.3571" role="in"/>
              <DvPort name="out" side="right" at="0.3571" role="out"/>
              {inlinePass}
              <DvShape class="accent" path="M0 11 H4 V29 H0 Z M60 11 H64 V29 H60 Z M35 44 H53 V50 H35 Z"/>
              <DvShape class="vessel" path="M4 13 H60 V27 H44 L51 44 H37 L32 27 H4 Z"/>
              <DvShape class="dash" path="M38 28 L44 43"/>
            </DvSymbol>
            """);
        Add("DvInline:sight", $"""
            <DvSymbol name="sight" view-box="0 0 64 48">
              <DvPort name="in" side="left" at="0.5" role="in"/>
              <DvPort name="out" side="right" at="0.5" role="out"/>
              {inlinePass}
              <DvShape class="accent" path="M0 13 H6 V35 H0 Z M58 13 H64 V35 H58 Z"/>
              <DvShape class="vessel" path="M6 16 H58 V32 H6 Z"/>
              <DvShape class="accent" path="{Circle(32, 24, 13)}"/>
              <DvShape class="glass" path="{Circle(32, 24, 9)}"/>
            </DvSymbol>
            """);

        // ── 부속(포트 없음): 장비 위에 얹힌다 ──
        Add("DvAgitator", """
            <DvSymbol name="agitator" view-box="0 0 40 100">
              <DvParam name="running" type="bool"/>
              <DvShape class="state" when="running" path="M8 0 H32 Q34 0 34 2 V20 Q34 22 32 22 H8 Q6 22 6 20 V2 Q6 0 8 0 Z"/>
              <DvShape class="accent" path="M12 22 H28 V30 H12 Z"/>
              <DvShape class="accent" path="M18.5 30 H21.5 V90 H18.5 Z"/>
              <DvShape class="detail" path="M10 4 V18 M15 4 V18 M20 4 V18 M25 4 V18 M30 4 V18"/>
              <DvShape class="accent" path="M20 90 L3 84 V95 Z M20 90 L37 84 V95 Z"/>
            </DvSymbol>
            """);
        Add("DvMotor", """
            <DvSymbol name="motor" view-box="0 0 76 46">
              <DvParam name="running" type="bool"/>
              <DvShape class="accent" path="M12 40 H52 V46 H12 Z M0 18 H8 V26 H0 Z M54 8 H62 Q66 8 66 12 V32 Q66 36 62 36 H54 Z M24 0 H38 V4 H24 Z"/>
              <DvShape class="state" when="running" path="M8 8 Q8 4 12 4 H50 Q54 4 54 8 V36 Q54 40 50 40 H12 Q8 40 8 36 Z"/>
              <DvShape class="detail" path="M16 8 V36 M24 8 V36 M32 8 V36 M40 8 V36 M48 8 V36"/>
            </DvSymbol>
            """);

        // 용기 포트 성질(마크업 밖 — 내장 전용): top·side 는 면 포트, side 는 상대 쪽 면
        foreach (var (key, def) in d)
        {
            if (!key.StartsWith("DvVessel:", StringComparison.Ordinal))
                continue;
            for (var i = 0; i < def.Ports.Count; i++)
            {
                var p = def.Ports[i];
                if (p.Name == "top")
                    def.Ports[i] = p with { Face = true };
                else if (p.Name == "side")
                    def.Ports[i] = p with { Face = true, EitherSide = true };
            }
        }

        return d;
    }
}
