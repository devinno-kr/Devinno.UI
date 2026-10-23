using Devinno.UI.App;
// DvKnob 드래그 확인용 데모 — P6bKnobSamples 와 같은 화면.
public sealed partial class KnobPage : DemoPage
{
    public const int Width = 760;
    public const int Height = 340;


    public double Valve { get; set; } = 45;
    public double Speed { get; set; } = 3600;
    public double Temp { get; set; } = 0;
}
