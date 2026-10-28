using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VetWeb.Data.Migrations
{
    /// <inheritdoc />
    public partial class AgregarPerroPatologia : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PerrosPatologias",
                columns: table => new
                {
                    PerroId = table.Column<int>(type: "integer", nullable: false),
                    PatologiaId = table.Column<int>(type: "integer", nullable: false),
                    Controlado = table.Column<bool>(type: "boolean", nullable: false),
                    Observacion = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PerrosPatologias", x => new { x.PerroId, x.PatologiaId });
                    table.ForeignKey(
                        name: "FK_PerrosPatologias_Patologias_PatologiaId",
                        column: x => x.PatologiaId,
                        principalTable: "Patologias",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PerrosPatologias_Perros_PerroId",
                        column: x => x.PerroId,
                        principalTable: "Perros",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PerrosPatologias_PatologiaId",
                table: "PerrosPatologias",
                column: "PatologiaId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PerrosPatologias");
        }
    }
}
