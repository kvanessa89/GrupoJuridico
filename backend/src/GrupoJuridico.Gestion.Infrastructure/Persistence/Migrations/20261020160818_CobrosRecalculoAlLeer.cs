using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GrupoJuridico.Gestion.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CobrosRecalculoAlLeer : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_PromesasPago_Pendientes",
                table: "PromesasPago",
                column: "FechaPromesa",
                filter: "\"EstadoPromesaId\" = 1");

            migrationBuilder.CreateIndex(
                name: "IX_Cuotas_PendientesAlDia",
                table: "Cuotas",
                column: "FechaVencimiento",
                filter: "\"EstadoCuotaVencimientoId\" = 1 AND \"EstadoCuotaPagoId\" <> 3");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PromesasPago_Pendientes",
                table: "PromesasPago");

            migrationBuilder.DropIndex(
                name: "IX_Cuotas_PendientesAlDia",
                table: "Cuotas");
        }
    }
}
