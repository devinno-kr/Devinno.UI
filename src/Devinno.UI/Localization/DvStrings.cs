using System.Diagnostics;
using System.Text.Json;

namespace Devinno.UI.Localization;

/// <summary>
/// A multilingual string table for on-screen text. Register key → string entries per language, and reference them in markup with <c>{L10n.key}</c> or <c>@key</c>.
/// </summary>
/// <remarks>
/// <para>Lookup order is the current language (<see cref="Language"/>) → <see cref="FallbackLanguage"/>. If neither has the key, <see cref="Get"/> returns <c>[key]</c> and
/// writes a Trace warning once per language/key combination. Language names are case-insensitive; keys are case-sensitive.</para>
/// <para>When attached to a screen (<see cref="DvSurface.Strings"/>, or the host's <c>Strings</c>), changing <see cref="Language"/> or adding entries to the current language
/// raises <see cref="Changed"/>, and the screen is laid out again automatically.</para>
/// <para>Built-in text such as input validation messages and picker buttons can also be overridden with keys starting with <c>dv.</c> (for example <c>dv.input.rejected</c>, <c>dv.picker.ok</c>, <c>dv.kbd.done</c>).
/// If absent, the default Korean text built into the code is used.</para>
/// <para>Not thread-safe. Register tables and switch languages on the UI thread. <see cref="Changed"/> is raised synchronously on the thread that changed the value.</para>
/// </remarks>
/// <example>
/// <code>
/// var strings = new DvStrings { Language = "ko" };
/// strings.LoadJson("ko", "{\"pump\":\"펌프\",\"alarm\":{\"none\":\"알람 없음\"}}");
/// strings.LoadJson("en", "{\"pump\":\"Pump\",\"alarm\":{\"none\":\"No alarms\"}}");
/// surface.Strings = strings;   // markup: &lt;DvLabel text="@pump"/&gt;, &lt;DvLabel text="{L10n.alarm.none}"/&gt;
/// strings.Language = "en";     // the attached screen is redrawn in English
/// </code>
/// </example>
public sealed class DvStrings
{
    private readonly Dictionary<string, Dictionary<string, string>> _tables = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _warned = new(StringComparer.Ordinal);
    private string _language = "ko";

