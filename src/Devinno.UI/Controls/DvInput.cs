using System.Globalization;
using Devinno.UI.Layout;

namespace Devinno.UI.Controls;

/// <summary>
/// Handle for a <c>DvInput</c> element. Reads and writes <see cref="Text"/> (text) and, for numeric input, <see cref="Value"/> (value ownership rules are the same as <see cref="DvToggle"/>).
/// </summary>
/// <remarks>
/// When there is no <c>value</c> and the handle owns the value, a numeric input (<c>keyboard-type="numeric"</c>) holds a <see cref="double"/> value; any other input holds text.
/// On-screen editing changes the value once, when editing ends (Done, Enter, or focus lost), not on every keystroke.
/// </remarks>
public sealed class DvInput : DvControl
{
    internal DvInput(string id) : base(id) => Slot = new ValueSlot(new ValueCell<string?>(), canOwn: true);

    private bool Numeric => Node?.Attributes.GetValueOrDefault("keyboard-type") == "numeric";

    internal override void OnAttached()
    {
        // 칸 형식 = 숫자 입력이면 double?(정수 규칙 없는 실수), 아니면 글자. 형식이 바뀌면 값을 옮겨 담는다.
        var want = Numeric ? typeof(double?) : typeof(string);
        if (Slot!.CellType == want)
            return;
        var old = Slot.CellValue;
        Slot = Numeric
            ? new ValueSlot(new ValueCell<double?>(), canOwn: true) { CellValue = ValueConvert.ToDouble(old) }
            : new ValueSlot(new ValueCell<string?>(), canOwn: true) { CellValue = ValueConvert.ToText(old) };
    }

    /// <summary>
    /// Gets or sets the text (numbers use <see cref="CultureInfo.InvariantCulture"/> notation; <c>format</c> is not applied). <see langword="null"/> if there is no value.
    /// When set on a numeric input, the text is parsed as a number: non-numeric text throws <see cref="FormatException"/>, and <see langword="null"/> or empty text means no value.
    /// </summary>
    /// <exception cref="FormatException">Non-numeric text was set on a numeric input.</exception>
    public string? Text
    {
        get => ValueConvert.ToText(CurrentValue());
        set
        {
            if (!Numeric)
            {
                SetCurrent(value);
                return;
            }

            SetCurrent(string.IsNullOrWhiteSpace(value)
                ? null
                : double.Parse(value, NumberStyles.Float, CultureInfo.InvariantCulture));
        }
    }

    /// <summary>
    /// Gets or sets the numeric value (<see langword="null"/> if it cannot be read as a number). On a non-numeric input, a set value is converted to text.
    /// If the bound property is an integer, the value is converted to that type.
    /// </summary>
    public double? Value
    {
        get => ValueConvert.ToDouble(CurrentValue());
        set => SetCurrent(Numeric ? value : value?.ToString("R", CultureInfo.InvariantCulture));
    }

    /// <summary>Raised when the value (text) changes (same rules as <see cref="DvToggle.CheckedChanged"/>).</summary>
    public event EventHandler<DvValueChangedEventArgs>? TextChanged
    {
        add { lock (EventGate) _text += value; }
        remove { lock (EventGate) _text -= value; }
    }

    /// <summary>Raised when the value of a numeric input changes (right after <see cref="TextChanged"/>; not raised for non-numeric inputs).</summary>
    public event EventHandler<DvValueChangedEventArgs>? ValueChanged
    {
        add { lock (EventGate) _value += value; }
        remove { lock (EventGate) _value -= value; }
    }

    private EventHandler<DvValueChangedEventArgs>? _text;
    private EventHandler<DvValueChangedEventArgs>? _value;
    private protected override string ValueEventName => "TextChanged";
    private protected override object? ValueFromBox(LayoutBox box) => box.InputValue;

    private protected override void OnValueChanged(DvValueChangedEventArgs e)
    {
        _text?.Invoke(this, e);
        if (Numeric)
            _value?.Invoke(this, e);
    }
}
