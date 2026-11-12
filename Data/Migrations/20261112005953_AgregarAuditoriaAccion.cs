using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace VetWeb.Data.Migrations
{
    /// <inheritdoc />
    public partial class AgregarAuditoriaAccion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AuditoriaAcciones",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    FechaUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UsuarioId = table.Column<string>(type: "text", nullable: false),
                    TutorId = table.Column<int>(type: "integer", nullable: false),
                    RolActivo = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Accion = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Entidad = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    EntidadId = table.Column<int>(type: "integer", nullable: false),
                    EstadoAnteriorId = table.Column<int>(type: "integer", nullable: true),
                    EstadoNuevoId = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditoriaAcciones", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AuditoriaAcciones_AspNetUsers_UsuarioId",
                        column: x => x.UsuarioId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AuditoriaAcciones_Tutores_TutorId",
                        column: x => x.TutorId,
                        principalTable: "Tutores",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AuditoriaAcciones_FechaUtc_Id",
                table: "AuditoriaAcciones",
                columns: new[] { "FechaUtc", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_AuditoriaAcciones_TutorId_FechaUtc_Id",
                table: "AuditoriaAcciones",
                columns: new[] { "TutorId", "FechaUtc", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_AuditoriaAcciones_UsuarioId",
                table: "AuditoriaAcciones",
                column: "UsuarioId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AuditoriaAcciones");
        }
    }
}
