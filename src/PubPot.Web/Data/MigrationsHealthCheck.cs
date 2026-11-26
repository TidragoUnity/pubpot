using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace PubPot.Web.Data;

/// <summary>
/// Teil von /health/ready: "bereit" erst, wenn alle Migrationen in der Datenbank angewendet sind.
/// Läuft die App mit neuerem Code als das Datenbankschema, schlägt der Smoke-Test nach dem Deploy fehl.
/// </summary>
public sealed class MigrationsHealthCheck(AppDbContext db) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var pending = (await db.Database.GetPendingMigrationsAsync(cancellationToken)).ToList();
        return pending.Count == 0
            ? HealthCheckResult.Healthy()
            : HealthCheckResult.Unhealthy($"{pending.Count} Migration(en) nicht angewendet.");
    }
}
