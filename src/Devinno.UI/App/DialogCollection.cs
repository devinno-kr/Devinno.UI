using System.Collections;

namespace Devinno.UI.App;

/// <summary>
/// The list of dialogs open in the app (<see cref="DvApp"/>) (<see cref="DvApp.Dialogs"/>). Ordered bottom to top (0 = bottom, last = top).
/// </summary>
/// <remarks>
/// <para>A dialog registered with <c>ShowDialog</c> appears in the list immediately, even before its screen is built (before the next frame), and is removed as soon as closing is requested (the same rule as <see cref="DvDialog.IsOpen"/>).</para>
/// <para>Readable from any thread. Because other threads may show or close dialogs at the same time, prefer enumeration (a snapshot taken at call time) or <see cref="Top"/>
/// over reading <see cref="Count"/> and the indexer separately.</para>
/// </remarks>
public sealed class DialogCollection : IReadOnlyList<DvDialog>
{
    private readonly DvApp _app;

    internal DialogCollection(DvApp app) => _app = app;

    /// <summary>Gets the number of open dialogs.</summary>
    public int Count => _app.DialogCount;

    /// <summary>Gets the dialog at position <paramref name="index"/> counted from the bottom (0 = bottom).</summary>
    /// <param name="index">Zero-based position (bottom to top).</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is out of range.</exception>
    public DvDialog this[int index] => _app.DialogAt(index);

    /// <summary>Gets the top dialog, or <see langword="null"/> if no dialog is open.</summary>
    public DvDialog? Top => _app.TopDialog;

    /// <summary>Enumerates the dialogs from bottom to top (a snapshot taken at call time).</summary>
    public IEnumerator<DvDialog> GetEnumerator() => ((IEnumerable<DvDialog>)_app.DialogSnapshot()).GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
