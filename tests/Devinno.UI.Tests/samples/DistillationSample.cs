using Devinno.UI;
using Devinno.UI.Styling;
using Xunit;
using Xunit.Abstractions;

// 복잡한 P&ID(증류 공정) — 좌표 배치(placement="fixed"). 크기 결정 절차를 직접 따라 보는 견본. 렌더 결과·경고를 출력한다
[Trait("Category", "Sample")]
public class DistillationSample(ITestOutputHelper output)
{
    internal const string Markup = """
        <DvTerminal name="FEED" text="FEED&#10;FROM TK-100" x="80" y="470"/>
        <DvPump name="P101A" running="true" label="P-101A" x="230" y="400"/>
        <DvPump name="P101B" label="P-101B" x="230" y="540"/>
        <DvExchanger name="E101" label="E-101" x="400" y="470"/>
        <DvTerminal name="STM1" text="LP STEAM" x="270" y="300"/>
        <DvTerminal name="CND1" text="CONDENSATE" x="560" y="555"/>
        <DvInline name="FE101" kind="flowmeter" label="FE-101" x="540" y="470"/>
        <DvValve name="FV101" kind="control" open="true" label="FV-101" x="650" y="470"/>
        <DvVessel name="T201" kind="column" height="380" level="45" label="T-201" x="800" y="470"/>

        <DvValve name="PSV201" kind="relief" label="PSV-201" x="920" y="250"/>
        <DvTerminal name="FLR1" text="TO FLARE" x="1060" y="272"/>
        <DvExchanger name="E201" label="E-201" x="1000" y="110"/>
        <DvTerminal name="CWS1" text="CWS" x="860" y="40"/>
        <DvTerminal name="CWR1" text="CWR" x="1110" y="215"/>
        <DvVessel name="D201" kind="drum" level="55" label="D-201" x="1300" y="120"/>
        <DvValve name="PV201" kind="control" open="true" label="PV-201" x="1490" y="100"/>
        <DvTerminal name="FLR2" text="TO FLARE" x="1640" y="100"/>
        <DvPump name="P202A" running="true" label="P-202A" x="1290" y="270"/>
        <DvPump name="P202B" label="P-202B" x="1290" y="380"/>
        <DvInline name="FE202" kind="flowmeter" label="FE-202" x="1470" y="330">
          <DvAttach side="bottom"><DvLabel class="caption muted" text="REFLUX RATIO"/><DvLabel class="title" text="2.4"/></DvAttach>
        </DvInline>
        <DvValve name="FV202" kind="control" open="true" flip="h" label="FV-202" x="1060" y="450"/>
        <DvValve name="LV201" kind="control" open="true" label="LV-201" x="1590" y="330"/>
        <DvTerminal name="DIST" text="DISTILLATE" x="1720" y="330"/>

        <DvExchanger name="E202" label="E-202" flip="h" x="640" y="810"/>
        <DvValve name="TV201" kind="control" open="true" label="TV-201" x="470" y="725"/>
        <DvTerminal name="STM2" text="LP STEAM" x="330" y="725"/>
        <DvValve name="CV1" kind="check" label="CHK" x="690" y="915"/>
        <DvTerminal name="CND2" text="CONDENSATE" x="840" y="915"/>
        <DvPump name="P203A" running="true" label="P-203A" x="990" y="760"/>
        <DvPump name="P203B" label="P-203B" x="990" y="880"/>
        <DvExchanger name="E203" label="E-203" x="1160" y="820"/>
        <DvTerminal name="CWS2" text="CWS" x="1020" y="700"/>
        <DvTerminal name="CWR2" text="CWR" x="1280" y="915"/>
        <DvValve name="LV202" kind="control" open="true" label="LV-202" x="1370" y="820"/>
        <DvTerminal name="BTM" text="BOTTOMS" x="1510" y="820"/>

        <DvLine from="FEED" to="P101A.in" medium="oil"/>
        <DvLine from="FEED" to="P101B.in"/>
        <DvLine from="P101A.out" to="E101.in"/>
        <DvLine from="P101B.out" to="E101.in"/>
        <DvLine from="STM1" to="E101.in2" medium="steam"/>
        <DvLine from="E101.out2" to="CND1" medium="water"/>
        <DvLine from="E101.out" to="FE101.in"/>
        <DvLine from="FE101.out" to="FV101.in"/>
        <DvLine from="FV101.out" to="T201.left@0.5" arrow="end"/>
        <DvLine from="T201.top" to="E201.in" medium="gas"/>
        <DvLine from="T201.right@0.1" to="PSV201.in"/>
        <DvLine from="PSV201.out" to="FLR1"/>
        <DvLine from="CWS1" to="E201.in2" medium="water"/>
        <DvLine from="E201.out2" to="CWR1"/>
        <DvLine from="E201.out" to="D201.top" medium="oil"/>
        <DvLine from="D201.right@0.3" to="PV201.in" medium="gas"/>
        <DvLine from="PV201.out" to="FLR2"/>
        <DvLine from="D201.bottom" to="P202A.in"/>
        <DvLine from="D201.bottom" to="P202B.in"/>
        <DvLine from="P202A.out" to="FE202.in"/>
        <DvLine from="P202B.out" to="FE202.in"/>
        <DvLine from="FE202.out" to="FV202.in"/>
        <DvLine from="FE202.out" to="LV201.in"/>
        <DvLine from="FV202.out" to="T201.right@0.3" arrow="end"/>
        <DvLine from="LV201.out" to="DIST"/>
        <DvLine from="T201.bottom" to="E202.in"/>
        <DvLine from="E202.out" to="T201.left@0.9" arrow="end"/>
        <DvLine from="STM2" to="TV201.in" medium="steam"/>
        <DvLine from="TV201.out" to="E202.in2"/>
        <DvLine from="E202.out2" to="CV1.in" medium="water"/>
        <DvLine from="CV1.out" to="CND2"/>
        <DvLine from="T201.bottom" to="P203A.in"/>
        <DvLine from="T201.bottom" to="P203B.in"/>
        <DvLine from="P203A.out" to="E203.in"/>
        <DvLine from="P203B.out" to="E203.in"/>
        <DvLine from="CWS2" to="E203.in2" medium="water"/>
        <DvLine from="E203.out2" to="CWR2"/>
        <DvLine from="E203.out" to="LV202.in"/>
        <DvLine from="LV202.out" to="BTM"/>

        <DvInstrument name="FT101" tag="FT" number="101" on="FE101"/>
        <DvInstrument name="FIC101" tag="FIC" number="101" on="FV101" panel="true" value="38.2 m³/h"/>
        <DvInstrument name="PT201" tag="PT" number="201" on="D201"/>
        <DvInstrument name="PIC201" tag="PIC" number="201" on="PV201" panel="true" value="1.8 barg"/>
        <DvInstrument name="TT201" tag="TT" number="201" tap="T201.right@0.62"/>
        <DvInstrument name="TIC201" tag="TIC" number="201" on="TV201" panel="true" value="96.4 °C"/>
        <DvInstrument name="LT201" tag="LT" number="201" on="D201"/>
        <DvInstrument name="LIC201" tag="LIC" number="201" on="LV201" panel="true" value="55 %"/>
        <DvInstrument name="FT202" tag="FT" number="202" on="FE202"/>
        <DvInstrument name="FIC202" tag="FIC" number="202" on="FV202" panel="true" value="21.0 m³/h"/>
        <DvInstrument name="LT202" tag="LT" number="202" tap="T201.right@0.82"/>
        <DvInstrument name="LIC202" tag="LIC" number="202" on="LV202" panel="true" value="45 %"/>
        <DvInstrument name="TI203" tag="TI" number="203" tap="E203.out" value="42 °C"/>
        <DvSignal from="FT101" to="FIC101"/>
        <DvSignal from="FIC101" to="FV101"/>
        <DvSignal from="PT201" to="PIC201"/>
        <DvSignal from="PIC201" to="PV201"/>
        <DvSignal from="TT201" to="TIC201"/>
        <DvSignal from="TIC201" to="TV201"/>
        <DvSignal from="LT201" to="LIC201"/>
        <DvSignal from="LIC201" to="LV201"/>
        <DvSignal from="FT202" to="FIC202"/>
        <DvSignal from="FIC202" to="FV202"/>
        <DvSignal from="LT202" to="LIC202"/>
        <DvSignal from="LIC202" to="LV202"/>
        """;

