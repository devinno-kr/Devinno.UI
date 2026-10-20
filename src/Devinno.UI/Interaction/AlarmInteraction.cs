using System.Reflection;
using Devinno.UI.Alarms;
using Devinno.UI.Layout;
using SkiaSharp;

namespace Devinno.UI.Interaction;

/// <summary>
/// Handles alarm acknowledgment (ACK) by calling the view model's <c>onack</c> method when an alarm widget (<c>DvAlarmBanner</c>, <c>DvAlarmList</c>) is pressed.
/// For the banner, the target is the unacknowledged alarm currently shown as representative; for the list, it is the ACK button of the pressed row.
/// </summary>
/// <remarks>This is an internal engine stage. App code normally does not use it directly; it uses markup bindings and <see cref="DvSurface"/>/<see cref="DvRenderer"/>.</remarks>
public static class AlarmInteraction
{
    /// <summary>Checks whether a box is an alarm widget (<c>DvAlarmBanner</c> or <c>DvAlarmList</c>).</summary>
    /// <param name="box">The layout box to check.</param>
    /// <returns><see langword="true"/> if it is an alarm widget.</returns>
    public static bool IsAlarmWidget(LayoutBox box) => box.Node.Tag is "DvAlarmBanner" or "DvAlarmList";

    /// <summary>
    /// Handles a click on an alarm widget. If there is a target alarm, calls the view model's <c>onack</c> method, preferring <c>M(AlarmRow)</c> and otherwise <c>M(long id)</c>.
    /// </summary>
    /// <param name="box">The pressed alarm widget box.</param>
    /// <param name="p">The pressed point (in the same coordinate system as the box's <see cref="LayoutBox.Rect"/>).</param>
    /// <param name="vm">The screen view model.</param>
    /// <returns>
    /// <see langword="true"/> if a method was called. <see langword="false"/> if the box is disabled, there is no view model or <c>onack</c>, ACK display is off, there is no target alarm, or no matching method exists.
    /// </returns>
    /// <remarks>Exceptions thrown by the called method are propagated wrapped in <see cref="TargetInvocationException"/>.
    /// (Click handling in <see cref="DvSurface"/> catches this exception and reports it through <see cref="DvSurface.HandlerError"/>.)</remarks>
    public static bool Click(LayoutBox box, SKPoint p, object? vm)
    {
        if (box.Disabled || vm is null || string.IsNullOrEmpty(box.OnAck) || !box.AlarmShowAck)
            return false;

        var target = box.Node.Tag == "DvAlarmBanner"
            ? AlarmView.Top(box.AlarmItems)
            : AlarmListLayout.Compute(box.Rect, box.AlarmItems, box.AlarmRowHeight, box.AlarmShowAck).AckTargetAt(p);
        if (target is null)
            return false;

        var type = vm.GetType();
        const BindingFlags flags = BindingFlags.Public | BindingFlags.Instance;
        if (type.GetMethod(box.OnAck, flags, new[] { typeof(AlarmRow) }) is { } byRow)
            byRow.Invoke(vm, new object[] { target });
        else if (type.GetMethod(box.OnAck, flags, new[] { typeof(long) }) is { } byId)
            byId.Invoke(vm, new object[] { target.Id });
        else
            return false;

        return true;
    }
}
