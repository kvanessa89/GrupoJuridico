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
        "Ana", "Vargas Mora", "1-0884-0219", "01-200137", "", new DateOnly(2026, 10, 1), 1,
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
