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

/// <summary>Speichert die Abfragen in PostgreSQL und legt die Tabellen beim Start an.</summary>
public sealed class SampleStore
{
    private const string Schema = """
        CREATE TABLE IF NOT EXISTS snapshots (
            id           bigserial PRIMARY KEY,
            taken_at     timestamptz NOT NULL,
            stream_count int         NOT NULL,
            viewer_sum   bigint      NOT NULL,
            pages        int         NOT NULL,
            duration_ms  int         NOT NULL,
            complete     boolean     NOT NULL,
            filter       text        NOT NULL
        );

        CREATE TABLE IF NOT EXISTS stream_samples (
            snapshot_id  bigint      NOT NULL REFERENCES snapshots(id) ON DELETE CASCADE,
            taken_at     timestamptz NOT NULL,
            stream_id    text        NOT NULL,
            user_id      text        NOT NULL,
            user_login   text        NOT NULL,
            game_id      text        NOT NULL,
            game_name    text        NOT NULL,
            language     text        NOT NULL,
            viewer_count int         NOT NULL,
            started_at   timestamptz NOT NULL,
            PRIMARY KEY (snapshot_id, stream_id)
        );

        CREATE INDEX IF NOT EXISTS ix_samples_stream ON stream_samples (stream_id);
        CREATE INDEX IF NOT EXISTS ix_samples_user   ON stream_samples (user_id, taken_at);
        CREATE INDEX IF NOT EXISTS ix_snapshots_time ON snapshots (taken_at);
        """;

    private readonly NpgsqlDataSource _db;

    public SampleStore(string connectionString)
    {
        _db = NpgsqlDataSource.Create(connectionString);
    }

    public async Task EnsureSchemaAsync(CancellationToken ct)
    {
        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(Schema, conn);
        await cmd.ExecuteNonQueryAsync(ct);
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
