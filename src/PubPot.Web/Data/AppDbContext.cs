using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace PubPot.Web.Data;

/// <summary>
/// Datenbank der Web-App (PostgreSQL, Datenbank "pubpot").
/// Tabellen und Spalten heißen snake_case (<c>signup_confirmations.token_hash</c>), wie in Postgres üblich.
/// Schema-Änderungen nur über EF-Migrationen: <c>dotnet ef migrations add &lt;Name&gt;</c> im Projektordner.
/// </summary>
public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options), IDataProtectionKeyContext
{
    public const int AdminNoteMaxLength = 4000;

    public DbSet<Signup> Signups => Set<Signup>();

    public DbSet<SignupConfirmation> SignupConfirmations => Set<SignupConfirmation>();

    public DbSet<OutboxEmail> OutboxEmails => Set<OutboxEmail>();

    /// <summary>
    /// Schlüssel von ASP.NET Data Protection (Antiforgery, Formular-Zeitstempel, später Admin-Login).
    /// In der Datenbank statt im Container, damit sie Neustarts und Deploys überleben.
    /// </summary>
    public DbSet<DataProtectionKey> DataProtectionKeys { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Signup>(e =>
        {
            e.ToTable("signups");
            e.HasDiscriminator(s => s.Kind)
                .HasValue<DeveloperSignup>(SignupKind.Developer)
                .HasValue<CreatorSignup>(SignupKind.Creator);
            e.Property(s => s.Kind).HasConversion<string>().HasMaxLength(20);

            e.Property(s => s.Email).HasMaxLength(254);
            e.Property(s => s.NormalizedEmail).HasMaxLength(254);
            e.Property(s => s.Name).HasMaxLength(100);
            e.Property(s => s.Language).HasMaxLength(5);
            e.Property(s => s.Message).HasMaxLength(2000);
            e.Property(s => s.ConsentVersion).HasMaxLength(20);
            e.Property(s => s.PipelineStatus).HasConversion<string>().HasMaxLength(20);
            e.Property(s => s.AdminNote).HasMaxLength(AdminNoteMaxLength);

            // Zusammenführen statt doppelt speichern: pro E-Mail und Rolle höchstens eine Anmeldung.
            e.HasIndex(s => new { s.Kind, s.NormalizedEmail }).IsUnique();
            // Für den Aufräum-Job: unbestätigt und letzte Einreichung älter als 30 Tage.
            e.HasIndex(s => new { s.ConfirmedAt, s.UpdatedAt });
            e.Ignore(s => s.IsConfirmed);
        });

        modelBuilder.Entity<DeveloperSignup>(e =>
        {
            e.Property(s => s.Studio).HasMaxLength(100);
            e.Property(s => s.GameTitle).HasMaxLength(150);
            e.Ignore(s => s.SteamUrl);
        });

        modelBuilder.Entity<CreatorSignup>(e =>
        {
            e.Property(s => s.Channel).HasMaxLength(200);
            e.Property(s => s.Genres).HasMaxLength(200);
            e.Property(s => s.AudienceSize).HasConversion<string>().HasMaxLength(20);
        });

        modelBuilder.Entity<SignupConfirmation>(e =>
        {
            e.ToTable("signup_confirmations");
            e.HasOne(c => c.Signup).WithMany().HasForeignKey(c => c.SignupId).OnDelete(DeleteBehavior.Cascade);
            e.Property(c => c.TokenHash).HasMaxLength(32);
            e.HasIndex(c => c.TokenHash).IsUnique();
            e.Property(c => c.PendingChanges).HasColumnType("jsonb");
        });

        modelBuilder.Entity<DataProtectionKey>(e => e.ToTable("data_protection_keys"));

        modelBuilder.Entity<OutboxEmail>(e =>
        {
            e.ToTable("outbox_emails");
            e.Property(m => m.To).HasMaxLength(254);
            e.Property(m => m.Subject).HasMaxLength(200);
            e.Property(m => m.LastError).HasMaxLength(1000);
            // Der Versanddienst sucht "noch nicht versendet und fällig".
            e.HasIndex(m => new { m.SentAt, m.NextAttemptAt });
        });
    }
}
