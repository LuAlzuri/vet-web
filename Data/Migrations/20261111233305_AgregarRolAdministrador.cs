using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VetWeb.Data.Migrations
{
    /// <inheritdoc />
    public partial class AgregarRolAdministrador : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "AspNetRoles",
                columns: new[] { "Id", "ConcurrencyStamp", "Name", "NormalizedName" },
                values: new object[] { "rol-administrador", "rol-administrador-v1", "Administrador", "ADMINISTRADOR" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: "rol-administrador");
        }
    }
}
