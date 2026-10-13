using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GrupoJuridico.Gestion.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CatalogosEnUsoRestrict : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Personas_Vendedores_VendedorId",
                table: "Personas");

            migrationBuilder.DropForeignKey(
                name: "FK_Ventas_MetodosVenta_MetodoVentaId",
                table: "Ventas");

            migrationBuilder.DropForeignKey(
                name: "FK_Ventas_ProcedenciasVenta_ProcedenciaVentaId",
                table: "Ventas");

            migrationBuilder.AddForeignKey(
                name: "FK_Personas_Vendedores_VendedorId",
                table: "Personas",
                column: "VendedorId",
                principalTable: "Vendedores",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Ventas_MetodosVenta_MetodoVentaId",
                table: "Ventas",
                column: "MetodoVentaId",
                principalTable: "MetodosVenta",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Ventas_ProcedenciasVenta_ProcedenciaVentaId",
                table: "Ventas",
                column: "ProcedenciaVentaId",
                principalTable: "ProcedenciasVenta",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Personas_Vendedores_VendedorId",
                table: "Personas");

            migrationBuilder.DropForeignKey(
                name: "FK_Ventas_MetodosVenta_MetodoVentaId",
                table: "Ventas");

            migrationBuilder.DropForeignKey(
                name: "FK_Ventas_ProcedenciasVenta_ProcedenciaVentaId",
                table: "Ventas");

            migrationBuilder.AddForeignKey(
                name: "FK_Personas_Vendedores_VendedorId",
                table: "Personas",
                column: "VendedorId",
                principalTable: "Vendedores",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Ventas_MetodosVenta_MetodoVentaId",
                table: "Ventas",
                column: "MetodoVentaId",
                principalTable: "MetodosVenta",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Ventas_ProcedenciasVenta_ProcedenciaVentaId",
                table: "Ventas",
                column: "ProcedenciaVentaId",
                principalTable: "ProcedenciasVenta",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }
    }
}
