using Devinno.UI.App;
using Devinno.UI.Scene3D;

namespace Devinno.UI.Scene3D.Demo;

/// <summary>측정용 화면: 3D 뷰 하나가 화면 전체를 쓴다. 매 프레임 State 를 바꿔 다시 그리게 한다.</summary>
public sealed partial class BenchPage : DvPage
{
    public SceneState State { get; } = new();
}
