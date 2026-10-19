using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using ControlStudio.Core.Actions;

namespace ControlStudio.Infrastructure;

/// <summary>Private app storage. A single versioned document makes edits and reordering atomic.</summary>
public sealed class SqliteActionRepository(string path, IActionValidator validator) : IActionRepository
{
    public const int MaximumActions = 200;
    private readonly SemaphoreSlim gate = new(1, 1);

    public Task<IReadOnlyList<ActionDefinition>> ReadAsync(CancellationToken cancellationToken = default) =>
        AccessAsync(null, cancellationToken);

    public async Task SaveAsync(ActionDefinition action, CancellationToken cancellationToken = default)
    {
        validator.Validate(action);
        await AccessAsync(actions =>
        {
            var index = actions.FindIndex(x => x.Id == action.Id);
            if (index >= 0) actions[index] = action with { SortOrder = actions[index].SortOrder };
            else
            {
                if (actions.Count >= MaximumActions) throw new ActionValidationException("Your dashboard can hold up to 200 actions.");
                actions.Add(action with { SortOrder = actions.Count });
            }
        }, cancellationToken);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default) =>
        await AccessAsync(actions => actions.RemoveAll(x => x.Id == id), cancellationToken);

    public async Task MoveAsync(Guid id, int offset, CancellationToken cancellationToken = default)
    {
        if (offset is not (-1 or 1)) throw new ArgumentOutOfRangeException(nameof(offset));
        await AccessAsync(actions =>
        {
            var index = actions.FindIndex(x => x.Id == id);
            if (index < 0) throw new ActionValidationException("This action no longer exists.");
            var target = index + offset;
            if (target >= 0 && target < actions.Count)
                (actions[index], actions[target]) = (actions[target], actions[index]);
        }, cancellationToken);
    }

    private async Task<IReadOnlyList<ActionDefinition>> AccessAsync(Action<List<ActionDefinition>>? change, CancellationToken token)
    {
        await gate.WaitAsync(token);
        try
        {
            return await Task.Run<IReadOnlyList<ActionDefinition>>(() =>
            {
                token.ThrowIfCancellationRequested();
                Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
                using var db = new Database(path);
                db.Execute("BEGIN IMMEDIATE");
                var committed = false;
                try
                {
                    var version = db.Read("PRAGMA user_version");
                    if (version != "0" && version != "1")
                        throw new InvalidDataException("This dashboard was created by a newer version. Update Control Studio to open it.");
                    if (version == "0")
                    {
                        db.Execute("CREATE TABLE IF NOT EXISTS dashboard (id INTEGER PRIMARY KEY CHECK(id = 1), payload TEXT NOT NULL)");
                        db.Execute("PRAGMA user_version = 1");
                    }
                    var json = db.Read("SELECT payload FROM dashboard WHERE id = 1");
                    List<ActionDefinition> actions = [];
                    if (json is not null)
                    {
                        if (json.Length > 16_000_000) throw InvalidDocument();
                        var document = JsonSerializer.Deserialize(json, ActionJsonContext.Default.DashboardDocument);
                        if (document is null || document.SchemaVersion != 1 || document.Actions is null || document.Actions.Count > MaximumActions)
                            throw InvalidDocument();
                        actions = document.Actions;
                        foreach (var action in actions) validator.Validate(action);
                        if (actions.Select(x => x.Id).Distinct().Count() != actions.Count ||
                            actions.Select(x => x.SortOrder).Distinct().Count() != actions.Count)
                            throw InvalidDocument();
                        actions = actions.OrderBy(x => x.SortOrder).ToList();
                    }
                    token.ThrowIfCancellationRequested();
                    if (change is not null)
                    {
                        change(actions);
                        for (var i = 0; i < actions.Count; i++) actions[i] = actions[i] with { SortOrder = i };
                        var payload = JsonSerializer.Serialize(new DashboardDocument(1, actions), ActionJsonContext.Default.DashboardDocument);
                        db.Write(payload);
                    }
                    token.ThrowIfCancellationRequested();
                    db.Execute("COMMIT");
                    committed = true;
                    return actions.AsReadOnly();
                }
                catch (JsonException) { throw InvalidDocument(); }
                catch (ActionValidationException) when (change is null) { throw InvalidDocument(); }
                finally { if (!committed) db.TryRollback(); }
            }, token);
        }
        finally { gate.Release(); }
    }

    private static InvalidDataException InvalidDocument() => new("The saved dashboard is invalid. Your data was preserved; restore a backup or use a compatible app version.");

