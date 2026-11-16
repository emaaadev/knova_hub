using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KnovaHub.InfrastructureLayer.Migrations;

public partial class AddDocumentReads : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "DocumentReads",
            columns: table => new
            {
                Id = table.Column<long>(type: "bigint", nullable: false)
                    .Annotation("SqlServer:Identity", "1, 1"),
                CompanyId = table.Column<int>(type: "int", nullable: false),
                DocumentId = table.Column<int>(type: "int", nullable: false),
                UserId = table.Column<int>(type: "int", nullable: false),
                ReadAt = table.Column<DateTime>(type: "datetime2", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_DocumentReads", row => row.Id);
                table.ForeignKey(
                    name: "FK_DocumentReads_Documents_DocumentId_CompanyId",
                    columns: row => new { row.DocumentId, row.CompanyId },
                    principalTable: "Documents",
                    principalColumns: new[] { "Id", "CompanyId" },
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_DocumentReads_DocumentId_CompanyId",
            table: "DocumentReads",
            columns: new[] { "DocumentId", "CompanyId" });

        migrationBuilder.CreateIndex(
            name: "IX_DocumentReads_CompanyId_UserId_ReadAt",
            table: "DocumentReads",
            columns: new[] { "CompanyId", "UserId", "ReadAt" });

        migrationBuilder.CreateIndex(
            name: "IX_QueryLogs_CompanyId_UserId_AskedAt",
            table: "QueryLogs",
            columns: new[] { "CompanyId", "UserId", "AskedAt" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(name: "IX_QueryLogs_CompanyId_UserId_AskedAt", table: "QueryLogs");
        migrationBuilder.DropTable(name: "DocumentReads");
    }
}
