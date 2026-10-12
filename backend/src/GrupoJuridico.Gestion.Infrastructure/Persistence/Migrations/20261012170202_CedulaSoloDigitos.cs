using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GrupoJuridico.Gestion.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CedulaSoloDigitos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // La cédula pasa a guardarse solo con dígitos ("1-1088-0714" -> "110880714").
            migrationBuilder.Sql("""
                UPDATE "Personas" SET "Cedula" = regexp_replace("Cedula", '[^0-9]', '', 'g') WHERE "Cedula" ~ '[^0-9]';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // No se puede recuperar el formato original (guiones y espacios).
        }
    }
}
