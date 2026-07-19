using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Tempo.ReportServer.Api.Storage.Migrations
{
    /// <inheritdoc />
    public partial class ReportFavoritesAndRenderRuns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Favorites",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TenantId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    ReportId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Favorites", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RenderRuns",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TenantId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    ActorId = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    ReportId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    ParametersJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Format = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    Outcome = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    PageCount = table.Column<int>(type: "int", nullable: true),
                    ByteSize = table.Column<long>(type: "bigint", nullable: true),
                    DurationMs = table.Column<int>(type: "int", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RenderRuns", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Favorites_TenantId_UserId_ReportId",
                table: "Favorites",
                columns: new[] { "TenantId", "UserId", "ReportId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RenderRuns_TenantId_ActorId_CreatedAt",
                table: "RenderRuns",
                columns: new[] { "TenantId", "ActorId", "CreatedAt" },
                descending: new[] { false, false, true });

            migrationBuilder.CreateIndex(
                name: "IX_RenderRuns_TenantId_ReportId",
                table: "RenderRuns",
                columns: new[] { "TenantId", "ReportId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Favorites");

            migrationBuilder.DropTable(
                name: "RenderRuns");
        }
    }
}
