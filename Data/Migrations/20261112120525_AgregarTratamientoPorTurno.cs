using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VetWeb.Data.Migrations
{
    /// <inheritdoc />
    public partial class AgregarTratamientoPorTurno : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Turnos_Tratamientos_TratamientoId",
                table: "Turnos");

            migrationBuilder.DropIndex(
                name: "IX_Turnos_TratamientoId",
                table: "Turnos");

            migrationBuilder.CreateTable(
                name: "TratamientosPorTurno",
                columns: table => new
                {
                    TurnoId = table.Column<int>(type: "integer", nullable: false),
                    TratamientoId = table.Column<int>(type: "integer", nullable: false),
                    Razon = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TratamientosPorTurno", x => new { x.TurnoId, x.TratamientoId });
                    table.ForeignKey(
                        name: "FK_TratamientosPorTurno_Tratamientos_TratamientoId",
                        column: x => x.TratamientoId,
                        principalTable: "Tratamientos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TratamientosPorTurno_Turnos_TurnoId",
                        column: x => x.TurnoId,
                        principalTable: "Turnos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.Sql("""
                INSERT INTO "TratamientosPorTurno" ("TurnoId", "TratamientoId")
                SELECT "Id", "TratamientoId" FROM "Turnos" WHERE "TratamientoId" IS NOT NULL;
                """);

            migrationBuilder.DropColumn(
                name: "TratamientoId",
                table: "Turnos");

            migrationBuilder.CreateIndex(
                name: "IX_TratamientosPorTurno_TratamientoId",
                table: "TratamientosPorTurno",
                column: "TratamientoId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // El modelo anterior no puede representar varios tratamientos ni motivos.
            migrationBuilder.Sql("""
                DO $$ BEGIN
                    IF EXISTS (SELECT 1 FROM "TratamientosPorTurno" GROUP BY "TurnoId" HAVING count(*) > 1)
                       OR EXISTS (SELECT 1 FROM "TratamientosPorTurno" WHERE "Razon" IS NOT NULL) THEN
                        RAISE EXCEPTION 'No se puede revertir sin perder tratamientos o sus motivos.';
                    END IF;
                END $$;
                """);

            migrationBuilder.AddColumn<int>(
                name: "TratamientoId",
                table: "Turnos",
                type: "integer",
                nullable: true);

            migrationBuilder.Sql("""
                UPDATE "Turnos" AS turno SET "TratamientoId" = relacion."TratamientoId"
                FROM "TratamientosPorTurno" AS relacion WHERE relacion."TurnoId" = turno."Id";
                """);
            migrationBuilder.DropTable(name: "TratamientosPorTurno");

            migrationBuilder.CreateIndex(
                name: "IX_Turnos_TratamientoId",
                table: "Turnos",
                column: "TratamientoId");

            migrationBuilder.AddForeignKey(
                name: "FK_Turnos_Tratamientos_TratamientoId",
                table: "Turnos",
                column: "TratamientoId",
                principalTable: "Tratamientos",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
