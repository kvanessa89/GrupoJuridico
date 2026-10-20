using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace GrupoJuridico.Gestion.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ComprobantesEnExpediente : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "PagoId",
                table: "ArchivosPersona",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ArchivosPersona_PagoId",
                table: "ArchivosPersona",
                column: "PagoId");

            migrationBuilder.AddForeignKey(
                name: "FK_ArchivosPersona_Pagos_PagoId",
                table: "ArchivosPersona",
                column: "PagoId",
                principalTable: "Pagos",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            // El tipo "Comprobantes de pago" pasa a tener un código fijo (si ya existía, se reutiliza; si no, se crea).
            // Sus permisos los asigna la carga inicial al iniciar la API.
            migrationBuilder.Sql(@"
UPDATE ""TiposDocumento"" SET ""Codigo"" = 'COMPROBANTES-PAGO'
WHERE ""Id"" = (SELECT MIN(""Id"") FROM ""TiposDocumento"" WHERE ""Nombre"" = 'Comprobantes de pago')
  AND NOT EXISTS (SELECT 1 FROM ""TiposDocumento"" WHERE ""Codigo"" = 'COMPROBANTES-PAGO');

INSERT INTO ""TiposDocumento"" (""Codigo"", ""Nombre"", ""ModificadoEn"")
SELECT 'COMPROBANTES-PAGO', 'Comprobantes de pago', now()
WHERE NOT EXISTS (SELECT 1 FROM ""TiposDocumento"" WHERE ""Codigo"" = 'COMPROBANTES-PAGO');");

            // Los comprobantes pasan a ser archivos del expediente del cliente. Los registros sin archivo guardado
            // (solo existían en los datos de demostración) no se copian.
            migrationBuilder.Sql(@"
INSERT INTO ""ArchivosPersona"" (""PersonaId"", ""TipoDocumentoId"", ""Nombre"", ""TipoContenido"", ""Tamano"", ""FechaCarga"", ""Clave"", ""PagoId"", ""ModificadoEn"", ""ModificadoPorId"")
SELECT per.""Id"", (SELECT ""Id"" FROM ""TiposDocumento"" WHERE ""Codigo"" = 'COMPROBANTES-PAGO'),
       ap.""Nombre"", ap.""TipoContenido"", ap.""Tamano"", ap.""FechaCarga"", ap.""Clave"", ap.""PagoId"", ap.""ModificadoEn"", ap.""ModificadoPorId""
FROM ""ArchivosPago"" ap
JOIN ""Pagos"" pg ON pg.""Id"" = ap.""PagoId""
JOIN ""Cuotas"" c ON c.""Id"" = pg.""CuotaId""
JOIN ""PlanesPago"" pl ON pl.""Id"" = c.""PlanPagoId""
JOIN ""Personas"" per ON per.""ClienteId"" = pl.""ClienteId""
WHERE ap.""Clave"" <> '';");

            migrationBuilder.DropTable(
                name: "ArchivosPago");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ArchivosPago",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Clave = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    FechaCarga = table.Column<DateOnly>(type: "date", nullable: false),
                    ModificadoEn = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModificadoPorId = table.Column<int>(type: "integer", nullable: true),
                    Nombre = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    PagoId = table.Column<int>(type: "integer", nullable: false),
                    Tamano = table.Column<long>(type: "bigint", nullable: false),
                    TipoContenido = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ArchivosPago", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ArchivosPago_Pagos_PagoId",
                        column: x => x.PagoId,
                        principalTable: "Pagos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ArchivosPago_PagoId",
                table: "ArchivosPago",
                column: "PagoId");

            migrationBuilder.Sql(@"
INSERT INTO ""ArchivosPago"" (""PagoId"", ""Nombre"", ""TipoContenido"", ""Tamano"", ""FechaCarga"", ""Clave"", ""ModificadoEn"", ""ModificadoPorId"")
SELECT ""PagoId"", ""Nombre"", ""TipoContenido"", ""Tamano"", ""FechaCarga"", ""Clave"", ""ModificadoEn"", ""ModificadoPorId""
FROM ""ArchivosPersona"" WHERE ""PagoId"" IS NOT NULL;
DELETE FROM ""ArchivosPersona"" WHERE ""PagoId"" IS NOT NULL;");


            migrationBuilder.DropForeignKey(
                name: "FK_ArchivosPersona_Pagos_PagoId",
                table: "ArchivosPersona");

            migrationBuilder.DropIndex(
                name: "IX_ArchivosPersona_PagoId",
                table: "ArchivosPersona");

            migrationBuilder.DropColumn(
                name: "PagoId",
                table: "ArchivosPersona");
        }
    }
}
