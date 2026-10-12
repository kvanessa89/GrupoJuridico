using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GrupoJuridico.Gestion.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AuditoriaVersionesSeccion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ModificadoEn",
                table: "VersionesSeccion",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "now()");

            migrationBuilder.AddColumn<int>(
                name: "ModificadoPorId",
                table: "VersionesSeccion",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ModificadoEn",
                table: "VersionesSeccion");

            migrationBuilder.DropColumn(
                name: "ModificadoPorId",
                table: "VersionesSeccion");
        }
    }
}
