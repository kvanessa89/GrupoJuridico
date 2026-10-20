using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GrupoJuridico.Gestion.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class FamiliaresSinWhatsapp : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PersonaFamiliares_Paises_WhatsappPaisId",
                table: "PersonaFamiliares");

            migrationBuilder.DropIndex(
                name: "IX_PersonaFamiliares_WhatsappPaisId",
                table: "PersonaFamiliares");

            migrationBuilder.DropColumn(
                name: "Whatsapp",
                table: "PersonaFamiliares");

            migrationBuilder.DropColumn(
                name: "WhatsappPaisId",
                table: "PersonaFamiliares");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Whatsapp",
                table: "PersonaFamiliares",
                type: "character varying(40)",
                maxLength: 40,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "WhatsappPaisId",
                table: "PersonaFamiliares",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_PersonaFamiliares_WhatsappPaisId",
                table: "PersonaFamiliares",
                column: "WhatsappPaisId");

            migrationBuilder.AddForeignKey(
                name: "FK_PersonaFamiliares_Paises_WhatsappPaisId",
                table: "PersonaFamiliares",
                column: "WhatsappPaisId",
                principalTable: "Paises",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
