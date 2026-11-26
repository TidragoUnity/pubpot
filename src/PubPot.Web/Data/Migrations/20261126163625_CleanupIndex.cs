using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PubPot.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class CleanupIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_signups_confirmed_at_created_at",
                table: "signups");

            migrationBuilder.CreateIndex(
                name: "ix_signups_confirmed_at_updated_at",
                table: "signups",
                columns: new[] { "confirmed_at", "updated_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_signups_confirmed_at_updated_at",
                table: "signups");

            migrationBuilder.CreateIndex(
                name: "ix_signups_confirmed_at_created_at",
                table: "signups",
                columns: new[] { "confirmed_at", "created_at" });
        }
    }
}
