using Devinno.UI.App;

// 잠금 화면 — App.Lock<LockDialog>(비밀번호) 로 맨 위 잠금 층에 뜬다. 맞으면 Close(true) = 잠금 해제, 틀리면 그대로.
// 인증은 앱 몫이라 데모는 고정 비밀번호를 비교한다.
public sealed partial class LockDialog : DvDialog<bool>
{
    private readonly string _pin;

    public LockDialog(string pin) => _pin = pin;

    public string Password { get; set; } = "";
    public string Error { get; set; } = "";
    public bool HasError => !string.IsNullOrEmpty(Error); // visible 은 참/거짓 — 글자를 바로 묶지 않는다

    public void Login()
    {
        if (Password == _pin)
        {
            Close(true);
            return;
        }
        Error = "Wrong password";
        Password = "";
    }
}
