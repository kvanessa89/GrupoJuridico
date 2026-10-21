using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GrupoJuridico.Gestion.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class EstadoClienteManual : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Clientes_EstadosCliente_EstadoClienteId",
                table: "Clientes");

            migrationBuilder.RenameColumn(
                name: "EstadoClienteId",
                table: "Clientes",
                newName: "EstadoCalculadoId");

            migrationBuilder.RenameIndex(
                name: "IX_Clientes_EstadoClienteId",
                table: "Clientes",
                newName: "IX_Clientes_EstadoCalculadoId");

            migrationBuilder.AddColumn<int>(
                name: "EstadoManualId",
                table: "Clientes",
                type: "integer",
                nullable: true);

            // Terminado, Abandonado, Sin Expediente (y cualquier otro estado no automático) pasan a ser el estado manual;
            // luego el calculado se obtiene de las cuotas: Moroso con alguna vencida, Estrella con todas pagadas,
            // Al Día en otro caso o sin plan. ActualizadorCobros lo mantiene al día desde aquí.
            migrationBuilder.Sql("""
                UPDATE "Clientes" SET "EstadoManualId" = "EstadoCalculadoId" WHERE "EstadoCalculadoId" NOT IN (1, 2, 3);
                UPDATE "Clientes" c SET "EstadoCalculadoId" = CASE
                    WHEN NOT EXISTS (SELECT 1 FROM "Cuotas" q JOIN "PlanesPago" p ON p."Id" = q."PlanPagoId" WHERE p."ClienteId" = c."Id") THEN 1
                    WHEN EXISTS (SELECT 1 FROM "Cuotas" q JOIN "PlanesPago" p ON p."Id" = q."PlanPagoId"
                                 WHERE p."ClienteId" = c."Id" AND q."EstadoCuotaVencimientoId" = 2) THEN 2
                    WHEN NOT EXISTS (SELECT 1 FROM "Cuotas" q JOIN "PlanesPago" p ON p."Id" = q."PlanPagoId"
                                     WHERE p."ClienteId" = c."Id" AND q."EstadoCuotaPagoId" <> 3) THEN 3
                    ELSE 1 END;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_Clientes_EstadoManualId",
                table: "Clientes",
                column: "EstadoManualId");

            migrationBuilder.AddForeignKey(
                name: "FK_Clientes_EstadosCliente_EstadoCalculadoId",
                table: "Clientes",
                column: "EstadoCalculadoId",
                principalTable: "EstadosCliente",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Clientes_EstadosCliente_EstadoManualId",
                table: "Clientes",
                column: "EstadoManualId",
                principalTable: "EstadosCliente",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Clientes_EstadosCliente_EstadoCalculadoId",
                table: "Clientes");

            migrationBuilder.DropForeignKey(
                name: "FK_Clientes_EstadosCliente_EstadoManualId",
                table: "Clientes");

            migrationBuilder.DropIndex(
                name: "IX_Clientes_EstadoManualId",
                table: "Clientes");

            // Vuelve a una sola columna con el estado que se muestra.
            migrationBuilder.Sql("""
                UPDATE "Clientes" SET "EstadoCalculadoId" = "EstadoManualId" WHERE "EstadoManualId" IS NOT NULL;
                """);

            migrationBuilder.DropColumn(
                name: "EstadoManualId",
                table: "Clientes");

            migrationBuilder.RenameColumn(
                name: "EstadoCalculadoId",
                table: "Clientes",
                newName: "EstadoClienteId");

            migrationBuilder.RenameIndex(
                name: "IX_Clientes_EstadoCalculadoId",
                table: "Clientes",
                newName: "IX_Clientes_EstadoClienteId");

            migrationBuilder.AddForeignKey(
                name: "FK_Clientes_EstadosCliente_EstadoClienteId",
                table: "Clientes",
                column: "EstadoClienteId",
                principalTable: "EstadosCliente",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
