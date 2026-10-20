using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GrupoJuridico.Gestion.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class UnaVentaPorPersona : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Si alguna persona tiene más de una venta, se detiene con un mensaje claro en vez del error del índice.
            migrationBuilder.Sql(@"
DO $$
BEGIN
    IF EXISTS (SELECT 1 FROM ""Ventas"" GROUP BY ""PersonaId"" HAVING COUNT(*) > 1) THEN
        RAISE EXCEPTION 'Hay personas con más de una venta. Deje una sola venta por persona antes de aplicar la migración UnaVentaPorPersona.';
    END IF;
END $$;");

            migrationBuilder.DropIndex(
                name: "IX_Ventas_PersonaId",
                table: "Ventas");

            migrationBuilder.CreateIndex(
                name: "IX_Ventas_PersonaId",
                table: "Ventas",
                column: "PersonaId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Ventas_PersonaId",
                table: "Ventas");

            migrationBuilder.CreateIndex(
                name: "IX_Ventas_PersonaId",
                table: "Ventas",
                column: "PersonaId");
        }
    }
}
