using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace GrupoJuridico.Gestion.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PaisesTelefonos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "TelefonoPaisId",
                table: "PersonaFamiliares",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "WhatsappPaisId",
                table: "PersonaFamiliares",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PaisId",
                table: "Numeros",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "Paises",
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
                    table.PrimaryKey("PK_Paises", x => x.Id);
                });

            // Países iniciales; los teléfonos existentes quedan como Costa Rica.
            migrationBuilder.Sql("""
                INSERT INTO "Paises" ("Codigo", "Nombre", "ModificadoEn") VALUES ('+506', 'Costa Rica', now()), ('+1', 'Estados Unidos', now());
                UPDATE "Numeros" SET "PaisId" = (SELECT "Id" FROM "Paises" WHERE "Codigo" = '+506');
                UPDATE "PersonaFamiliares" SET "TelefonoPaisId" = (SELECT "Id" FROM "Paises" WHERE "Codigo" = '+506') WHERE "Telefono" <> '';
                UPDATE "PersonaFamiliares" SET "WhatsappPaisId" = (SELECT "Id" FROM "Paises" WHERE "Codigo" = '+506') WHERE "Whatsapp" <> '';
                """);

            migrationBuilder.CreateIndex(
                name: "IX_PersonaFamiliares_TelefonoPaisId",
                table: "PersonaFamiliares",
                column: "TelefonoPaisId");

            migrationBuilder.CreateIndex(
                name: "IX_PersonaFamiliares_WhatsappPaisId",
                table: "PersonaFamiliares",
                column: "WhatsappPaisId");

            migrationBuilder.CreateIndex(
                name: "IX_Numeros_PaisId",
                table: "Numeros",
                column: "PaisId");

            migrationBuilder.AddForeignKey(
                name: "FK_Numeros_Paises_PaisId",
                table: "Numeros",
                column: "PaisId",
                principalTable: "Paises",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PersonaFamiliares_Paises_TelefonoPaisId",
                table: "PersonaFamiliares",
                column: "TelefonoPaisId",
                principalTable: "Paises",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PersonaFamiliares_Paises_WhatsappPaisId",
                table: "PersonaFamiliares",
                column: "WhatsappPaisId",
                principalTable: "Paises",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Numeros_Paises_PaisId",
                table: "Numeros");

            migrationBuilder.DropForeignKey(
                name: "FK_PersonaFamiliares_Paises_TelefonoPaisId",
                table: "PersonaFamiliares");

            migrationBuilder.DropForeignKey(
                name: "FK_PersonaFamiliares_Paises_WhatsappPaisId",
                table: "PersonaFamiliares");

            migrationBuilder.DropTable(
                name: "Paises");

            migrationBuilder.DropIndex(
                name: "IX_PersonaFamiliares_TelefonoPaisId",
                table: "PersonaFamiliares");

            migrationBuilder.DropIndex(
                name: "IX_PersonaFamiliares_WhatsappPaisId",
                table: "PersonaFamiliares");

            migrationBuilder.DropIndex(
                name: "IX_Numeros_PaisId",
                table: "Numeros");

            migrationBuilder.DropColumn(
                name: "TelefonoPaisId",
                table: "PersonaFamiliares");

            migrationBuilder.DropColumn(
                name: "WhatsappPaisId",
                table: "PersonaFamiliares");

            migrationBuilder.DropColumn(
                name: "PaisId",
                table: "Numeros");
        }
    }
}
