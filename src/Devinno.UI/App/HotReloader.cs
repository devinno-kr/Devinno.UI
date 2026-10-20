using System.Diagnostics;

namespace Devinno.UI.App;

/// <summary>
/// <see cref="DvApp.HotReload"/> — watches the folders containing screen dvml files, and on change collects changes briefly (while the editor writes several times)
/// and then has the screens that use that file re-read it on the app's input thread. Watching runs on the file system thread; re-reading goes through <see cref="DvApp.EnqueuePosted"/>.
/// </summary>
internal sealed class HotReloader : IDisposable
{
    internal const int DebounceMs = 150;

    private readonly DvApp _app;
    private readonly object _gate = new();
    private readonly Dictionary<string, FileSystemWatcher> _watchers = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _files = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _changed = new(StringComparer.OrdinalIgnoreCase);
    private readonly Timer _debounce;
    private bool _disposed;

    public HotReloader(DvApp app)
    {
        _app = app;
        _debounce = new Timer(_ => Flush());
    }

    public static string Normalize(string path)
    {
        try { return Path.GetFullPath(path); }
        catch (Exception) { return path; }
    }

    /// <summary>Adds this file to the watch list (creates a watcher for its folder if there is none). Ignored if the folder does not exist.</summary>
    public void Watch(string file)
    {
        var full = Normalize(file);
        var dir = Path.GetDirectoryName(full);
        if (dir is null || !Directory.Exists(dir))
            return;
        lock (_gate)
        {
            if (_disposed || !_files.Add(full) || _watchers.ContainsKey(dir))
                return;
            try
            {
                var w = new FileSystemWatcher(dir, "*.dvml")
                {
                    NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.Size,
                    IncludeSubdirectories = false,
                };
                w.Changed += OnFileEvent;
                w.Created += OnFileEvent;
                w.Renamed += (_, e) => Mark(e.FullPath); // 편집기가 임시 파일을 이름 바꿔 덮어쓰는 경우
                w.EnableRaisingEvents = true;
                _watchers[dir] = w;
            }
            catch (Exception ex) when (ex is IOException or ArgumentException or PlatformNotSupportedException or UnauthorizedAccessException)
            {
                Trace.TraceWarning($"Devinno.UI: 핫리로드 — '{dir}' 를 감시하지 못했습니다: {ex.Message}");
            }
        }
    }

    private void OnFileEvent(object sender, FileSystemEventArgs e) => Mark(e.FullPath);

    private void Mark(string path)
    {
        var full = Normalize(path);
        lock (_gate)
        {
            if (_disposed || !_files.Contains(full))
                return;
            _changed.Add(full);
            _debounce.Change(DebounceMs, Timeout.Infinite);
        }
    }

    private void Flush()
    {
        string[] paths;
        lock (_gate)
        {
            if (_disposed || _changed.Count == 0)
                return;
            paths = _changed.ToArray();
            _changed.Clear();
        }
        _app.EnqueuePosted(_ => _app.ReloadFiles(paths), null);
        _app.Surface.RequestFrame();
    }

    /// <summary>The file's source text — if the editor is still writing it (locked), waits briefly and retries. Returns null if it still cannot be read.</summary>
    public static string? Read(string path)
    {
        for (var i = 0; ; i++)
        {
            try
            {
                return File.ReadAllText(path);
            }
            catch (IOException) when (i < 5)
            {
                Thread.Sleep(30);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                Trace.TraceWarning($"Devinno.UI: 핫리로드 — '{path}' 를 읽지 못했습니다: {ex.Message}");
                return null;
            }
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed)
                return;
            _disposed = true;
            foreach (var w in _watchers.Values)
                w.Dispose();
            _watchers.Clear();
        }
        _debounce.Dispose();
    }
}
