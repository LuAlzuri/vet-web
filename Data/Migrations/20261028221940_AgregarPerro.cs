using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace VetWeb.Data.Migrations
{
    /// <inheritdoc />
    public partial class AgregarPerro : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Perros",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Nombre = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    FechaNacimiento = table.Column<DateTime>(type: "date", nullable: false),
                    Reactivo = table.Column<bool>(type: "boolean", nullable: false),
                    CalendarioVacunasCompleto = table.Column<bool>(type: "boolean", nullable: false),
                    TutorId = table.Column<int>(type: "integer", nullable: false),
                    GrupoEtarioId = table.Column<int>(type: "integer", nullable: false),
                    TamanioId = table.Column<int>(type: "integer", nullable: false),
                    Eliminado = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Perros", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Perros_GruposEtarios_GrupoEtarioId",
                        column: x => x.GrupoEtarioId,
                        principalTable: "GruposEtarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Perros_Tamanios_TamanioId",
                        column: x => x.TamanioId,
                        principalTable: "Tamanios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Perros_Tutores_TutorId",
                        column: x => x.TutorId,
                        principalTable: "Tutores",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Perros_GrupoEtarioId",
                table: "Perros",
                column: "GrupoEtarioId");

            migrationBuilder.CreateIndex(
                name: "IX_Perros_TamanioId",
                table: "Perros",
                column: "TamanioId");

            migrationBuilder.CreateIndex(
                name: "IX_Perros_TutorId",
                table: "Perros",
                column: "TutorId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Perros");
        }
    }
}
