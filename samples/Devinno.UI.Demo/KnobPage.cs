using Devinno.UI.App;
// DvKnob 드래그 확인용 데모 — P6bKnobSamples 와 같은 화면.
public sealed partial class KnobPage : DvPage
{
    public const int Width = 760;
    public const int Height = 340;

    /// <summary>시나리오 데모(<c>-- top</c>)에서 "이전" 버튼을 보일지(기본 숨김 — 숨기면 화면에 요소가 없다).</summary>
    public bool ShowBack { get; set; }

    public void Back() => App.Show<HomePage>();

    public double Valve { get; set; } = 45;
    public double Speed { get; set; } = 3600;
    public double Temp { get; set; } = 0;
}
