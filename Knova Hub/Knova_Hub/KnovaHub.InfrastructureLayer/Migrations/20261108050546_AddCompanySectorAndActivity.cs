using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KnovaHub.InfrastructureLayer.Migrations
{
    /// <inheritdoc />
    public partial class AddCompanySectorAndActivity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Activity",
                table: "Companies",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Sector",
                table: "Companies",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.Sql(@"
        UPDATE [Companies]
        SET [Activity] = N'Actividad no especificada'
        WHERE [Activity] IS NULL OR LTRIM(RTRIM([Activity])) = '';

        UPDATE [Companies]
        SET [Sector] = N'Otros'
        WHERE [Sector] IS NULL OR LTRIM(RTRIM([Sector])) = '';
    ");

            migrationBuilder.AlterColumn<string>(
                name: "Activity",
                table: "Companies",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(250)",
                oldMaxLength: 250,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Sector",
                table: "Companies",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100,
                oldNullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_Companies_Activity",
                table: "Companies",
                sql: "LEN(LTRIM(RTRIM([Activity]))) > 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Companies_Sector",
                table: "Companies",
                sql: "[Sector] IN (N'Agricultura y ganadería', N'Comercio', N'Construcción e inmobiliaria', N'Educación', N'Energía y servicios públicos', N'Finanzas y seguros', N'Industria y manufactura', N'Salud', N'Tecnología y telecomunicaciones', N'Transporte y logística', N'Turismo, hotelería y gastronomía', N'Servicios profesionales', N'Medios y entretenimiento', N'Otros')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Companies_Activity",
                table: "Companies");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Companies_Sector",
                table: "Companies");

            migrationBuilder.DropColumn(
                name: "Activity",
                table: "Companies");

            migrationBuilder.DropColumn(
                name: "Sector",
                table: "Companies");
        }
    }
}
