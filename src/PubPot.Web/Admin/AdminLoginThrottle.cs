using Microsoft.Extensions.Options;

namespace PubPot.Web.Admin;

/// <summary>
/// Sperre nach zu vielen falschen Logins: Nach <see cref="AdminOptions.MaxFailedLogins"/> Fehlversuchen in Folge
/// ist der Login für <see cref="AdminOptions.LockoutDuration"/> gesperrt, auch mit dem richtigen Passwort.
/// Ein erfolgreicher Login setzt den Zähler zurück.
/// </summary>
/// <remarks>
/// Die Sperre gilt für das Konto, nicht pro IP. Ein Angreifer mit vielen IP-Adressen kann so nicht einfach
/// weiterprobieren. Der Preis: Er kann den Login zeitweise sperren (E-026). Gezählt wird im Arbeitsspeicher,
/// nach einem Neustart beginnt die Zählung neu. Für eine einzelne App-Instanz reicht das.
/// </remarks>
public sealed class AdminLoginThrottle(IOptions<AdminOptions> options, TimeProvider time)
{
    private readonly Lock _lock = new();
    private int _failures;
    private DateTimeOffset? _lockedUntil;

    /// <summary>Restzeit der Sperre, oder <c>null</c>, wenn der Login offen ist.</summary>
    public TimeSpan? RemainingLockout()
    {
        lock (_lock)
        {
            var now = time.GetUtcNow();
            if (_lockedUntil is { } until && until > now)
            {
                return until - now;
            }

            if (_lockedUntil is not null)
            {
                // Sperre abgelaufen: wieder volle Versuche.
                _lockedUntil = null;
                _failures = 0;
            }

            return null;
        }
    }

    /// <summary>Zählt einen Fehlversuch. Gibt die Sperrdauer zurück, wenn der Login damit gesperrt wird.</summary>
    public TimeSpan? RegisterFailure()
    {
        lock (_lock)
        {
            _failures++;
            if (_failures < options.Value.MaxFailedLogins)
            {
                return null;
            }

            _lockedUntil = time.GetUtcNow() + options.Value.LockoutDuration;
            return options.Value.LockoutDuration;
        }
    }

    public int Failures
    {
        get
        {
            lock (_lock)
            {
                return _failures;
            }
        }
    }

    public void Reset()
    {
        lock (_lock)
        {
            _failures = 0;
            _lockedUntil = null;
        }
    }
}
