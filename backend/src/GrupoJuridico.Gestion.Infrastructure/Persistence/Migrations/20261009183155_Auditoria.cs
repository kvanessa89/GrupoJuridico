using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GrupoJuridico.Gestion.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Auditoria : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ModificadoEn",
                table: "Ventas",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "now()");

            migrationBuilder.AddColumn<int>(
                name: "ModificadoPorId",
                table: "Ventas",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ModificadoEn",
                table: "Vendedores",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "now()");

            migrationBuilder.AddColumn<int>(
                name: "ModificadoPorId",
                table: "Vendedores",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ModificadoEn",
                table: "ProcedenciasVenta",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "now()");

            migrationBuilder.AddColumn<int>(
                name: "ModificadoPorId",
                table: "ProcedenciasVenta",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ModificadoEn",
                table: "Primas",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "now()");

            migrationBuilder.AddColumn<int>(
                name: "ModificadoPorId",
                table: "Primas",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ModificadoEn",
                table: "Personas",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "now()");

            migrationBuilder.AddColumn<int>(
                name: "ModificadoPorId",
                table: "Personas",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ModificadoEn",
                table: "PersonaFamiliares",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "now()");

            migrationBuilder.AddColumn<int>(
                name: "ModificadoPorId",
                table: "PersonaFamiliares",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ModificadoEn",
                table: "OrigenesCliente",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "now()");

            migrationBuilder.AddColumn<int>(
                name: "ModificadoPorId",
                table: "OrigenesCliente",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ModificadoEn",
                table: "Numeros",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "now()");

            migrationBuilder.AddColumn<int>(
                name: "ModificadoPorId",
                table: "Numeros",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ModificadoEn",
                table: "MetodosVenta",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "now()");

            migrationBuilder.AddColumn<int>(
                name: "ModificadoPorId",
                table: "MetodosVenta",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ModificadoEn",
                table: "Fincas",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "now()");

            migrationBuilder.AddColumn<int>(
                name: "ModificadoPorId",
                table: "Fincas",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ModificadoEn",
                table: "EstadosPrima",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "now()");

            migrationBuilder.AddColumn<int>(
                name: "ModificadoPorId",
                table: "EstadosPrima",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ModificadoEn",
                table: "EstadosCliente",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "now()");

            migrationBuilder.AddColumn<int>(
                name: "ModificadoPorId",
                table: "EstadosCliente",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ModificadoEn",
                table: "CorreosElectronicos",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "now()");

            migrationBuilder.AddColumn<int>(
                name: "ModificadoPorId",
                table: "CorreosElectronicos",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ModificadoEn",
                table: "ConfiguracionSistema",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "now()");

            migrationBuilder.AddColumn<int>(
                name: "ModificadoPorId",
                table: "ConfiguracionSistema",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ModificadoEn",
                table: "Comentarios",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "now()");

            migrationBuilder.AddColumn<int>(
                name: "ModificadoPorId",
                table: "Comentarios",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ModificadoEn",
                table: "Clientes",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "now()");

            migrationBuilder.AddColumn<int>(
                name: "ModificadoPorId",
                table: "Clientes",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ModificadoEn",
                table: "AspNetUsers",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "now()");

            migrationBuilder.AddColumn<int>(
                name: "ModificadoPorId",
                table: "AspNetUsers",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ModificadoEn",
                table: "Ventas");

            migrationBuilder.DropColumn(
                name: "ModificadoPorId",
                table: "Ventas");

            migrationBuilder.DropColumn(
                name: "ModificadoEn",
                table: "Vendedores");

            migrationBuilder.DropColumn(
                name: "ModificadoPorId",
                table: "Vendedores");

            migrationBuilder.DropColumn(
                name: "ModificadoEn",
                table: "ProcedenciasVenta");

            migrationBuilder.DropColumn(
                name: "ModificadoPorId",
                table: "ProcedenciasVenta");

            migrationBuilder.DropColumn(
                name: "ModificadoEn",
                table: "Primas");

            migrationBuilder.DropColumn(
                name: "ModificadoPorId",
                table: "Primas");

            migrationBuilder.DropColumn(
                name: "ModificadoEn",
                table: "Personas");

            migrationBuilder.DropColumn(
                name: "ModificadoPorId",
                table: "Personas");

            migrationBuilder.DropColumn(
                name: "ModificadoEn",
                table: "PersonaFamiliares");

            migrationBuilder.DropColumn(
                name: "ModificadoPorId",
                table: "PersonaFamiliares");

            migrationBuilder.DropColumn(
                name: "ModificadoEn",
                table: "OrigenesCliente");

            migrationBuilder.DropColumn(
                name: "ModificadoPorId",
                table: "OrigenesCliente");

            migrationBuilder.DropColumn(
                name: "ModificadoEn",
                table: "Numeros");

            migrationBuilder.DropColumn(
                name: "ModificadoPorId",
                table: "Numeros");

            migrationBuilder.DropColumn(
                name: "ModificadoEn",
                table: "MetodosVenta");

            migrationBuilder.DropColumn(
                name: "ModificadoPorId",
                table: "MetodosVenta");

            migrationBuilder.DropColumn(
                name: "ModificadoEn",
                table: "Fincas");

            migrationBuilder.DropColumn(
                name: "ModificadoPorId",
                table: "Fincas");

            migrationBuilder.DropColumn(
                name: "ModificadoEn",
                table: "EstadosPrima");

            migrationBuilder.DropColumn(
                name: "ModificadoPorId",
                table: "EstadosPrima");

            migrationBuilder.DropColumn(
                name: "ModificadoEn",
                table: "EstadosCliente");

            migrationBuilder.DropColumn(
                name: "ModificadoPorId",
                table: "EstadosCliente");

            migrationBuilder.DropColumn(
                name: "ModificadoEn",
                table: "CorreosElectronicos");

            migrationBuilder.DropColumn(
                name: "ModificadoPorId",
                table: "CorreosElectronicos");

            migrationBuilder.DropColumn(
                name: "ModificadoEn",
                table: "ConfiguracionSistema");

            migrationBuilder.DropColumn(
                name: "ModificadoPorId",
                table: "ConfiguracionSistema");

            migrationBuilder.DropColumn(
                name: "ModificadoEn",
                table: "Comentarios");

            migrationBuilder.DropColumn(
                name: "ModificadoPorId",
                table: "Comentarios");

            migrationBuilder.DropColumn(
                name: "ModificadoEn",
                table: "Clientes");

            migrationBuilder.DropColumn(
                name: "ModificadoPorId",
                table: "Clientes");

            migrationBuilder.DropColumn(
                name: "ModificadoEn",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "ModificadoPorId",
                table: "AspNetUsers");
        }
    }
}
