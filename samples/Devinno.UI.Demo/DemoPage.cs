using Devinno.UI.App;

// 데모 기능 페이지 공통 — 제목 줄 오른쪽의 "← Back"(쇼케이스 = Widgets 목록, -- top 시나리오 = 홈).
// BackTo 가 없으면(페이지를 실행 인자로 단독 실행) 버튼을 숨긴다.
public abstract class DemoPage : DvPage
{
    public DvPage? BackTo { get; set; }

    public bool ShowBack => BackTo is not null;

    public void Back()
    {
        if (BackTo is not null)
            App.Show(BackTo);
    }
}
