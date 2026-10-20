using Devinno.UI.App;
// 네온/글래스 스타일 시안 — 마크업은 Pages/NeonPage.dvml (그라디언트·글로우 그림자·absolute·flex 만 사용).
public sealed partial class NeonPage : DvPage
{
    public const int Width = 1280;
    public const int Height = 800;

    public bool Online { get; set; } = true;

    public void Toggle() => Online = !Online;
}
