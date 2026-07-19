using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Tempo.ReportServer.Api.Storage.Migrations
{
    /// <inheritdoc />
    public partial class AclSubjectKindAndEffect : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_FolderPermissions_TenantId_SubjectId_FolderId",
                table: "FolderPermissions");

            migrationBuilder.AddColumn<int>(
                name: "Effect",
                table: "FolderPermissions",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Permissions",
                table: "FolderPermissions",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SubjectKind",
                table: "FolderPermissions",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_FolderPermissions_TenantId_FolderId_SubjectKind_SubjectId_Effect",
                table: "FolderPermissions",
                columns: new[] { "TenantId", "FolderId", "SubjectKind", "SubjectId", "Effect" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_FolderPermissions_TenantId_FolderId_SubjectKind_SubjectId_Effect",
                table: "FolderPermissions");

            migrationBuilder.DropColumn(
                name: "Effect",
                table: "FolderPermissions");

            migrationBuilder.DropColumn(
                name: "Permissions",
                table: "FolderPermissions");

            migrationBuilder.DropColumn(
                name: "SubjectKind",
                table: "FolderPermissions");

            migrationBuilder.CreateIndex(
                name: "IX_FolderPermissions_TenantId_SubjectId_FolderId",
                table: "FolderPermissions",
                columns: new[] { "TenantId", "SubjectId", "FolderId" },
                unique: true);
        }
    }
}
