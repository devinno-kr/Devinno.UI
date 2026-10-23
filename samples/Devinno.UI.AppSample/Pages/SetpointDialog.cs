using Devinno.UI.App;

public sealed partial class SetpointDialog : DvDialog<double?>
{
    public SetpointDialog(double current) => Value = current;   // edit a copy of the value, not the original

    public double Value { get; set; }

    public void Ok() => Close(Value);
    public void Cancel() => Close();                            // result = null (cancel)
}
