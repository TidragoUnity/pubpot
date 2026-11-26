using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PubPot.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "outbox_emails",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    to = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: false),
                    subject = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    text_body = table.Column<string>(type: "text", nullable: false),
                    html_body = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    next_attempt_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    attempts = table.Column<int>(type: "integer", nullable: false),
                    sent_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    last_error = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_outbox_emails", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "signups",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    kind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    email = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: false),
                    normalized_email = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    language = table.Column<string>(type: "character varying(5)", maxLength: 5, nullable: false),
                    message = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    consent_version = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    consent_given_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    confirmed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    platforms = table.Column<int>(type: "integer", nullable: true),
                    channel = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    audience_size = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    genres = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    confirmed_adult = table.Column<bool>(type: "boolean", nullable: true),
                    studio = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    game_title = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    steam_app_id = table.Column<long>(type: "bigint", nullable: true),
                    already_released = table.Column<bool>(type: "boolean", nullable: true),
                    planned_release = table.Column<DateOnly>(type: "date", nullable: true),
                    has_demo = table.Column<bool>(type: "boolean", nullable: true),
                    interested_in_next_fest_pilot = table.Column<bool>(type: "boolean", nullable: true),
                    has_publisher = table.Column<bool>(type: "boolean", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_signups", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "signup_confirmations",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    signup_id = table.Column<Guid>(type: "uuid", nullable: false),
                    token_hash = table.Column<byte[]>(type: "bytea", maxLength: 32, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    pending_changes = table.Column<string>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_signup_confirmations", x => x.id);
                    table.ForeignKey(
                        name: "fk_signup_confirmations_signups_signup_id",
                        column: x => x.signup_id,
                        principalTable: "signups",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_outbox_emails_sent_at_next_attempt_at",
                table: "outbox_emails",
                columns: new[] { "sent_at", "next_attempt_at" });

            migrationBuilder.CreateIndex(
                name: "ix_signup_confirmations_signup_id",
                table: "signup_confirmations",
                column: "signup_id");

            migrationBuilder.CreateIndex(
                name: "ix_signup_confirmations_token_hash",
                table: "signup_confirmations",
                column: "token_hash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_signups_confirmed_at_created_at",
                table: "signups",
                columns: new[] { "confirmed_at", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_signups_kind_normalized_email",
                table: "signups",
                columns: new[] { "kind", "normalized_email" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "outbox_emails");

            migrationBuilder.DropTable(
                name: "signup_confirmations");

            migrationBuilder.DropTable(
                name: "signups");
        }
    }
}