    private sealed class Database : IDisposable
    {
        private IntPtr handle;
        public Database(string file)
        {
            var result = Sqlite.Open(Utf8(file), out handle, 0x00000002 | 0x00000004 | 0x00010000, IntPtr.Zero);
            if (result != 0) { Dispose(); Check(result); }
            Check(Sqlite.BusyTimeout(handle, 3000));
        }

        public void Execute(string sql)
        {
            var statement = Prepare(sql);
            try { Check(Sqlite.Step(statement), 101); }
            finally { Sqlite.Finalize(statement); }
        }

        public string? Read(string sql)
        {
            var statement = Prepare(sql);
            try
            {
                var result = Sqlite.Step(statement);
                if (result == 101) return null;
                Check(result, 100);
                return Marshal.PtrToStringUTF8(Sqlite.ColumnText(statement, 0)) ?? throw InvalidDocument();
            }
            finally { Sqlite.Finalize(statement); }
        }

        public void Write(string payload)
        {
            var statement = Prepare("INSERT INTO dashboard(id, payload) VALUES(1, ?1) ON CONFLICT(id) DO UPDATE SET payload = excluded.payload");
            try
            {
                var bytes = Utf8(payload);
                Check(Sqlite.BindText(statement, 1, bytes, bytes.Length - 1, new IntPtr(-1)));
                Check(Sqlite.Step(statement), 101);
            }
            finally { Sqlite.Finalize(statement); }
        }

        private IntPtr Prepare(string sql)
        {
            var bytes = Utf8(sql);
            var result = Sqlite.Prepare(handle, bytes, bytes.Length, out var statement, IntPtr.Zero);
            if (result != 0) { if (statement != IntPtr.Zero) Sqlite.Finalize(statement); Check(result); }
            return statement;
        }

        private static byte[] Utf8(string value) => Encoding.UTF8.GetBytes(value + "\0");
        private static void Check(int result, int expected = 0)
        {
            if (result != expected) throw new IOException(result is 5 or 6
                ? "The dashboard is busy. Wait a moment and try again."
                : "The dashboard could not be read or saved. Your existing data was preserved.");
        }
        public void TryRollback() { try { Execute("ROLLBACK"); } catch (IOException) { } }
        public void Dispose() { if (handle != IntPtr.Zero) { Sqlite.Close(handle); handle = IntPtr.Zero; } }
    }

    private static class Sqlite
    {
        // iOS links the system SQLite through the existing native bridge. Windows tests use the OS library.
        static Sqlite()
        {
            if (OperatingSystem.IsWindows() || OperatingSystem.IsMacOS())
                NativeLibrary.SetDllImportResolver(typeof(Sqlite).Assembly, (name, assembly, searchPath) =>
                    name == "__Internal" ? NativeLibrary.Load(OperatingSystem.IsWindows() ? "winsqlite3.dll" : "libsqlite3.dylib", assembly, searchPath) : IntPtr.Zero);
        }
        [DllImport("__Internal", EntryPoint = "sqlite3_open_v2", CallingConvention = CallingConvention.Cdecl)]
        internal static extern int Open(byte[] path, out IntPtr db, int flags, IntPtr vfs);
        [DllImport("__Internal", EntryPoint = "sqlite3_close", CallingConvention = CallingConvention.Cdecl)]
        internal static extern int Close(IntPtr db);
        [DllImport("__Internal", EntryPoint = "sqlite3_busy_timeout", CallingConvention = CallingConvention.Cdecl)]
        internal static extern int BusyTimeout(IntPtr db, int milliseconds);
        [DllImport("__Internal", EntryPoint = "sqlite3_prepare_v2", CallingConvention = CallingConvention.Cdecl)]
        internal static extern int Prepare(IntPtr db, byte[] sql, int length, out IntPtr statement, IntPtr tail);
        [DllImport("__Internal", EntryPoint = "sqlite3_step", CallingConvention = CallingConvention.Cdecl)]
        internal static extern int Step(IntPtr statement);
        [DllImport("__Internal", EntryPoint = "sqlite3_finalize", CallingConvention = CallingConvention.Cdecl)]
        internal static extern int Finalize(IntPtr statement);
        [DllImport("__Internal", EntryPoint = "sqlite3_column_text", CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr ColumnText(IntPtr statement, int index);
        [DllImport("__Internal", EntryPoint = "sqlite3_bind_text", CallingConvention = CallingConvention.Cdecl)]
        internal static extern int BindText(IntPtr statement, int index, byte[] value, int length, IntPtr destructor);
    }
}
