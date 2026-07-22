using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Tempo.ReportServer.Api.Storage.Migrations
{
    /// <inheritdoc />
    public partial class RenderJobs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RenderJobs",
                columns: table => new
                {
                    JobId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    TenantId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    ReportId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Format = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    RequestJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    QueuedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    QueuedSequence = table.Column<long>(type: "bigint", nullable: false),
                    TenantSequence = table.Column<long>(type: "bigint", nullable: false),
                    StartedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CompletedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ErrorMessage = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: true),
                    DownloadUrl = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    SnapshotUrl = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    LeaseOwner = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    LeasedUntilTicks = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RenderJobs", x => x.JobId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RenderJobs_JobId",
                table: "RenderJobs",
                column: "JobId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RenderJobs_Status_TenantSequence_QueuedSequence",
                table: "RenderJobs",
                columns: new[] { "Status", "TenantSequence", "QueuedSequence" });

            migrationBuilder.CreateIndex(
                name: "IX_RenderJobs_TenantId_JobId",
                table: "RenderJobs",
                columns: new[] { "TenantId", "JobId" });

            migrationBuilder.CreateIndex(
                name: "IX_RenderJobs_TenantId_TenantSequence",
                table: "RenderJobs",
                columns: new[] { "TenantId", "TenantSequence" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RenderJobs");
        }
    }
}
