using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace GrupoJuridico.Gestion.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Cobros : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "EstadosCuotaPago",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false),
                    ModificadoEn = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModificadoPorId = table.Column<int>(type: "integer", nullable: true),
                    Codigo = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Nombre = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EstadosCuotaPago", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "EstadosCuotaVencimiento",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false),
                    ModificadoEn = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModificadoPorId = table.Column<int>(type: "integer", nullable: true),
                    Codigo = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Nombre = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EstadosCuotaVencimiento", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "EstadosPromesa",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false),
                    ModificadoEn = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModificadoPorId = table.Column<int>(type: "integer", nullable: true),
                    Codigo = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Nombre = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EstadosPromesa", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PlanesPago",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ClienteId = table.Column<int>(type: "integer", nullable: false),
                    MontoTotal = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    MontoCuota = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    CantidadCuotas = table.Column<int>(type: "integer", nullable: false),
                    FechaInicio = table.Column<DateOnly>(type: "date", nullable: false),
                    DiaPago = table.Column<int>(type: "integer", nullable: false),
                    ModificadoEn = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModificadoPorId = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlanesPago", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PlanesPago_Clientes_ClienteId",
                        column: x => x.ClienteId,
                        principalTable: "Clientes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Cuotas",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PlanPagoId = table.Column<int>(type: "integer", nullable: false),
                    NumeroCuota = table.Column<int>(type: "integer", nullable: false),
                    FechaVencimiento = table.Column<DateOnly>(type: "date", nullable: false),
                    MontoCuota = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    EstadoCuotaPagoId = table.Column<int>(type: "integer", nullable: false),
                    EstadoCuotaVencimientoId = table.Column<int>(type: "integer", nullable: false),
                    SinMora = table.Column<bool>(type: "boolean", nullable: true),
                    Version = table.Column<Guid>(type: "uuid", nullable: false),
                    ModificadoEn = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModificadoPorId = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Cuotas", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Cuotas_EstadosCuotaPago_EstadoCuotaPagoId",
                        column: x => x.EstadoCuotaPagoId,
                        principalTable: "EstadosCuotaPago",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Cuotas_EstadosCuotaVencimiento_EstadoCuotaVencimientoId",
                        column: x => x.EstadoCuotaVencimientoId,
                        principalTable: "EstadosCuotaVencimiento",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Cuotas_PlanesPago_PlanPagoId",
                        column: x => x.PlanPagoId,
                        principalTable: "PlanesPago",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Pagos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CuotaId = table.Column<int>(type: "integer", nullable: false),
                    MontoPago = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    FechaPago = table.Column<DateOnly>(type: "date", nullable: false),
                    ModificadoEn = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModificadoPorId = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Pagos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Pagos_Cuotas_CuotaId",
                        column: x => x.CuotaId,
                        principalTable: "Cuotas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PromesasPago",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CuotaId = table.Column<int>(type: "integer", nullable: false),
                    FechaPromesa = table.Column<DateOnly>(type: "date", nullable: false),
                    MontoPrometido = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    MontoPagado = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    EstadoPromesaId = table.Column<int>(type: "integer", nullable: false),
                    ModificadoEn = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModificadoPorId = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PromesasPago", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PromesasPago_Cuotas_CuotaId",
                        column: x => x.CuotaId,
                        principalTable: "Cuotas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PromesasPago_EstadosPromesa_EstadoPromesaId",
                        column: x => x.EstadoPromesaId,
                        principalTable: "EstadosPromesa",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ArchivosPago",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PagoId = table.Column<int>(type: "integer", nullable: false),
                    Nombre = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    TipoContenido = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Tamano = table.Column<long>(type: "bigint", nullable: false),
                    FechaCarga = table.Column<DateOnly>(type: "date", nullable: false),
                    Clave = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ModificadoEn = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModificadoPorId = table.Column<int>(type: "integer", nullable: true)
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

            migrationBuilder.CreateIndex(
                name: "IX_Cuotas_EstadoCuotaPagoId",
                table: "Cuotas",
                column: "EstadoCuotaPagoId");

            migrationBuilder.CreateIndex(
                name: "IX_Cuotas_EstadoCuotaVencimientoId",
                table: "Cuotas",
                column: "EstadoCuotaVencimientoId");

            migrationBuilder.CreateIndex(
                name: "IX_Cuotas_FechaVencimiento",
                table: "Cuotas",
                column: "FechaVencimiento");

            migrationBuilder.CreateIndex(
                name: "IX_Cuotas_PlanPagoId_NumeroCuota",
                table: "Cuotas",
                columns: new[] { "PlanPagoId", "NumeroCuota" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Pagos_CuotaId",
                table: "Pagos",
                column: "CuotaId");

            migrationBuilder.CreateIndex(
                name: "IX_PlanesPago_ClienteId",
                table: "PlanesPago",
                column: "ClienteId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PromesasPago_CuotaId",
                table: "PromesasPago",
                column: "CuotaId");

            migrationBuilder.CreateIndex(
                name: "IX_PromesasPago_EstadoPromesaId",
                table: "PromesasPago",
                column: "EstadoPromesaId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ArchivosPago");

            migrationBuilder.DropTable(
                name: "PromesasPago");

            migrationBuilder.DropTable(
                name: "Pagos");

            migrationBuilder.DropTable(
                name: "EstadosPromesa");

            migrationBuilder.DropTable(
                name: "Cuotas");

            migrationBuilder.DropTable(
                name: "EstadosCuotaPago");

            migrationBuilder.DropTable(
                name: "EstadosCuotaVencimiento");

            migrationBuilder.DropTable(
                name: "PlanesPago");
        }
    }
}