    /// <summary>
    /// Gets or sets the current language name (for example <c>ko</c>, <c>en</c>). The default is <c>ko</c>. Setting a value that differs only in case is not treated as a change.
    /// </summary>
    /// <remarks>Raises <see cref="Changed"/> when the value actually changes. Switching to an unregistered language does not throw; lookups fall through to <see cref="FallbackLanguage"/>.</remarks>
    public string Language
    {
        get => _language;
        set
        {
            if (string.Equals(_language, value, StringComparison.OrdinalIgnoreCase))
                return;
            _language = value;
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>Gets or sets the language searched next when the current language lacks a key. The default is <c>en</c>; if <see langword="null"/>, no fallback lookup is done. Changing it does not raise <see cref="Changed"/>.</summary>
    public string? FallbackLanguage { get; set; } = "en";

    /// <summary>Gets the names of languages that have at least one registered entry (languages registered with <see cref="Add"/>/<see cref="LoadJson"/>). A read-only view of the internal table.</summary>
    public IReadOnlyCollection<string> Languages => _tables.Keys;

    /// <summary>
    /// Raised when a change occurs that requires the screen to be redrawn: when <see cref="Language"/> changes, and when entries are added to the current language with <see cref="Add"/>/<see cref="LoadJson"/>.
    /// </summary>
    /// <remarks>When attached to <see cref="DvSurface.Strings"/>, the screen subscribes to this event and lays out again automatically. Raised synchronously on the thread that made the change.</remarks>
    public event EventHandler? Changed;

    /// <summary>
    /// Adds key → string entries to a language. Existing keys are overwritten with the new value. Entries for the same language may be added in several calls.
    /// </summary>
    /// <remarks>Raises <see cref="Changed"/> if <paramref name="language"/> is the current language.</remarks>
    /// <param name="language">Language name (case-insensitive).</param>
    /// <param name="entries">Entries to add. Keys are case-sensitive and may contain dots (<c>.</c>) to form a hierarchy (for example <c>alarm.none</c>).</param>
    public void Add(string language, IEnumerable<KeyValuePair<string, string>> entries)
    {
        if (!_tables.TryGetValue(language, out var table))
            _tables[language] = table = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (k, v) in entries)
            table[k] = v;
        if (string.Equals(language, _language, StringComparison.OrdinalIgnoreCase))
            Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Reads a JSON object string and adds its contents as entries to a language (same rules as <see cref="Add"/>). Nested objects are flattened into dot-path keys
    /// (<c>{"alarm":{"none":"…"}}</c> → key <c>alarm.none</c>).
    /// </summary>
    /// <remarks>
    /// String values are stored as is; numbers, <c>true</c>/<c>false</c> and arrays are stored as their raw JSON text; <c>null</c> values are skipped.
    /// </remarks>
    /// <param name="language">Language name (case-insensitive).</param>
    /// <param name="json">JSON text whose root is an object (such as file contents).</param>
    /// <exception cref="JsonException"><paramref name="json"/> is not valid JSON.</exception>
    public void LoadJson(string language, string json)
    {
        var entries = new Dictionary<string, string>(StringComparer.Ordinal);
        using var doc = JsonDocument.Parse(json);
        Flatten(doc.RootElement, "", entries);
        Add(language, entries);
    }

    /// <summary>Looks up the string for a key in the current language, then <see cref="FallbackLanguage"/>. Writes no warning.</summary>
    /// <param name="key">The key to look up (case-sensitive).</param>
    /// <param name="value">The string found, or an empty string if not found.</param>
    /// <returns><see langword="true"/> if found.</returns>
    public bool TryGet(string key, out string value)
    {
        if (Lookup(_language, key, out value))
            return true;
        return FallbackLanguage is { } fb && Lookup(fb, key, out value);
    }

    /// <summary>
    /// Returns the string for a key. Markup <c>{L10n.key}</c> and <c>@key</c> are resolved through this method.
    /// </summary>
    /// <remarks>
    /// If the key is in neither the current language nor <see cref="FallbackLanguage"/>, returns <c>[key]</c> so the missing translation is visible on screen right away,
    /// and writes a <see cref="Trace"/> warning once per language/key combination.
    /// </remarks>
    /// <param name="key">The key to look up (case-sensitive).</param>
    /// <returns>The string found, or <c>[key]</c> if none.</returns>
    public string Get(string key)
    {
        if (TryGet(key, out var v))
            return v;

        lock (_warned)
        {
            if (_warned.Add(_language + "|" + key))
                Trace.TraceWarning($"Devinno.UI: 다국어 키 '{key}' 없음 (language={_language})");
        }

        return "[" + key + "]";
    }

    /// <summary>
    /// Returns the string for the key if the table has it; otherwise returns <paramref name="fallback"/>. Writes no warning.
    /// Used for text whose translation is optional, such as the library's built-in text (<c>dv.</c> keys).
    /// </summary>
    /// <param name="strings">The multilingual table. If <see langword="null"/>, <paramref name="fallback"/> is always returned.</param>
    /// <param name="key">The key to look up (case-sensitive).</param>
    /// <param name="fallback">The default text to use when the key is absent.</param>
    /// <returns>The string found, or <paramref name="fallback"/>.</returns>
    public static string Text(DvStrings? strings, string key, string fallback)
        => strings is not null && strings.TryGet(key, out var v) ? v : fallback;

    private bool Lookup(string language, string key, out string value)
    {
        value = "";
        return _tables.TryGetValue(language, out var t) && t.TryGetValue(key, out value!);
    }

    private static void Flatten(JsonElement e, string prefix, Dictionary<string, string> into)
    {
        switch (e.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var p in e.EnumerateObject())
                    Flatten(p.Value, prefix.Length == 0 ? p.Name : prefix + "." + p.Name, into);
                break;
            case JsonValueKind.String:
                into[prefix] = e.GetString() ?? "";
                break;
            case JsonValueKind.Null:
            case JsonValueKind.Undefined:
                break;
            default:
                into[prefix] = e.GetRawText();
                break;
        }
    }
}
