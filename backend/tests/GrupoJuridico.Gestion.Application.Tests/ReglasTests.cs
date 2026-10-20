using GrupoJuridico.Gestion.Application.Common.Models;
using GrupoJuridico.Gestion.Application.Personas;
using GrupoJuridico.Gestion.Domain.Constants;
using GrupoJuridico.Gestion.Domain.Entities;
using GrupoJuridico.Gestion.Domain.Enums;

namespace GrupoJuridico.Gestion.Application.Tests;

public class PrimaTests
{
    [Theory]
    [InlineData(100000, 0, EstadoPrima.Pendiente, 100000)]
    [InlineData(100000, 40000, EstadoPrima.Incompleta, 60000)]
    [InlineData(100000, 100000, EstadoPrima.Pagada, 0)]
    [InlineData(100000, 150000, EstadoPrima.Pagada, 0)]
    public void Recalcular_deriva_estado_y_saldo_de_los_montos(decimal monto, decimal cancelado, int estado, decimal saldo)
    {
        var prima = new Prima { Monto = monto, MontoCancelado = cancelado };
        prima.Recalcular();
        Assert.Equal(estado, prima.EstadoPrimaId);
        Assert.Equal(saldo, prima.SaldoPendiente);
    }
}

public class PersonaTests
{
    [Fact]
    public void NumeroPrincipal_usa_el_marcado_o_el_primero_del_tipo()
    {
        var p = new Persona();
        p.Numeros.Add(new Numero { Id = 1, Valor = "1111", Tipo = TipoNumero.Telefono });
        p.Numeros.Add(new Numero { Id = 2, Valor = "2222", Tipo = TipoNumero.Telefono, Principal = true });
        p.Numeros.Add(new Numero { Id = 3, Valor = "3333", Tipo = TipoNumero.WhatsApp });

        Assert.Equal("2222", p.NumeroPrincipal(TipoNumero.Telefono)!.Valor);
        Assert.Equal("3333", p.NumeroPrincipal(TipoNumero.WhatsApp)!.Valor);
    }

    [Fact]
    public void Vendedor_se_muestra_como_asesor_codigo_nombre()
    {
        Assert.Equal("Asesor 30 - Marta", new Vendedor { Codigo = "30", Nombre = "Marta" }.Etiqueta);
    }
}

public class CrearProspectoValidatorTests
{
    private static CrearProspectoRequest Valido() => new(
        "Ana", "Vargas Mora", "1-0884-0219", "01-20013", "", new DateOnly(2026, 10, 1), 1,
        "8812-4477", "", "", 1, 2, 900000, "", 225000, 0, new DateOnly(2026, 10, 15), null, null);

    [Fact]
    public void Prospecto_completo_es_valido()
    {
        Assert.True(new CrearProspectoValidator().Validate(Valido()).IsValid);
    }

    [Fact]
    public void Requiere_al_menos_telefono_o_whatsapp()
    {
        var r = new CrearProspectoValidator().Validate(Valido() with { TelefonoPrincipal = " ", WhatsappPrincipal = "" });
        Assert.Contains(r.Errors, e => e.ErrorMessage == CrearProspectoValidator.MensajeContacto);
    }

    [Fact]
    public void Requiere_vendedor_procedencia_metodo_y_montos()
    {
        var r = new CrearProspectoValidator().Validate(Valido() with { VendedorId = null, ProcedenciaVentaId = 0, MetodoVentaId = null, MontoVenta = 0, MontoPrima = 0 });
        var campos = r.Errors.Select(e => e.PropertyName).ToHashSet();
        Assert.Superset(new HashSet<string> { "VendedorId", "ProcedenciaVentaId", "MetodoVentaId", "MontoVenta", "MontoPrima" }, campos);
    }

    [Fact]
    public void Prima_pagada_requiere_fecha_de_pago()
    {
        var r = new CrearProspectoValidator().Validate(Valido() with { MontoCancelado = 225000 });
        Assert.Contains(r.Errors, e => e.PropertyName == "FechaPago");
    }
}

public class ActualizarPrimaValidatorTests
{
    [Fact]
    public void Prima_pagada_requiere_fecha_de_pago()
    {
        var r = new ActualizarPrimaValidator().Validate(new ActualizarPrimaRequest(225000, 225000, new DateOnly(2026, 10, 15), null));
        Assert.Contains(r.Errors, e => e.PropertyName == "FechaPago");
    }

    [Fact]
    public void Prima_pagada_con_fecha_de_pago_es_valida()
    {
        var r = new ActualizarPrimaValidator().Validate(new ActualizarPrimaRequest(225000, 225000, null, new DateOnly(2026, 10, 12)));
        Assert.True(r.IsValid);
    }

    [Fact]
    public void Prima_incompleta_no_requiere_fecha_de_pago()
    {
        var r = new ActualizarPrimaValidator().Validate(new ActualizarPrimaRequest(225000, 100000, null, null));
        Assert.True(r.IsValid);
    }
}

public class RolesTests
{
    [Theory]
    [InlineData(Roles.Administrador, false, true)]
    [InlineData(Roles.AsistenteVentas, true, false)]
    [InlineData(Roles.Cobros, true, true)]
    public void Reglas_fijas_por_rol(string rol, bool ocultaPagadas, bool veClientes)
    {
        Assert.Equal(ocultaPagadas, Roles.OcultaPrimasPagadas(rol));
        Assert.Equal(veClientes, Roles.VeClientes(rol));
    }
}

