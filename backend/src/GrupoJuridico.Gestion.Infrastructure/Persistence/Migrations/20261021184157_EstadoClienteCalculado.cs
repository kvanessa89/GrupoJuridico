using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GrupoJuridico.Gestion.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class EstadoClienteCalculado : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "EstadoClienteCalculadoId",
                table: "Clientes",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.CreateTable(
                name: "EstadosClienteCalculado",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false),
                    ModificadoEn = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModificadoPorId = table.Column<int>(type: "integer", nullable: true),
                    Codigo = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Nombre = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EstadosClienteCalculado", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Clientes_EstadoClienteCalculadoId",
                table: "Clientes",
                column: "EstadoClienteCalculadoId");

            // Al Día, Moroso y Estrella pasan a su tabla con los mismos ids y nombres (en una base nueva la llena DbSeeder).
            // El estado calculado sale de las cuotas: Moroso con alguna vencida, Estrella con todas pagadas, Al Día en
            // otro caso o sin plan; ActualizadorCobros lo mantiene desde aquí. En EstadosCliente, Al Día pasa a ser
            // Activo (automático) y los clientes que estaban Moroso o Estrella quedan Activos.
            migrationBuilder.Sql("""
                INSERT INTO "EstadosClienteCalculado" ("Id", "ModificadoEn", "ModificadoPorId", "Codigo", "Nombre")
                SELECT "Id", "ModificadoEn", "ModificadoPorId", 'ECC-' || "Id", "Nombre" FROM "EstadosCliente" WHERE "Id" IN (1, 2, 3);

                UPDATE "Clientes" c SET "EstadoClienteCalculadoId" = CASE
                    WHEN NOT EXISTS (SELECT 1 FROM "Cuotas" q JOIN "PlanesPago" p ON p."Id" = q."PlanPagoId" WHERE p."ClienteId" = c."Id") THEN 1
                    WHEN EXISTS (SELECT 1 FROM "Cuotas" q JOIN "PlanesPago" p ON p."Id" = q."PlanPagoId"
                                 WHERE p."ClienteId" = c."Id" AND q."EstadoCuotaVencimientoId" = 2) THEN 2
                    WHEN NOT EXISTS (SELECT 1 FROM "Cuotas" q JOIN "PlanesPago" p ON p."Id" = q."PlanPagoId"
                                     WHERE p."ClienteId" = c."Id" AND q."EstadoCuotaPagoId" <> 3) THEN 3
                    ELSE 1 END;

                UPDATE "Clientes" SET "EstadoClienteId" = 1 WHERE "EstadoClienteId" IN (2, 3);
                UPDATE "EstadosCliente" SET "Nombre" = 'Activo' WHERE "Id" = 1;
                DELETE FROM "EstadosCliente" WHERE "Id" IN (2, 3);
                """);

            migrationBuilder.AddForeignKey(
                name: "FK_Clientes_EstadosClienteCalculado_EstadoClienteCalculadoId",
                table: "Clientes",
                column: "EstadoClienteCalculadoId",
                principalTable: "EstadosClienteCalculado",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Vuelve a un solo catálogo: los clientes Activos toman su estado calculado.
            migrationBuilder.Sql("""
                INSERT INTO "EstadosCliente" ("Id", "ModificadoEn", "ModificadoPorId", "Codigo", "Nombre")
                SELECT "Id", "ModificadoEn", "ModificadoPorId", 'EC-' || "Id", "Nombre" FROM "EstadosClienteCalculado" WHERE "Id" IN (2, 3);
                UPDATE "EstadosCliente" e SET "Nombre" = k."Nombre" FROM "EstadosClienteCalculado" k WHERE e."Id" = 1 AND k."Id" = 1;
                UPDATE "Clientes" SET "EstadoClienteId" = "EstadoClienteCalculadoId" WHERE "EstadoClienteId" = 1;
                """);

            migrationBuilder.DropForeignKey(
                name: "FK_Clientes_EstadosClienteCalculado_EstadoClienteCalculadoId",
                table: "Clientes");

            migrationBuilder.DropTable(
                name: "EstadosClienteCalculado");

            migrationBuilder.DropIndex(
                name: "IX_Clientes_EstadoClienteCalculadoId",
                table: "Clientes");

            migrationBuilder.DropColumn(
                name: "EstadoClienteCalculadoId",
                table: "Clientes");
        }
    }
}
