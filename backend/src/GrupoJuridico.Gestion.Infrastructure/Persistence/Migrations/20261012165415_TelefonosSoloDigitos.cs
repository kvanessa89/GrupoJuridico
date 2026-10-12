using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GrupoJuridico.Gestion.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class TelefonosSoloDigitos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Los teléfonos pasan a guardarse solo con dígitos ("8812-4477" -> "88124477");
            // el prefijo internacional vive en el país.
            migrationBuilder.Sql("""
                UPDATE "Numeros" SET "Numero" = regexp_replace("Numero", '[^0-9]', '', 'g') WHERE "Numero" ~ '[^0-9]';
                UPDATE "PersonaFamiliares" SET "Telefono" = regexp_replace("Telefono", '[^0-9]', '', 'g') WHERE "Telefono" ~ '[^0-9]';
                UPDATE "PersonaFamiliares" SET "Whatsapp" = regexp_replace("Whatsapp", '[^0-9]', '', 'g') WHERE "Whatsapp" ~ '[^0-9]';
                UPDATE "PersonaFamiliares" SET "TelefonoPaisId" = NULL WHERE "Telefono" = '';
                UPDATE "PersonaFamiliares" SET "WhatsappPaisId" = NULL WHERE "Whatsapp" = '';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // No se puede recuperar el formato original (guiones y espacios).
        }
    }
}
