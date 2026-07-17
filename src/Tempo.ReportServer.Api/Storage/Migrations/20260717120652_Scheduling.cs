using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Tempo.ReportServer.Api.Storage.Migrations
{
    /// <inheritdoc />
    public partial class Scheduling : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ScheduleRuns",
                columns: table => new
                {
                    RunId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    TenantId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    ScheduleId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    OccurrenceUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    StartedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CompletedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Attempt = table.Column<int>(type: "int", nullable: false),
                    DeliveryKind = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    DeliveryTarget = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: false),
                    ArtifactFileName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ArtifactContentType = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    ArtifactByteCount = table.Column<int>(type: "int", nullable: false),
                    ErrorMessage = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScheduleRuns", x => x.RunId);
                });

            migrationBuilder.CreateTable(
                name: "Schedules",
                columns: table => new
                {
                    ScheduleId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    TenantId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    OwnerUserId = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ReportId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    CronExpression = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Format = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    CultureName = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    ParametersJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DeliveryKind = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    DeliveryTarget = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: false),
                    MissedRunPolicy = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    IsEnabled = table.Column<bool>(type: "bit", nullable: false),
                    NextRunUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    LastRunUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    LastDeliveredUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    RetryAfterUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    FailureCount = table.Column<int>(type: "int", nullable: false),
                    MaxAttempts = table.Column<int>(type: "int", nullable: false),
                    LastStatus = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    LastStatusMessage = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    PendingOccurrencesJson = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Schedules", x => x.ScheduleId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ScheduleRuns_TenantId_ScheduleId_OccurrenceUtc",
                table: "ScheduleRuns",
                columns: new[] { "TenantId", "ScheduleId", "OccurrenceUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Schedules_IsEnabled_NextRunUtc",
                table: "Schedules",
                columns: new[] { "IsEnabled", "NextRunUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Schedules_TenantId_ScheduleId",
                table: "Schedules",
                columns: new[] { "TenantId", "ScheduleId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ScheduleRuns");

            migrationBuilder.DropTable(
                name: "Schedules");
        }
    }
}
