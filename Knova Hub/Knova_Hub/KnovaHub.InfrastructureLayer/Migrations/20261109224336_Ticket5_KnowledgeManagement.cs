using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KnovaHub.InfrastructureLayer.Migrations
{
    /// <inheritdoc />
    public partial class Ticket5_KnowledgeManagement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddUniqueConstraint(
                name: "AK_Departments_Id_CompanyId",
                table: "Departments",
                columns: new[] { "Id", "CompanyId" });

            migrationBuilder.CreateTable(
                name: "Categories",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CompanyId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Categories", x => x.Id);
                    table.UniqueConstraint("AK_Categories_Id_CompanyId", x => new { x.Id, x.CompanyId });
                    table.CheckConstraint("CK_Categories_Name", "LEN(LTRIM(RTRIM([Name]))) > 0");
                    table.ForeignKey(
                        name: "FK_Categories_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Tags",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    CompanyId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tags", x => x.Id);
                    table.UniqueConstraint("AK_Tags_Id_CompanyId", x => new { x.Id, x.CompanyId });
                    table.CheckConstraint("CK_Tags_Name", "LEN(LTRIM(RTRIM([Name]))) > 0");
                    table.ForeignKey(
                        name: "FK_Tags_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Documents",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Content = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Type = table.Column<int>(type: "int", nullable: false),
                    Visibility = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CompanyId = table.Column<int>(type: "int", nullable: false),
                    AuthorId = table.Column<int>(type: "int", nullable: false),
                    CategoryId = table.Column<int>(type: "int", nullable: true),
                    DepartmentId = table.Column<int>(type: "int", nullable: true),
                    OriginalFileName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    StorageKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ContentType = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    FileExtension = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    FileSizeBytes = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Documents", x => x.Id);
                    table.UniqueConstraint("AK_Documents_Id_CompanyId", x => new { x.Id, x.CompanyId });
                    table.CheckConstraint("CK_Documents_FileMetadata", "(([StorageKey] IS NULL AND [OriginalFileName] IS NULL AND [ContentType] IS NULL AND [FileExtension] IS NULL AND [FileSizeBytes] IS NULL) OR ([StorageKey] IS NOT NULL AND [OriginalFileName] IS NOT NULL AND [ContentType] IS NOT NULL AND [FileExtension] IS NOT NULL AND [FileSizeBytes] > 0))");
                    table.CheckConstraint("CK_Documents_Title", "LEN(LTRIM(RTRIM([Title]))) > 0");
                    table.CheckConstraint("CK_Documents_Type", "[Type] IN (1, 2, 3)");
                    table.CheckConstraint("CK_Documents_Visibility", "[Visibility] IN (1, 2)");
                    table.CheckConstraint("CK_Documents_VisibilityDepartment", "([Visibility] = 1 AND [DepartmentId] IS NULL) OR ([Visibility] = 2 AND [DepartmentId] IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_Documents_Categories_CategoryId_CompanyId",
                        columns: x => new { x.CategoryId, x.CompanyId },
                        principalTable: "Categories",
                        principalColumns: new[] { "Id", "CompanyId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Documents_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Documents_Departments_DepartmentId_CompanyId",
                        columns: x => new { x.DepartmentId, x.CompanyId },
                        principalTable: "Departments",
                        principalColumns: new[] { "Id", "CompanyId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Documents_Users_AuthorId",
                        column: x => x.AuthorId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DocumentTags",
                columns: table => new
                {
                    DocumentId = table.Column<int>(type: "int", nullable: false),
                    TagId = table.Column<int>(type: "int", nullable: false),
                    CompanyId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DocumentTags", x => new { x.DocumentId, x.TagId });
                    table.ForeignKey(
                        name: "FK_DocumentTags_Documents_DocumentId_CompanyId",
                        columns: x => new { x.DocumentId, x.CompanyId },
                        principalTable: "Documents",
                        principalColumns: new[] { "Id", "CompanyId" },
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DocumentTags_Tags_TagId_CompanyId",
                        columns: x => new { x.TagId, x.CompanyId },
                        principalTable: "Tags",
                        principalColumns: new[] { "Id", "CompanyId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Categories_CompanyId_Name",
                table: "Categories",
                columns: new[] { "CompanyId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Documents_AuthorId",
                table: "Documents",
                column: "AuthorId");

            migrationBuilder.CreateIndex(
                name: "IX_Documents_CategoryId_CompanyId",
                table: "Documents",
                columns: new[] { "CategoryId", "CompanyId" });

            migrationBuilder.CreateIndex(
                name: "IX_Documents_CompanyId_CategoryId",
                table: "Documents",
                columns: new[] { "CompanyId", "CategoryId" });

            migrationBuilder.CreateIndex(
                name: "IX_Documents_CompanyId_CreatedAt",
                table: "Documents",
                columns: new[] { "CompanyId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Documents_CompanyId_DepartmentId_Visibility",
                table: "Documents",
                columns: new[] { "CompanyId", "DepartmentId", "Visibility" });

            migrationBuilder.CreateIndex(
                name: "IX_Documents_CompanyId_Type",
                table: "Documents",
                columns: new[] { "CompanyId", "Type" });

            migrationBuilder.CreateIndex(
                name: "IX_Documents_DepartmentId_CompanyId",
                table: "Documents",
                columns: new[] { "DepartmentId", "CompanyId" });

            migrationBuilder.CreateIndex(
                name: "IX_DocumentTags_DocumentId_CompanyId",
                table: "DocumentTags",
                columns: new[] { "DocumentId", "CompanyId" });

            migrationBuilder.CreateIndex(
                name: "IX_DocumentTags_TagId",
                table: "DocumentTags",
                column: "TagId");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentTags_TagId_CompanyId",
                table: "DocumentTags",
                columns: new[] { "TagId", "CompanyId" });

            migrationBuilder.CreateIndex(
                name: "IX_Tags_CompanyId_Name",
                table: "Tags",
                columns: new[] { "CompanyId", "Name" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DocumentTags");

            migrationBuilder.DropTable(
                name: "Documents");

            migrationBuilder.DropTable(
                name: "Tags");

            migrationBuilder.DropTable(
                name: "Categories");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_Departments_Id_CompanyId",
                table: "Departments");
        }
    }
}
