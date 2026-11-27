using Npgsql;
using Testcontainers.PostgreSql;

namespace TwitchWorker.Tests;

/// <summary>
/// Schema des Workers aus deploy/db/twitch (E-036): Der Runner migrate.sh läuft im echten Postgres-Image,
/// genau wie später der Compose-Dienst "migrate-twitch". Jeder Test bekommt eine eigene, leere Datenbank.
/// </summary>
public sealed class SchemaTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:16.16")
        .WithUsername("twitch").WithDatabase("twitch").WithPassword("twitch").Build();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        await _postgres.StartAsync(Ct);
        // Wie im Compose-Dienst: Skripte im Container unter /twitch.
        await _postgres.CopyAsync(new DirectoryInfo(MigrationsDir), "/twitch", ct: Ct);
    }

    public async ValueTask DisposeAsync() => await _postgres.DisposeAsync();

    /// <summary>Ordner deploy/db/twitch im Repo.</summary>
    private static string MigrationsDir
    {
        get
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "PubPot.slnx")))
            {
                dir = dir.Parent;
            }

            var root = dir?.FullName ?? throw new InvalidOperationException("PubPot.slnx nicht gefunden.");
            return Path.Combine(root, "deploy", "db", "twitch");
        }
    }

    private async Task<string> NewDatabaseAsync()
    {
        var name = $"t_{Guid.NewGuid():N}";
        await using var conn = new NpgsqlConnection(_postgres.GetConnectionString());
        await conn.OpenAsync(Ct);
        await using var cmd = new NpgsqlCommand($"CREATE DATABASE {name}", conn);
        await cmd.ExecuteNonQueryAsync(Ct);
        return name;
    }

    private string ConnectionString(string database) =>
        new NpgsqlConnectionStringBuilder(_postgres.GetConnectionString()) { Database = database }.ConnectionString;

    /// <summary>Führt migrate.sh im Container aus, wie der Compose-Dienst (Verbindung über die PG*-Variablen).</summary>
    private async Task<(long ExitCode, string Output)> MigrateAsync(string database, string dir = "/twitch")
    {
        var result = await _postgres.ExecAsync(
            ["sh", "-c", $"PGUSER=twitch PGDATABASE={database} MIGRATIONS_DIR={dir} sh /twitch/migrate.sh"], Ct);
        return (result.ExitCode ?? -1, result.Stdout + result.Stderr);
    }

    private Task<DotNet.Testcontainers.Containers.ExecResult> PsqlAsync(string database, params string[] args) =>
        _postgres.ExecAsync(["psql", "-U", "twitch", "-d", database, "-v", "ON_ERROR_STOP=1", .. args], Ct);

    private async Task<T> ScalarAsync<T>(string database, string sql)
    {
        await using var conn = new NpgsqlConnection(ConnectionString(database));
        await conn.OpenAsync(Ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        return (T)(await cmd.ExecuteScalarAsync(Ct))!;
    }

    [Fact]
    public async Task Worker_reports_missing_tables_before_migration()
    {
        var database = await NewDatabaseAsync();
        var store = new SampleStore(ConnectionString(database));

        Assert.Equal(["snapshots", "stream_samples"], await store.MissingTablesAsync(Ct));
    }

    [Fact]
    public async Task Migration_creates_schema_the_worker_can_write_to()
    {
        var database = await NewDatabaseAsync();

        var (exitCode, output) = await MigrateAsync(database);

        Assert.Equal(0, exitCode);
        Assert.Contains("1 neu eingespielt", output, StringComparison.Ordinal);

        var store = new SampleStore(ConnectionString(database));
        Assert.Empty(await store.MissingTablesAsync(Ct));

        var takenAt = new DateTimeOffset(2026, 11, 27, 10, 0, 0, TimeSpan.Zero);
        await store.SaveAsync(
            new SnapshotInfo(takenAt, Pages: 1, TimeSpan.FromSeconds(2), Complete: true, Filter: "language=de"),
            [new TwitchStream("s1", "u1", "kanal", "g1", "Spiel", "de", 12, takenAt.AddHours(-1))],
            Ct);

        Assert.Equal(takenAt, await store.GetFirstSnapshotTimeAsync(Ct));
        Assert.Equal(1L, await ScalarAsync<long>(database, "SELECT count(*) FROM stream_samples"));
    }

    [Fact]
    public async Task Second_run_changes_nothing()
    {
        var database = await NewDatabaseAsync();
        await MigrateAsync(database);

        var (exitCode, output) = await MigrateAsync(database);

        Assert.Equal(0, exitCode);
        Assert.Contains("0 neu eingespielt", output, StringComparison.Ordinal);
        Assert.DoesNotContain("NOTICE", output, StringComparison.Ordinal);
        Assert.Equal(1L, await ScalarAsync<long>(database, "SELECT count(*) FROM schema_migrations"));
    }

    /// <summary>Datenbanken, in denen der alte Worker die Tabellen selbst angelegt hat, laufen ohne Fehler durch.</summary>
    [Fact]
    public async Task Existing_tables_from_old_worker_are_kept()
    {
        var database = await NewDatabaseAsync();
        await PsqlAsync(database, "-f", "/twitch/001_schema.sql");
        await PsqlAsync(database, "-c",
            "INSERT INTO snapshots (taken_at, stream_count, viewer_sum, pages, duration_ms, complete, filter) VALUES (now(), 0, 0, 0, 0, true, 'alt')");

        var (exitCode, _) = await MigrateAsync(database);

        Assert.Equal(0, exitCode);
        Assert.Equal(1L, await ScalarAsync<long>(database, "SELECT count(*) FROM snapshots"));
    }

    /// <summary>Ein fehlerhaftes Skript wird ganz zurückgerollt und nicht als eingespielt vermerkt.</summary>
    [Fact]
    public async Task Broken_script_fails_and_rolls_back()
    {
        var database = await NewDatabaseAsync();
        await _postgres.ExecAsync(
            ["sh", "-c", "mkdir -p /broken && cp /twitch/*.sql /broken/ && printf 'CREATE TABLE half_done (id int);\\nSELEKT kaputt;\\n' > /broken/002_broken.sql"],
            Ct);

        var (exitCode, output) = await MigrateAsync(database, "/broken");

        Assert.NotEqual(0, exitCode);
        Assert.Contains("SELEKT", output, StringComparison.Ordinal);
        Assert.Equal("001_schema", await ScalarAsync<string>(database, "SELECT string_agg(version, ',') FROM schema_migrations"));
        Assert.False(await ScalarAsync<bool>(database, "SELECT to_regclass('half_done') IS NOT NULL"));
    }
}