public class PaginadoTests
{
    [Fact]
    public void Ajusta_la_pagina_al_rango_valido()
    {
        var p = Paginado<int>.Crear(Enumerable.Range(1, 25).ToList(), pagina: 9, porPagina: 10);
        Assert.Equal(3, p.Pagina);
        Assert.Equal(new[] { 21, 22, 23, 24, 25 }, p.Filas);
        Assert.Equal(25, p.Total);
    }
}


public class EdicionRequeridosTests
{
    [Theory]
    [InlineData(null, 1, 100)]
    [InlineData(1, null, 100)]
    [InlineData(1, 1, 0)]
    [InlineData(1, 1, -1)]
    public void Venta_requiere_catalogos_y_monto_positivo(int? procedencia, int? metodo, decimal monto)
    {
        Assert.False(new ActualizarVentaValidator().Validate(new ActualizarVentaRequest(procedencia, metodo, monto, "")).IsValid);
    }

    [Theory]
    [InlineData(0, 0, false)]
    [InlineData(-1, 0, false)]
    [InlineData(100, -1, false)]
    [InlineData(100, 0, true)]
    public void Prima_requiere_monto_positivo_y_cancelado_admite_cero(decimal monto, decimal cancelado, bool valido)
    {
        Assert.Equal(valido, new ActualizarPrimaValidator().Validate(new ActualizarPrimaRequest(monto, cancelado, null, null)).IsValid);
    }
}


public class FormatosPersonaTests
{
    private static CrearProspectoRequest Nuevo() => new(
        "Ana", "Vargas", "123", "12-12345", "", new DateOnly(2026, 10, 13), 1,
        "88887777", "", "", 1, 1, 1000m, "", 100m, 0m, null, null, null);
    private static ActualizarDatosRequest Datos() => new(
        "Ana", "Vargas", "123", "12-12345", "", new DateOnly(2026, 10, 13), 1,
        "88887777", "", "", null, null);

    [Theory]
    [InlineData("12-12345", true)]
    [InlineData("01-00001", true)]
    [InlineData("1-12345", false)]
    [InlineData("12-123456", false)]
    [InlineData("12-ABCDE", false)]
    [InlineData("1212345", false)]
    [InlineData("", false)]
    public void Finca_exige_dos_y_cinco_digitos(string finca, bool valido)
    {
        Assert.Equal(valido, new CrearProspectoValidator().Validate(Nuevo() with { Finca = finca }).IsValid);
        Assert.Equal(valido, new ActualizarDatosValidator().Validate(Datos() with { Finca = finca }).IsValid);
    }

    [Theory]
    [InlineData(null, true)]
    [InlineData("", true)]
    [InlineData("12-123456-1234-AB", true)]
    [InlineData("12-123456-1234-ab", true)]
    [InlineData("1-123456-1234-AB", false)]
    [InlineData("12-12345-1234-AB", false)]
    [InlineData("12-123456-123-AB", false)]
    [InlineData("12-123456-1234-A1", false)]
    [InlineData("12-123456-1234-ABC", false)]
    public void Expediente_opcional_respeta_formato_en_creacion_edicion_y_conversion(string? expediente, bool valido)
    {
        Assert.Equal(valido, new CrearProspectoValidator().Validate(Nuevo() with { Expediente = expediente }).IsValid);
        Assert.Equal(valido, new ActualizarDatosValidator().Validate(Datos() with { Expediente = expediente! }).IsValid);
        Assert.Equal(valido, new ConvertirClienteValidator().Validate(new ConvertirClienteRequest(expediente, 1, null)).IsValid);
    }

    [Theory]
    [InlineData(0, true)]
    [InlineData(1000, true)]
    [InlineData(1001, false)]
    public void Notas_de_venta_admiten_hasta_mil_caracteres(int cantidad, bool valido)
    {
        var notas = new string('x', cantidad);
        Assert.Equal(valido, new CrearProspectoValidator().Validate(Nuevo() with { NotasVenta = notas }).IsValid);
        Assert.Equal(valido, new ActualizarVentaValidator().Validate(new ActualizarVentaRequest(1, 1, 1000m, notas)).IsValid);
    }
}


public class RutasValidacionTests
{
    [Theory]
    [InlineData("Familiares[2].CorreoElectronico", "familiares[2].correoElectronico")]
    [InlineData("Numeros[1].PaisId", "numeros[1].paisId")]
    [InlineData("Nombre", "nombre")]
    public void Normaliza_cada_segmento(string ruta, string esperado) =>
        Assert.Equal(esperado, GrupoJuridico.Gestion.Application.Common.Validacion.CamelCase(ruta));

    [Fact]
    public async Task Familiar_invalido_identifica_fila_y_campos()
    {
        var error = await Assert.ThrowsAsync<GrupoJuridico.Gestion.Application.Common.Exceptions.ValidacionException>(() =>
            GrupoJuridico.Gestion.Application.Common.Validacion.ValidarAsync(new FamiliarItemValidator(),
                new FamiliarItem(0, "", null, "", ""), prefijo: "familiares[2]"));
        Assert.Contains("familiares[2].nombreCompleto", error.Errores.Keys);
        Assert.Contains("familiares[2].telefono", error.Errores.Keys);
        Assert.Contains("familiares[2].correoElectronico", error.Errores.Keys);
    }
}
