using System.Diagnostics;

namespace Devinno.UI;

// 앱(VM) 코드 예외 — 입력 처리·호스트 루프 밖으로 새지 않게 잡고, Trace 오류 + HandlerError 로 알린다(Interaction.HandlerErrors).
public sealed partial class DvSurface
{
    /// <summary>
    /// Raised when app (view model) code called by the library throws an exception. The exception has already been caught and handled; it does not escape pointer/key handling, layout, painting or the host loop.
    /// </summary>
    /// <remarks>
    /// <para><b>Covered code</b>: <c>onclick</c>, <c>onrowclick</c>, <c>onack</c> and <c>onbeforecommit</c> handlers; setters of view model properties that widgets write
    /// (<c>DvToggle</c>, <c>DvKnob</c>, <c>DvInput</c>, <c>DvSelect</c>, pickers, grid cells, grid selection);
    /// and value-reading code during <see cref="Layout(float, float)"/>/<see cref="RenderTo(SkiaSharp.SKCanvas, float, float, long)"/> (property getters, <c>ToString</c> of values, list enumeration, grid sort comparisons).
    /// What was being done when it failed is given by <see cref="DvHandlerErrorEventArgs.Kind"/>.</para>
    /// <para><b>Outcome</b>: a failed value write is treated like an <c>onbeforecommit</c> rejection. <c>DvInput</c> (including grid input cells) stays in edit mode and shows the
    /// <c>dv.input.write-failed</c> text ("값을 저장하지 못했습니다", "the value could not be saved") on the error line; the exception message is not shown on screen. A picker stays open with its popup,
    /// toggles, knobs and check cells keep their value, and grid selection keeps the previous selection. A failed value read treats the value as empty and keeps painting,
    /// and the same target (<see cref="DvHandlerErrorEventArgs.Kind"/>/<see cref="DvHandlerErrorEventArgs.Member"/>/<see cref="DvHandlerErrorEventArgs.Tag"/>)
    /// is reported only once until it is rebuilt by <see cref="Invalidate"/> (so reading every frame, as a grid does, does not flood).</para>
    /// <para><b>Always traced</b>: even with no subscribers, it is logged once with <see cref="Trace.TraceError(string)"/>. Exceptions thrown by a subscriber itself are swallowed and only traced
    /// (the remaining subscribers are still called).</para>
    /// <para><b>DvApp</b>: an exception raised in an area (bar) or page surface of <see cref="App.DvApp"/> is raised on that surface's event and then also on the root surface's (<see cref="App.DvApp.Surface"/>) event
    /// with the same arguments. <c>sender</c> is the surface that owns the event. When using <see cref="App.DvApp"/>, subscribe only to <see cref="App.DvApp.HandlerError"/> (raised only once).</para>
    /// <para>Raised during input handling, layout or painting (UI thread). Exceptions from methods that app code calls directly propagate to that call as is and are not raised through this event.</para>
    /// </remarks>
    /// <example>
    /// <code>
    /// var surface = new DvSurface(markup, vm);
    /// surface.HandlerError += (_, e) =&gt;
    ///     Log.Error(e.Exception, $"화면 처리기 실패: {e.Kind} {e.Member} ({e.Tag})");
    /// // if the VM's public void Start() throws → Kind = "onclick", Member = "Start", Tag = "DvButton"
    /// </code>
    /// </example>
    public event EventHandler<DvHandlerErrorEventArgs>? HandlerError;

    // 이 화면과 위쪽(루트) 화면의 구독자에게 차례로 알린다. 구독자 예외는 삼킨다(Trace).
    internal void RaiseHandlerError(DvHandlerErrorEventArgs e)
    {
        for (var s = this; s is not null; s = s.ParentSurface)
        {
            if (s.HandlerError is not { } handlers)
                continue;
            foreach (var d in handlers.GetInvocationList())
            {
                try
                {
                    ((EventHandler<DvHandlerErrorEventArgs>)d)(s, e);
                }
                catch (Exception ex)
                {
                    Trace.TraceError($"Devinno.UI: HandlerError 구독자 예외(무시함): {ex.GetType().Name}: {ex.Message}");
                }
            }
        }
    }
}
