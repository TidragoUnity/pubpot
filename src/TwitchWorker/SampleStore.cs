using Npgsql;
using NpgsqlTypes;

namespace TwitchWorker;

/// <summary>Zusammenfassung einer Abfrage-Runde (eine Zeile in "snapshots").</summary>
public sealed record SnapshotInfo(
    DateTimeOffset TakenAt,
    int Pages,
    TimeSpan Duration,
    bool Complete,
    string Filter);

/// <summary>Speichert die Abfragen in PostgreSQL. Die Tabellen legt das SQL-Skript an (E-036).</summary>
public sealed class SampleStore
{
    /// <summary>Tabellen, die das Skript deploy/db/twitch/001_schema.sql anlegt.</summary>
    internal static readonly string[] RequiredTables = ["snapshots", "stream_samples"];

    private readonly NpgsqlDataSource _db;

    public SampleStore(string connectionString)
    {
        var builder = new NpgsqlDataSourceBuilder(connectionString);

        // Kerberos/GSSAPI nutzen wir nicht. Ohne diese Zeile versucht Npgsql (ab Version 9), libgssapi zu laden,
        // und schreibt im schlanken Docker-Image eine harmlose, aber verwirrende Fehlermeldung ins Log.
        builder.ConnectionStringBuilder.GssEncryptionMode = GssEncryptionMode.Disable;

        _db = builder.Build();
    }

    /// <summary>
    /// Prüft, ob das Schema da ist. Angelegt wird es nicht mehr hier, sondern vorher vom Container
    /// "migrate-twitch" mit den SQL-Skripten aus deploy/db/twitch (E-011, E-036).
    /// </summary>
    public async Task<IReadOnlyList<string>> MissingTablesAsync(CancellationToken ct)
    {
        await using var conn = await _db.OpenConnectionAsync(ct);
        var missing = new List<string>();
        foreach (var table in RequiredTables)
        {
            await using var cmd = new NpgsqlCommand("SELECT to_regclass(@table) IS NOT NULL", conn);
            cmd.Parameters.AddWithValue("table", table);
            if (!(bool)(await cmd.ExecuteScalarAsync(ct))!)
            {
                missing.Add(table);
            }
        }

        return missing;
    }

    /// <summary>Zeitpunkt der allerersten Abfrage, damit "StopAfterHours" auch nach einem Neustart stimmt.</summary>
    public async Task<DateTimeOffset?> GetFirstSnapshotTimeAsync(CancellationToken ct)
    {
        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand("SELECT min(taken_at) FROM snapshots", conn);
        var value = await cmd.ExecuteScalarAsync(ct);
        return value is DateTime dt ? new DateTimeOffset(DateTime.SpecifyKind(dt, DateTimeKind.Utc)) : null;
    }

    /// <summary>
    /// Schreibt eine Abfrage-Runde in einer Transaktion: erst die Zusammenfassung,
    /// dann alle Streams per COPY (schnell, auch bei mehreren tausend Zeilen).
    /// </summary>
    public async Task SaveAsync(SnapshotInfo info, IReadOnlyCollection<TwitchStream> streams, CancellationToken ct)
    {
        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);

        long snapshotId;
        await using (var cmd = new NpgsqlCommand("""
            INSERT INTO snapshots (taken_at, stream_count, viewer_sum, pages, duration_ms, complete, filter)
            VALUES (@taken_at, @stream_count, @viewer_sum, @pages, @duration_ms, @complete, @filter)
            RETURNING id
            """, conn, tx))
        {
            cmd.Parameters.AddWithValue("taken_at", info.TakenAt.UtcDateTime);
            cmd.Parameters.AddWithValue("stream_count", streams.Count);
            cmd.Parameters.AddWithValue("viewer_sum", streams.Sum(s => (long)s.ViewerCount));
            cmd.Parameters.AddWithValue("pages", info.Pages);
            cmd.Parameters.AddWithValue("duration_ms", (int)info.Duration.TotalMilliseconds);
            cmd.Parameters.AddWithValue("complete", info.Complete);
            cmd.Parameters.AddWithValue("filter", info.Filter);
            snapshotId = (long)(await cmd.ExecuteScalarAsync(ct))!;
        }

        await using (var importer = await conn.BeginBinaryImportAsync("""
            COPY stream_samples (snapshot_id, taken_at, stream_id, user_id, user_login,
                                 game_id, game_name, language, viewer_count, started_at)
            FROM STDIN (FORMAT BINARY)
            """, ct))
        {
            foreach (var s in streams)
            {
                await importer.StartRowAsync(ct);
                await importer.WriteAsync(snapshotId, NpgsqlDbType.Bigint, ct);
                await importer.WriteAsync(info.TakenAt.UtcDateTime, NpgsqlDbType.TimestampTz, ct);
                await importer.WriteAsync(s.Id, NpgsqlDbType.Text, ct);
                await importer.WriteAsync(s.UserId, NpgsqlDbType.Text, ct);
                await importer.WriteAsync(s.UserLogin, NpgsqlDbType.Text, ct);
                await importer.WriteAsync(s.GameId, NpgsqlDbType.Text, ct);
                await importer.WriteAsync(s.GameName, NpgsqlDbType.Text, ct);
                await importer.WriteAsync(s.Language, NpgsqlDbType.Text, ct);
                await importer.WriteAsync(s.ViewerCount, NpgsqlDbType.Integer, ct);
                await importer.WriteAsync(s.StartedAt.UtcDateTime, NpgsqlDbType.TimestampTz, ct);
            }

            await importer.CompleteAsync(ct);
        }

        await tx.CommitAsync(ct);
    }
}
