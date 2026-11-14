using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KnovaHub.InfrastructureLayer.Migrations
{
    /// <inheritdoc />
    public partial class Ticket2Avance4_DocumentTexts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "TextStatus",
                table: "Documents",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TextStatusReason",
                table: "Documents",
                type: "nvarchar(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "DocumentTexts",
                columns: table => new
                {
                    DocumentId = table.Column<int>(type: "int", nullable: false),
                    CompanyId = table.Column<int>(type: "int", nullable: false),
                    Markdown = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CharacterCount = table.Column<int>(type: "int", nullable: false),
                    ProcessedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DocumentTexts", x => x.DocumentId);
                    table.CheckConstraint("CK_DocumentTexts_CharacterCount", "[CharacterCount] > 0");
                    table.CheckConstraint("CK_DocumentTexts_Markdown", "LEN([Markdown]) > 0");
                    table.ForeignKey(
                        name: "FK_DocumentTexts_Documents_DocumentId_CompanyId",
                        columns: x => new { x.DocumentId, x.CompanyId },
                        principalTable: "Documents",
                        principalColumns: new[] { "Id", "CompanyId" },
                        onDelete: ReferentialAction.Cascade);
                });

            // Los archivos subidos antes de esta migración aún no tienen Markdown: quedan Pendiente (1).
            // Sin este paso, CK_Documents_TextStatus fallaría sobre una base de datos con documentos.
            migrationBuilder.Sql(
                "UPDATE [Documents] SET [TextStatus] = 1 WHERE [StorageKey] IS NOT NULL AND [TextStatus] IS NULL;");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Documents_TextStatus",
                table: "Documents",
                sql: "([StorageKey] IS NULL AND [TextStatus] IS NULL) OR ([StorageKey] IS NOT NULL AND [TextStatus] IS NOT NULL AND [TextStatus] IN (1, 2, 3))");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentTexts_DocumentId_CompanyId",
                table: "DocumentTexts",
                columns: new[] { "DocumentId", "CompanyId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DocumentTexts");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Documents_TextStatus",
                table: "Documents");

            migrationBuilder.DropColumn(
                name: "TextStatus",
                table: "Documents");

            migrationBuilder.DropColumn(
                name: "TextStatusReason",
                table: "Documents");
        }
    }
}
