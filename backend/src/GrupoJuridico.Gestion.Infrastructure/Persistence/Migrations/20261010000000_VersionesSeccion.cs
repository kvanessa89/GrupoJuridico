using GrupoJuridico.Gestion.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace GrupoJuridico.Gestion.Infrastructure.Persistence.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20261010000000_VersionesSeccion")]
public partial class VersionesSeccion : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "VersionesSeccion",
            columns: table => new
            {
                PersonaId = table.Column<int>(type: "integer", nullable: false),
                Seccion = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                Version = table.Column<Guid>(type: "uuid", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_VersionesSeccion", x => new { x.PersonaId, x.Seccion });
                table.ForeignKey("FK_VersionesSeccion_Personas_PersonaId", x => x.PersonaId, "Personas", "Id", onDelete: ReferentialAction.Cascade);
            });
        // UUID determinista por registro/sección: no depende de extensiones de PostgreSQL.
        migrationBuilder.Sql("""
            INSERT INTO "VersionesSeccion" ("PersonaId", "Seccion", "Version")
            SELECT p."Id", s.nombre, md5(p."Id"::text || ':' || s.nombre)::uuid
            FROM "Personas" p
            CROSS JOIN (VALUES ('datos'), ('venta'), ('prima'), ('familiares')) s(nombre);
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.DropTable("VersionesSeccion");
}
