using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PubPot.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class AdminPipeline : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "admin_note",
                table: "signups",
                type: "character varying(4000)",
                maxLength: 4000,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "admin_updated_at",
                table: "signups",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "next_step_on",
                table: "signups",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "pipeline_status",
                table: "signups",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                // Von Hand angepasst (EF schlägt "" vor): Bestehende Anmeldungen starten in der Pipeline als "neu".
                defaultValue: "New");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "admin_note",
                table: "signups");

            migrationBuilder.DropColumn(
                name: "admin_updated_at",
                table: "signups");

            migrationBuilder.DropColumn(
                name: "next_step_on",
                table: "signups");

            migrationBuilder.DropColumn(
                name: "pipeline_status",
                table: "signups");
        }
    }
}
