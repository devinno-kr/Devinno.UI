using Devinno.UI.App;

public sealed partial class SetpointDialog : DvDialog<double?>
{
    public SetpointDialog(double current) => Value = current;   // 원본이 아니라 값을 복사해 편집

    public double Value { get; set; }

    public void Ok() => Close(Value);
    public void Cancel() => Close();                            // 결과 = null(취소)
}
