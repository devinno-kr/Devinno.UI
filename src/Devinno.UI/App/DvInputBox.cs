namespace Devinno.UI.App;

/// <summary>The built-in text input dialog shown by <see cref="DvApp.InputBox"/>. It is itself the VM of its markup. Input editing starts as soon as it opens.</summary>
internal sealed class DvInputBoxDialog : DvOkCancelDialog<string?>
{
    private const string Markup = """
        <DvDialogFrame title="{Caption}" style="width:360">
          <DvInput value="{Bind Value}"/>
          <DvRow gap="sm">
            <DvSpacer/>
            <DvButton text="{CancelText}" variant="secondary" onclick="PressCancel"/>
            <DvButton text="{OkText}" variant="primary" onclick="PressOk"/>
          </DvRow>
        </DvDialogFrame>
        """;

    public DvInputBoxDialog(string caption, string? value)
    {
        Caption = caption;
        Value = value ?? "";
    }

    protected override string MarkupSource => Markup;

    public string Caption { get; }

    /// <summary>The input value (written on edit commit).</summary>
    public string Value { get; set; }

    public void PressOk() => Close(Value);

    // 맨 위 다이얼로그가 될 때까지 기다렸다가 한 번 편집 시작 — 같은 프레임에 다른 박스가 위에 뜨면 아래에서 편집하지 않는다(닫히는 중이면 시작 안 함)
    internal override bool AfterFirstLayout()
    {
        if (!IsTopOpen)
            return false;
        Surface?.BeginEditFirstInput();
        return true;
    }

    // 완료(Enter)로 받아들여진 편집 = 확인(현장에서 두 번 누르지 않게). 거절된 커밋은 여기 오지 않는다. 열려 있고 맨 위일 때만.
    internal override void AfterEditCommitted(DvEditExit exit)
    {
        if (exit == DvEditExit.Enter && IsTopOpen)
            PressOk();
    }
}

/// <summary>The built-in numeric input dialog shown by <see cref="DvApp.InputNumber"/>. The numeric keypad and range (<c>min</c>/<c>max</c>) follow the <c>DvInput</c> rules as is — out-of-range input is not written to the VM.</summary>
internal sealed class DvNumberBoxDialog : DvOkCancelDialog<double?>
{
    private const string Markup = """
        <DvDialogFrame title="{Caption}" style="width:360">
          <DvInput value="{Bind Value}" keyboard-type="numeric" min="{Min}" max="{Max}" unit="{Unit}"/>
          <DvRow gap="sm">
            <DvSpacer/>
            <DvButton text="{CancelText}" variant="secondary" onclick="PressCancel"/>
            <DvButton text="{OkText}" variant="primary" onclick="PressOk"/>
          </DvRow>
        </DvDialogFrame>
        """;

    public DvNumberBoxDialog(string caption, double value, double? min, double? max)
        : this(caption, value, min, max, null)
    {
    }

    // 노브 탭 키패드용 — 입력 칸에 노브 단위를 보인다
    public DvNumberBoxDialog(string caption, double value, double? min, double? max, string? unit)
    {
        Caption = caption;
        Value = value;
        Min = min;
        Max = max;
        Unit = unit;
    }

    protected override string MarkupSource => Markup;

    public string Caption { get; }

    /// <summary>The input value (written on edit commit — not written if out of range).</summary>
    public double Value { get; set; }

    public double? Min { get; }

    public double? Max { get; }

    public string? Unit { get; }

    public void PressOk() => Close(Value);

    // 맨 위 다이얼로그가 될 때까지 기다렸다가 한 번 편집 시작 — 같은 프레임에 다른 박스가 위에 뜨면 아래에서 편집하지 않는다(닫히는 중이면 시작 안 함)
    internal override bool AfterFirstLayout()
    {
        if (!IsTopOpen)
            return false;
        Surface?.BeginEditFirstInput();
        return true;
    }

    // 완료(Enter)로 받아들여진 편집 = 확인(현장에서 두 번 누르지 않게). 거절된 커밋은 여기 오지 않는다. 열려 있고 맨 위일 때만.
    internal override void AfterEditCommitted(DvEditExit exit)
    {
        if (exit == DvEditExit.Enter && IsTopOpen)
            PressOk();
    }
}
