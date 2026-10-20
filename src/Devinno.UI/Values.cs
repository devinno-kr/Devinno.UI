namespace Devinno.UI;

/// <summary>
/// String constants for behavior keywords used by only one widget family. They let C# binding properties use markup keywords without typos.
/// </summary>
/// <remarks>
/// <para>Nested class names are the widget short name + the attribute name in PascalCase. Short names: <c>Grid</c> = <c>DvDataGrid</c>, <c>DvColumn</c>, <c>DvSummaryRow</c>, <c>DvSummary</c>;
/// <c>DateTime</c> = <c>DvDateTimeInput</c> (e.g. <c>sort</c> of <c>DvColumn</c> → <see cref="GridSort"/>). Constant values equal the keywords written in markup.</para>
/// <para>Keywords shared by several widgets are in <see cref="Styles"/>.</para>
/// </remarks>
/// <example>
/// <code>
/// public class LogVm
/// {
///     public string Mode { get; set; } = Values.GridSelectionMode.Multi;   // &lt;DvDataGrid selection-mode="{Mode}"&gt;
///     public string PickMode { get; set; } = Values.DateTimeMode.Date;      // &lt;DvDateTimeInput mode="{PickMode}"/&gt;
/// }
/// </code>
/// </example>
public static class Values
{
    /// <summary><c>selection-mode</c> keywords of <c>DvDataGrid</c>: how rows are selected when pressed. Omitted or unknown values mean <see cref="None"/>.</summary>
    public static class GridSelectionMode
    {
        /// <summary><c>none</c>: rows are not selected (default). <c>onrowclick</c> is still called.</summary>
        public const string None = "none";
        /// <summary><c>single</c>: selects only one row.</summary>
        public const string Single = "single";
        /// <summary><c>multi</c>: selects multiple rows. Each press of a row toggles its selection.</summary>
        public const string Multi = "multi";
    }

    /// <summary>
    /// <c>cell-type</c> keywords of <c>DvColumn</c>: how a cell is drawn and handled. <see cref="Text"/> is the same as omitting it (text cell).
    /// <see cref="Check"/> and <see cref="Button"/> are cells that act immediately when pressed, <see cref="Lamp"/> is display only, and <see cref="Input"/>, <see cref="Select"/>, <see cref="DateTime"/> and <see cref="Color"/> are
    /// edit cells whose value is changed by pressing them. Changing a cell value writes once to the property of that row object.
    /// </summary>
    public static class GridCellType
    {
        /// <summary><c>text</c>: shows the value as text (<c>format</c> applied). This is the default.</summary>
        public const string Text = "text";
        /// <summary><c>check</c>: shows a <c>bool</c>/<c>bool?</c> value as a check box; pressing it inverts the value and writes it to the row object.</summary>
        public const string Check = "check";
        /// <summary><c>lamp</c>: shows the value as a lamp (display only). <c>bool</c> means on/off; strings and enums pick the color by <see cref="Styles.State"/> value.</summary>
        public const string Lamp = "lamp";
        /// <summary><c>button</c>: draws a button in the cell; pressing it calls the VM's <c>onclick</c> method with the row object as the argument (or with no argument if there is no such overload).</summary>
        public const string Button = "button";
        /// <summary><c>input</c>: edits text or numbers in place at the cell (string = text, numeric type = numeric keypad).</summary>
        public const string Input = "input";
        /// <summary><c>select</c>: pressing it opens a list popup below the cell to pick one item (<c>DvOption</c> children or <c>items</c>).</summary>
        public const string Select = "select";
        /// <summary><c>datetime</c>: pressing it opens a date/time picker popup (same rules as <c>DvDateTimeInput</c>).</summary>
        public const string DateTime = "datetime";
        /// <summary><c>color</c>: shows a color swatch and <c>#RRGGBB</c>; pressing it opens a color picker popup.</summary>
        public const string Color = "color";
    }

    /// <summary><c>DvColumn filter</c>: the column filter kind. Omitted = <see cref="None"/>.</summary>
    public static class GridFilter
    {
        /// <summary>No filter (default).</summary>
        public const string None = "none";
        /// <summary>Chosen automatically from the value type: string = text, number = range, date/time = date, bool/enum/select column = values.</summary>
        public const string Auto = "auto";
        /// <summary>Rows whose displayed cell text contains the entered text.</summary>
        public const string Text = "text";
        /// <summary>Rows with the values checked in the list.</summary>
        public const string Values = "values";
        /// <summary>Minimum–maximum numeric range (inclusive; either end may be omitted).</summary>
        public const string Range = "range";
        /// <summary>Start–end date/time range (inclusive; either end may be omitted).</summary>
        public const string Date = "date";
    }

    /// <summary><c>align</c> keywords of <c>DvColumn</c>: horizontal alignment of cell text. If omitted, numeric columns align right and others align left.</summary>
    public static class GridAlign
    {
        /// <summary><c>left</c>: left aligned.</summary>
        public const string Left = "left";
        /// <summary><c>center</c>: centered.</summary>
        public const string Center = "center";
        /// <summary><c>right</c>: right aligned.</summary>
        public const string Right = "right";
    }

    /// <summary><c>sort</c> keywords of <c>DvColumn</c>: the sort direction applied when the screen first opens. Sorting happens only inside the grid; the app's list order is not changed.</summary>
    public static class GridSort
    {
        /// <summary><c>asc</c>: ascending.</summary>
        public const string Asc = "asc";
        /// <summary><c>desc</c>: descending.</summary>
        public const string Desc = "desc";
    }

    /// <summary>
    /// <c>fn</c> keywords of <c>DvSummaryRow</c>/<c>DvSummary</c>: the aggregation method of the summary row. Non-numeric values and <see langword="null"/> are skipped.
    /// Set on <c>DvSummaryRow</c> it applies to all numeric columns; set on <c>DvSummary</c> it applies only to that column.
    /// </summary>
    public static class GridFn
    {
        /// <summary><c>sum</c>: sum.</summary>
        public const string Sum = "sum";
        /// <summary><c>avg</c>: average.</summary>
        public const string Avg = "avg";
        /// <summary><c>min</c>: minimum.</summary>
        public const string Min = "min";
        /// <summary><c>max</c>: maximum.</summary>
        public const string Max = "max";
        /// <summary><c>count</c>: number of non-<see langword="null"/> values.</summary>
        public const string Count = "count";
    }

    /// <summary>
    /// <c>mode</c> keywords of <c>DvDateTimeInput</c> (and <c>cell-type="datetime"</c> columns): which parts the picker selects.
    /// If omitted, it is determined by the bound type (<c>DateTime</c> = date + time, <c>DateOnly</c> = date, <c>TimeOnly</c> = time); a value that does not match the type logs one warning and the type's default is used.
    /// </summary>
    public static class DateTimeMode
    {
        /// <summary><c>date</c>: picks only the date from a calendar. For a <c>DateTime</c> value, the time part is kept.</summary>
        public const string Date = "date";
        /// <summary><c>time</c>: picks only hours and minutes (plus seconds with <c>show-seconds="true"</c>). For a <c>DateTime</c> value, the date part is kept.</summary>
        public const string Time = "time";
        /// <summary><c>datetime</c>: picks both date and time.</summary>
        public const string DateTime = "datetime";
    }
}
