using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VetWeb.Data.Migrations
{
    /// <inheritdoc />
    public partial class AgregarCantidadReservada : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CantidadReservada",
                table: "Turnos",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddCheckConstraint(
                name: "CK_Turnos_CantidadReservada",
                table: "Turnos",
                sql: "\"CantidadReservada\" >= 1");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Turnos_CantidadReservada",
                table: "Turnos");

            migrationBuilder.DropColumn(
                name: "CantidadReservada",
                table: "Turnos");
        }
    }
}
