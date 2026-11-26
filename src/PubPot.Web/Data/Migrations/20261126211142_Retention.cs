using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PubPot.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class Retention : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "last_activity_at",
                table: "signups",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "retention_token_hash",
                table: "signups",
                type: "bytea",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "retention_warning_sent_at",
                table: "signups",
                type: "timestamp with time zone",
                nullable: true);

            // Von Hand ergänzt: Bestehende bestätigte Anmeldungen bekommen als letzte Aktivität den späteren Zeitpunkt
            // von Bestätigung und letzter Admin-Änderung (GREATEST übergeht NULL). Unbestätigte bleiben NULL.
            migrationBuilder.Sql("""
                UPDATE signups
                SET last_activity_at = GREATEST(confirmed_at, admin_updated_at)
                WHERE confirmed_at IS NOT NULL;
                """);

            migrationBuilder.CreateIndex(
                name: "ix_signups_retention_token_hash",
                table: "signups",
                column: "retention_token_hash",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_signups_retention_token_hash",
                table: "signups");

            migrationBuilder.DropColumn(
                name: "last_activity_at",
                table: "signups");

            migrationBuilder.DropColumn(
                name: "retention_token_hash",
                table: "signups");

            migrationBuilder.DropColumn(
                name: "retention_warning_sent_at",
                table: "signups");
        }
    }
}
