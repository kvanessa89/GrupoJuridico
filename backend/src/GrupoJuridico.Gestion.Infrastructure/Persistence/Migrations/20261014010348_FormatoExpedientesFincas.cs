using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GrupoJuridico.Gestion.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Lleva los expedientes y fincas existentes al formato que exige la validación
    /// (12-123456-1234-CJ y 12-12345), a partir de los dígitos de cada valor.
    /// Los vacíos no se tocan: no se inventan expedientes ni fincas.
    /// </summary>
    public partial class FormatoExpedientesFincas : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Expedientes con el formato pero otras letras al final: solo se cambian por CJ.
            migrationBuilder.Sql("""
                UPDATE "Personas"
                SET "Expediente" = left(trim("Expediente"), 15) || 'CJ'
                WHERE trim("Expediente") ~ '^[0-9]{2}-[0-9]{6}-[0-9]{4}-[A-Za-z]{2}$'
                  AND "Expediente" <> left(trim("Expediente"), 15) || 'CJ';
                """);

            // EXP-2026-018 → 00-000018-2026-CJ: el número va al bloque de 6 y el año al de 4.
            migrationBuilder.Sql("""
                UPDATE "Personas"
                SET "Expediente" = '00-'
                    || lpad((regexp_match(trim("Expediente"), '^[A-Za-z]+-([0-9]{4})-([0-9]{1,6})$'))[2], 6, '0')
                    || '-' || (regexp_match(trim("Expediente"), '^[A-Za-z]+-([0-9]{4})-([0-9]{1,6})$'))[1]
                    || '-CJ'
                WHERE trim("Expediente") ~ '^[A-Za-z]+-[0-9]{4}-[0-9]{1,6}$';
                """);

            // Cualquier otro expediente con dígitos: sus primeros 12 dígitos (con ceros a la izquierda
            // si faltan; lpad recorta los sobrantes), repartidos en 2-6-4. Sin dígitos no hay de dónde sacarlo y se deja igual.
            migrationBuilder.Sql("""
                UPDATE "Personas"
                SET "Expediente" =
                       substr(lpad(regexp_replace("Expediente", '[^0-9]', '', 'g'), 12, '0'), 1, 2)
                    || '-' || substr(lpad(regexp_replace("Expediente", '[^0-9]', '', 'g'), 12, '0'), 3, 6)
                    || '-' || substr(lpad(regexp_replace("Expediente", '[^0-9]', '', 'g'), 12, '0'), 9, 4)
                    || '-CJ'
                WHERE trim("Expediente") !~ '^[0-9]{2}-[0-9]{6}-[0-9]{4}-CJ$'
                  AND "Expediente" ~ '[0-9]';
                """);

            // Quita espacios sobrantes de los expedientes que ya cumplían.
            migrationBuilder.Sql("""
                UPDATE "Personas"
                SET "Expediente" = trim("Expediente")
                WHERE "Expediente" <> trim("Expediente")
                  AND trim("Expediente") ~ '^[0-9]{2}-[0-9]{6}-[0-9]{4}-CJ$';
                """);

            // Fincas "01-200411" → "01-20041": los 2 dígitos iniciales y los primeros 5 del bloque
            // (si el bloque tiene menos de 5 se completa con ceros a la izquierda).
            migrationBuilder.Sql("""
                UPDATE "Fincas"
                SET "Numero" = lpad(split_part(trim("Numero"), '-', 1), 2, '0') || '-' ||
                    CASE WHEN length(split_part(trim("Numero"), '-', 2)) >= 5
                         THEN left(split_part(trim("Numero"), '-', 2), 5)
                         ELSE lpad(split_part(trim("Numero"), '-', 2), 5, '0') END
                WHERE trim("Numero") ~ '^[0-9]{1,2}-[0-9]+$'
                  AND "Numero" !~ '^[0-9]{2}-[0-9]{5}$';
                """);

            // Cualquier otra finca con dígitos: sus primeros 7 dígitos (con ceros a la izquierda si faltan), en 2-5.
            migrationBuilder.Sql("""
                UPDATE "Fincas"
                SET "Numero" =
                       left(lpad(regexp_replace("Numero", '[^0-9]', '', 'g'), 7, '0'), 2)
                    || '-' || substr(lpad(regexp_replace("Numero", '[^0-9]', '', 'g'), 7, '0'), 3, 5)
                WHERE "Numero" !~ '^[0-9]{2}-[0-9]{5}$'
                  AND "Numero" ~ '[0-9]';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Los valores originales no se guardan: la conversión no se puede deshacer.
        }
    }
}
