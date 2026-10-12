using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace GrupoJuridico.Gestion.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Parentescos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Parentescos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ModificadoEn = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModificadoPorId = table.Column<int>(type: "integer", nullable: true),
                    Codigo = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Nombre = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Parentescos", x => x.Id);
                });

            migrationBuilder.AddColumn<int>(
                name: "ParentescoId",
                table: "PersonaFamiliares",
                type: "integer",
                nullable: true);

            // Catálogo inicial. El texto libre existente se asigna a la opción equivalente
            // (Hija -> Hijo/a, Esposa -> Cónyuge…); lo que no encaja se agrega al catálogo para no perderlo.
            migrationBuilder.Sql("""
                INSERT INTO "Parentescos" ("Codigo", "Nombre", "ModificadoEn") VALUES
                    ('PA-1', 'Cónyuge', now()), ('PA-2', 'Hijo/a', now()), ('PA-3', 'Madre', now()),
                    ('PA-4', 'Padre', now()), ('PA-5', 'Hermano/a', now());

                UPDATE "PersonaFamiliares" f SET "ParentescoId" = p."Id"
                FROM "Parentescos" p
                WHERE p."Nombre" = CASE
                    WHEN lower(trim(f."Parentesco")) IN ('cónyuge', 'conyuge', 'cónyugue', 'conyugue', 'esposo', 'esposa', 'esposo/a') THEN 'Cónyuge'
                    WHEN lower(trim(f."Parentesco")) IN ('hijo', 'hija', 'hijo/a') THEN 'Hijo/a'
                    WHEN lower(trim(f."Parentesco")) IN ('madre', 'mamá', 'mama') THEN 'Madre'
                    WHEN lower(trim(f."Parentesco")) IN ('padre', 'papá', 'papa') THEN 'Padre'
                    WHEN lower(trim(f."Parentesco")) IN ('hermano', 'hermana', 'hermano/a') THEN 'Hermano/a'
                END;

                INSERT INTO "Parentescos" ("Codigo", "Nombre", "ModificadoEn")
                SELECT '', min(trim("Parentesco")), now() FROM "PersonaFamiliares"
                WHERE "ParentescoId" IS NULL AND trim("Parentesco") <> ''
                GROUP BY lower(trim("Parentesco"));
                UPDATE "Parentescos" SET "Codigo" = 'PA-' || "Id" WHERE "Codigo" = '';

                UPDATE "PersonaFamiliares" f SET "ParentescoId" = p."Id"
                FROM "Parentescos" p
                WHERE f."ParentescoId" IS NULL AND lower(trim(f."Parentesco")) = lower(p."Nombre");
                """);

            migrationBuilder.DropColumn(
                name: "Parentesco",
                table: "PersonaFamiliares");

            migrationBuilder.CreateIndex(
                name: "IX_PersonaFamiliares_ParentescoId",
                table: "PersonaFamiliares",
                column: "ParentescoId");

            migrationBuilder.AddForeignKey(
                name: "FK_PersonaFamiliares_Parentescos_ParentescoId",
                table: "PersonaFamiliares",
                column: "ParentescoId",
                principalTable: "Parentescos",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Vuelve al texto libre con el nombre del catálogo.
            migrationBuilder.AddColumn<string>(
                name: "Parentesco",
                table: "PersonaFamiliares",
                type: "character varying(60)",
                maxLength: 60,
                nullable: false,
                defaultValue: "");

            migrationBuilder.Sql("""
                UPDATE "PersonaFamiliares" f SET "Parentesco" = left(p."Nombre", 60)
                FROM "Parentescos" p WHERE f."ParentescoId" = p."Id";
                """);

            migrationBuilder.DropForeignKey(
                name: "FK_PersonaFamiliares_Parentescos_ParentescoId",
                table: "PersonaFamiliares");

            migrationBuilder.DropIndex(
                name: "IX_PersonaFamiliares_ParentescoId",
                table: "PersonaFamiliares");

            migrationBuilder.DropColumn(
                name: "ParentescoId",
                table: "PersonaFamiliares");

            migrationBuilder.DropTable(
                name: "Parentescos");
        }
    }
}
