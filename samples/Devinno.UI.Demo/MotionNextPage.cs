using Devinno.UI.App;

// 페이지 전환 데모의 두 번째 페이지 — 기본 컨트롤 페이지로 돌아간다
public sealed partial class MotionNextPage : DvPage
{
    public void BackSlide() => App.Show<BasicPage>(DvPageTransition.Slide);
    public void BackFade() => App.Show<BasicPage>(DvPageTransition.Fade);
}