    public const string BaseSize = "1800 980";

    [Theory]
    [InlineData("fit=\"contain\" style=\"height:0; font-size:21\"", 1832, 1012, "dist-contain-base-font21.png", true)]
    [InlineData("fit=\"contain\" style=\"height:0; font-size:21\"", 1280, 720, "dist-contain-1280-font21.png", true)]
    public void Render(string attrs, int w, int h, string file, bool baseSize)
    {
        var warnings = new List<string>();
        var listener = new Listener(warnings);
        System.Diagnostics.Trace.Listeners.Add(listener);
        var sw = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            var path = Path.Combine(ReportsDir(), file);
            DvRenderer.RenderToFile($"<DvPageFrame style=\"padding:16\"><DvProcess look=\"rich\" placement=\"fixed\" {(baseSize ? $"base-size=\"{BaseSize}\"" : "")} grow=\"1\" {attrs}{(attrs.Contains("style=") ? "" : " style=\"height:0\"")}>{Markup}</DvProcess></DvPageFrame>",
                new object(), w, h, path, DvTheme.Dark);
        }
        finally
        {
            System.Diagnostics.Trace.Listeners.Remove(listener);
        }

        output.WriteLine($"{file}: {sw.ElapsedMilliseconds} ms");
        foreach (var m in warnings.Distinct())
            output.WriteLine(m);
    }

    private sealed class Listener(List<string> sink) : System.Diagnostics.TraceListener
    {
        public override void Write(string? message) { }
        public override void WriteLine(string? message) { }
        public override void TraceEvent(System.Diagnostics.TraceEventCache? c, string source, System.Diagnostics.TraceEventType t, int id, string? message)
        {
            lock (sink)
                sink.Add(message ?? "");
        }
    }

    private static string ReportsDir()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "docs", "superpowers")))
            dir = dir.Parent;
        return Path.Combine(dir!.FullName, "docs", "superpowers", "reports");
    }
}
