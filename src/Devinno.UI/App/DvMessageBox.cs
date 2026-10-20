using Devinno.UI.Localization;

namespace Devinno.UI.App;

/// <summary>
/// The button the user pressed in a message box (<see cref="DvApp.MessageBox"/>).
/// </summary>
public enum DvDialogResult
{
    /// <summary>The Cancel button was pressed, or the box was closed from outside (<see cref="DvApp.CloseDialogs"/> or window close; this also applies to boxes without a Cancel button).</summary>
    Cancel,

    /// <summary>The OK button was pressed.</summary>
    Ok,

    /// <summary>The Yes button was pressed.</summary>
    Yes,

    /// <summary>The No button was pressed.</summary>
    No
}

/// <summary>
/// The set of buttons to show in a message box (<see cref="DvApp.MessageBox"/>). Buttons are laid out left to right, with the primary button (OK/Yes) on the far right.
/// </summary>
public enum DvMessageButtons
{
    /// <summary>[OK].</summary>
    Ok,

    /// <summary>[Cancel, OK].</summary>
    OkCancel,

    /// <summary>[No, Yes].</summary>
    YesNo,

    /// <summary>[Cancel, No, Yes].</summary>
    YesNoCancel
}

/// <summary>The built-in dialog shown by <see cref="DvApp.MessageBox"/>. It is itself the VM of its markup.</summary>
internal sealed class DvMessageBoxDialog : DvDialog<DvDialogResult?>
{
    private readonly DvMessageButtons _buttons;

    // 본문은 라벨 하나 — 엔진이 칸 폭(창 폭 − 32 까지)에 맞춰 줄바꿈한다. 폭 = 글자 한 줄 폭 + 틀 여백, 최소 360(최대는 다이얼로그 칸).
    // 본문은 스크롤 상자 안 — 높이는 내용 높이이고, 창보다 길면 칸 높이에 맞춰 줄어 본문만 스크롤된다(버튼 줄은 칸 안).
    private const string Markup = """
        <DvDialogFrame title="{Caption}" style="min-width:360">
          <DvStack gap="none" style="overflow:scroll">
            <DvLabel class="body" text="{Text}"/>
          </DvStack>
          <DvRow gap="sm">
            <DvSpacer/>
            <DvButton text="{CancelText}" variant="secondary" onclick="PressCancel" visible="{HasCancel}"/>
            <DvButton text="{NoText}" variant="secondary" onclick="PressNo" visible="{HasNo}"/>
            <DvButton text="{PrimaryText}" variant="primary" onclick="PressPrimary"/>
          </DvRow>
        </DvDialogFrame>
        """;

    public DvMessageBoxDialog(string text, string? caption, DvMessageButtons buttons)
    {
        Text = text ?? "";
        Caption = caption;
        _buttons = buttons;
    }

    protected override string MarkupSource => Markup;

    public string? Caption { get; }

    public string Text { get; }

    public bool HasCancel => _buttons is DvMessageButtons.OkCancel or DvMessageButtons.YesNoCancel;

    public bool HasNo => _buttons is DvMessageButtons.YesNo or DvMessageButtons.YesNoCancel;

    // 예/아니요 구성이면 주 버튼이 '예', 아니면 '확인'
    private bool PrimaryIsYes => HasNo;

    public string CancelText => DialogTexts.Cancel(App);

    public string NoText => DialogTexts.No(App);

    public string PrimaryText => PrimaryIsYes ? DialogTexts.Yes(App) : DialogTexts.Ok(App);

    public void PressCancel() => Close(DvDialogResult.Cancel);

    public void PressNo() => Close(DvDialogResult.No);

    public void PressPrimary() => Close(PrimaryIsYes ? DvDialogResult.Yes : DvDialogResult.Ok);
}

/// <summary>Button labels of the built-in dialogs (message and input boxes): the <c>dv.dialog.*</c> keys of the app's <see cref="DvApp.Strings"/>, or the default text if absent.</summary>
internal static class DialogTexts
{
    public static string Ok(DvApp app) => Str(app, "dv.dialog.ok", "확인");

    public static string Cancel(DvApp app) => Str(app, "dv.dialog.cancel", "취소");

    public static string Yes(DvApp app) => Str(app, "dv.dialog.yes", "예");

    public static string No(DvApp app) => Str(app, "dv.dialog.no", "아니요");

    private static string Str(DvApp app, string key, string fallback) => DvStrings.Text(app.Strings, key, fallback);
}

/// <summary>Common base of the built-in input and selection dialogs that use [Cancel][OK] buttons: the button labels and cancellation (result <see langword="null"/>).</summary>
internal abstract class DvOkCancelDialog<TResult> : DvDialog<TResult?>
{
    public string CancelText => DialogTexts.Cancel(App);

    public string OkText => DialogTexts.Ok(App);

    public void PressCancel() => Close(default);
}
