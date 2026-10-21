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
            migrationBuilder.AddColumn<int>(
                name: "EstadoManualId",
                table: "Clientes",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Clientes_EstadoManualId",
                table: "Clientes",
                column: "EstadoManualId");

            migrationBuilder.AddForeignKey(
                name: "FK_Clientes_EstadosCliente_EstadoManualId",
                table: "Clientes",
                column: "EstadoManualId",
                principalTable: "EstadosCliente",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            // Los estados manuales actuales (todo lo que no es Al Día, Moroso o Estrella) pasan a la columna nueva.
            // Un cliente sin estado manual ni plan de pago queda Al Día; los que tienen plan se recalculan al leerlos.
            migrationBuilder.Sql(@"
UPDATE ""Clientes"" SET ""EstadoManualId"" = ""EstadoClienteId"" WHERE ""EstadoClienteId"" NOT IN (1, 2, 3);
UPDATE ""Clientes"" c SET ""EstadoClienteId"" = 1
WHERE c.""EstadoManualId"" IS NULL AND c.""EstadoClienteId"" <> 1
  AND NOT EXISTS (SELECT 1 FROM ""PlanesPago"" p WHERE p.""ClienteId"" = c.""Id"");");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Clientes_EstadosCliente_EstadoManualId",
                table: "Clientes");

            migrationBuilder.DropIndex(
                name: "IX_Clientes_EstadoManualId",
                table: "Clientes");

            migrationBuilder.DropColumn(
                name: "EstadoManualId",
                table: "Clientes");
        }
    }
}
