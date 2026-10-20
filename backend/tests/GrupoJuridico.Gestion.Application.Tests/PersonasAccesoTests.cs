using GrupoJuridico.Gestion.Application.Cobros;
using GrupoJuridico.Gestion.Application.Common.Exceptions;
using GrupoJuridico.Gestion.Application.Common.Interfaces;
using GrupoJuridico.Gestion.Application.Personas;
using GrupoJuridico.Gestion.Application.Usuarios;
using GrupoJuridico.Gestion.Domain.Constants;
using GrupoJuridico.Gestion.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.Sqlite;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Json;
using GrupoJuridico.Gestion.Api.Controllers;
using GrupoJuridico.Gestion.Api.Infrastructure;
using GrupoJuridico.Gestion.Infrastructure.Persistence;

namespace GrupoJuridico.Gestion.Application.Tests;

public class PersonasAccesoTests
{
    [Theory]
    [InlineData(Roles.AsistenteVentas, EstadoPrima.Pendiente)]
    [InlineData(Roles.AsistenteVentas, EstadoPrima.Incompleta)]
    [InlineData(Roles.Administrador, EstadoPrima.Pagada)]
    [InlineData(Roles.Cobros, EstadoPrima.Pagada)]
    public async Task Obtener_permite_los_roles_y_estados_autorizados(string rol, int estado)
    {
        using var db = CrearDb();
        var id = await SembrarAsync(db, estado);
        var dto = await CrearServicio(db, rol).ObtenerAsync(id);
        Assert.Equal(id, dto.Id);
        Assert.Equal(1000m, dto.Venta!.Monto);
    }

    [Fact]
    public async Task Asistente_no_puede_consultar_prima_pagada_por_id()
    {
        using var db = CrearDb();
        var id = await SembrarAsync(db, EstadoPrima.Pagada);
        await Assert.ThrowsAsync<ProhibidoException>(() =>
            CrearServicio(db, Roles.AsistenteVentas).ObtenerAsync(id));
    }

    [Theory]
    [InlineData("datos")]
    [InlineData("venta")]
    [InlineData("prima")]
    [InlineData("numeros")]
    [InlineData("correos")]
    [InlineData("familiares")]
    [InlineData("convertir")]
    [InlineData("comentar")]
    [InlineData("eliminar-comentario")]
    public async Task Asistente_no_puede_modificar_persona_pagada(string operacion)
    {
        using var db = CrearDb();
        var id = await SembrarAsync(db, EstadoPrima.Pagada);
        var comentarioId = await db.Comentarios.Select(c => c.Id).SingleAsync();
        var servicio = CrearServicio(db, Roles.AsistenteVentas);
        var guardados = db.Guardados;

        Task Ejecutar() => operacion switch
        {
            "datos" => servicio.ActualizarDatosAsync(id, new ActualizarDatosRequest(
                "Otro", "Nombre", "123", "01-20013", "", new DateOnly(2026, 10, 9),
                null, null, null, null, null, null)),
            "venta" => servicio.ActualizarVentaAsync(id, new ActualizarVentaRequest(null, null, 2000m, "Cambio")),
            "prima" => servicio.ActualizarPrimaAsync(id, new ActualizarPrimaRequest(100m, 0m, null, null)),
            "numeros" => servicio.ReemplazarNumerosAsync(id, new[] { new NumeroItem(0, "99999999", "Teléfono", true) }),
            "correos" => servicio.ReemplazarCorreosAsync(id, Array.Empty<CorreoItem>()),
            "familiares" => servicio.ReemplazarFamiliaresAsync(id, Array.Empty<FamiliarItem>()),
            "convertir" => servicio.ConvertirEnClienteAsync(id, new ConvertirClienteRequest("", 1, null)),
            "comentar" => servicio.ComentarAsync(id, new ComentarioRequest("Nuevo")),
            "eliminar-comentario" => servicio.EliminarComentarioAsync(comentarioId),
            _ => throw new InvalidOperationException()
        };

        await Assert.ThrowsAsync<ProhibidoException>(Ejecutar);
        Assert.Equal(guardados, db.Guardados);
        Assert.Single(await db.Comentarios.ToListAsync());
        Assert.Equal("Ana", (await db.Personas.FindAsync(id))!.Nombres);
        Assert.Equal(100m, (await db.Primas.SingleAsync()).MontoCancelado);
    }

    [Fact]
    public async Task Asistente_puede_completar_pago_pero_no_reabrir_ni_revertirlo()
    {
        using var db = CrearDb();
        var id = await SembrarAsync(db, EstadoPrima.Incompleta);
        var servicio = CrearServicio(db, Roles.AsistenteVentas);
        var dto = await servicio.ActualizarPrimaAsync(id,
            new ActualizarPrimaRequest(100m, 100m, null, new DateOnly(2026, 10, 9)),
            version: (await servicio.ObtenerAsync(id)).Versiones["prima"]);

        Assert.Equal(EstadoPrima.Pagada, dto.EstadoPrimaId);
        Assert.Equal(0m, dto.SaldoPendiente);
        await Assert.ThrowsAsync<ProhibidoException>(() => servicio.ObtenerAsync(id));
        await Assert.ThrowsAsync<ProhibidoException>(() => servicio.ActualizarPrimaAsync(id,
            new ActualizarPrimaRequest(100m, 0m, null, null)));
    }

    [Fact]
    public async Task Crear_con_prima_pagada_devuelve_exito_sin_permitir_consultas_posteriores()
    {
        using var db = CrearDb();
        db.Paises.Add(new Pais { Codigo = "+506", Nombre = "Costa Rica" });
        var vendedor = new Vendedor { Codigo = "30", Nombre = "Marta" };
        var procedencia = new ProcedenciaVenta { Nombre = "Feria" };
        var metodo = new MetodoVenta { Nombre = "Llamada" };
        db.AddRange(vendedor, procedencia, metodo);
        await db.SaveChangesAsync();
        var servicio = CrearServicio(db, Roles.AsistenteVentas);
        var dto = await servicio.CrearProspectoAsync(new CrearProspectoRequest(
            "Ana", "Vargas", "123", "01-20013", "", new DateOnly(2026, 10, 9), vendedor.Id,
            "88888888", "", "", procedencia.Id, metodo.Id, 1000m, "", 100m, 100m,
            null, new DateOnly(2026, 10, 9), null));

        Assert.True(dto.Id > 0);
        Assert.Equal(EstadoPrima.Pagada, dto.Prima!.EstadoPrimaId);
        await Assert.ThrowsAsync<ProhibidoException>(() => servicio.ObtenerAsync(dto.Id));
    }

    [Fact]
    public async Task Eliminar_comentario_sigue_exigiendo_autoria()
    {
        using var db = CrearDb();
        await SembrarAsync(db, EstadoPrima.Pendiente);
        var c = await db.Comentarios.SingleAsync();
        c.UsuarioId = 99;
        await db.SaveChangesAsync();
        await Assert.ThrowsAsync<ProhibidoException>(() =>
            CrearServicio(db, Roles.Administrador).EliminarComentarioAsync(c.Id));
    }

    [Fact]
    public async Task Nombres_y_apellidos_admiten_veinte_caracteres()
    {
        using var db = CrearDb();
        var id = await SembrarAsync(db, EstadoPrima.Pendiente);
        var servicio = CrearServicio(db, Roles.Administrador);
        ActualizarDatosRequest Datos(string nombres, string apellidos) => new(
            nombres, apellidos, "123", "01-20013", "", new DateOnly(2026, 10, 9), 1, "88888888", null, null, null, null);

        var veinte = new string('a', 20);
        await servicio.ActualizarDatosAsync(id, Datos(veinte, veinte), version: (await servicio.ObtenerAsync(id)).Versiones["datos"]);
        var version = (await servicio.ObtenerAsync(id)).Versiones["datos"];
        var guardados = db.Guardados;
        await Assert.ThrowsAsync<ValidacionException>(() => servicio.ActualizarDatosAsync(id, Datos(veinte + "b", "Vargas"), version: version));
        await Assert.ThrowsAsync<ValidacionException>(() => servicio.ActualizarDatosAsync(id, Datos("Ana", veinte + "b"), version: version));
        Assert.Equal(guardados, db.Guardados);

        var largo = new CrearProspectoValidator().Validate(
            new CrearProspectoRequest(veinte + "b", "Vargas", "123", "01-20013", "", new DateOnly(2026, 10, 9), 1,
                "88888888", "", "", 1, 1, 1000m, "", 100m, 0m, null, null, null));
        Assert.Contains(largo.Errors, e => e.PropertyName == nameof(CrearProspectoRequest.Nombres));
    }

    [Fact]
    public async Task Cedula_admite_quince_digitos_y_los_correos_deben_ser_validos()
    {
        using var db = CrearDb();
        var id = await SembrarAsync(db, EstadoPrima.Pendiente);
        var servicio = CrearServicio(db, Roles.Administrador);
        ActualizarDatosRequest Datos(string cedula, string? correo) => new(
            "Ana", "Vargas", cedula, "01-20013", "", new DateOnly(2026, 10, 9), 1, "88888888", null, correo, null, null);
        var ficha = await servicio.ObtenerAsync(id);
        var guardados = db.Guardados;

        await Assert.ThrowsAsync<ValidacionException>(() => servicio.ActualizarDatosAsync(id, Datos(new string('1', 16), null), version: ficha.Versiones["datos"]));
        await Assert.ThrowsAsync<ValidacionException>(() => servicio.ActualizarDatosAsync(id, Datos("1", "ana@correo"), version: ficha.Versiones["datos"]));
        await Assert.ThrowsAsync<ValidacionException>(() => servicio.ActualizarDatosAsync(id,
            Datos("1", new string('a', 92) + "@mail.com"), version: ficha.Versiones["datos"])); // 101 caracteres
        await Assert.ThrowsAsync<ValidacionException>(() => servicio.ReemplazarCorreosAsync(id,
            new[] { new CorreoItem(0, "sin arroba", true) }, version: ficha.Versiones["datos"]));
        await Assert.ThrowsAsync<ValidacionException>(() => servicio.ReemplazarFamiliaresAsync(id,
            new[] { new FamiliarItem(0, "Luis", null, "", "luis@", "") }, version: ficha.Versiones["familiares"]));
        Assert.Equal(guardados, db.Guardados);

        await servicio.ActualizarDatosAsync(id, Datos(new string('1', 15), " ana@correo.cr "), version: ficha.Versiones["datos"]);
        var actual = await servicio.ObtenerAsync(id);
        Assert.Equal(new string('1', 15), actual.Cedula);
        Assert.Equal("ana@correo.cr", actual.CorreoPrincipal);
    }

    [Fact]
    public async Task Familiares_usan_parentescos_del_catalogo_y_no_se_eliminan_en_uso()
    {
        using var db = CrearDb();
        var id = await SembrarAsync(db, EstadoPrima.Pendiente);
        var servicio = CrearServicio(db, Roles.Administrador);
        var catalogos = new Catalogos.CatalogosService(db, new IdentityPrueba(), new ActualizadorCobros(db, new FechaPrueba()));
        var hermano = await catalogos.CrearAsync("parentescos", new Catalogos.GuardarCatalogoItemRequest("Hermano/a", null, null));
        Assert.StartsWith("PA-", hermano.Codigo);
        var version = (await servicio.ObtenerAsync(id)).Versiones["familiares"];

        await Assert.ThrowsAsync<ValidacionException>(() => servicio.ReemplazarFamiliaresAsync(id,
            new[] { new FamiliarItem(0, "Luis", 999, "", "", "") }, version: version));
        var familiares = await servicio.ReemplazarFamiliaresAsync(id,
            new[] { new FamiliarItem(0, "Luis", hermano.Id, "88887777", "", "") }, version: version);
        Assert.Equal(hermano.Id, familiares[0].ParentescoId);

        await Assert.ThrowsAsync<ValidacionException>(() => catalogos.EliminarAsync("parentescos", hermano.Id));

        // El nombre y un medio de contacto son requeridos.
        var conNombre = (await servicio.ObtenerAsync(id)).Versiones["familiares"];
        await Assert.ThrowsAsync<ValidacionException>(() => servicio.ReemplazarFamiliaresAsync(id,
            new[] { new FamiliarItem(0, "  ", hermano.Id, "88887777", "", "") }, version: conNombre));
        // Una fila agregada vacía debe completarse o quitarse, no guardarse silenciosamente.
        await Assert.ThrowsAsync<ValidacionException>(() => servicio.ReemplazarFamiliaresAsync(id, new[]
        {
            new FamiliarItem(familiares[0].Id, "Luis", hermano.Id, "88887777", "", ""),
            new FamiliarItem(-1, "", null, "", "", ""),
        }, version: conNombre));

        // Nombre completo del familiar: hasta 60 caracteres.
        var nueva = (await servicio.ObtenerAsync(id)).Versiones["familiares"];
        await Assert.ThrowsAsync<ValidacionException>(() => servicio.ReemplazarFamiliaresAsync(id,
            new[] { new FamiliarItem(0, new string('a', 61), hermano.Id, "", "", "") }, version: nueva));
        await servicio.ReemplazarFamiliaresAsync(id,
            new[] { new FamiliarItem(0, new string('a', 60), hermano.Id, "88887777", "", "") }, version: nueva);
        Assert.NotNull(await db.Parentescos.FindAsync(hermano.Id));
    }

    [Fact]
    public async Task Prima_pagada_no_se_guarda_sin_fecha_de_pago()
    {
        using var db = CrearDb();
        var id = await SembrarAsync(db, EstadoPrima.Incompleta);
        var servicio = CrearServicio(db, Roles.Administrador);
        var version = (await servicio.ObtenerAsync(id)).Versiones["prima"];
        var guardados = db.Guardados;

        await Assert.ThrowsAsync<ValidacionException>(() =>
            servicio.ActualizarPrimaAsync(id, new ActualizarPrimaRequest(100m, 100m, null, null), version: version));
        Assert.Equal(guardados, db.Guardados);
        Assert.Equal(EstadoPrima.Incompleta, (await db.Primas.SingleAsync()).EstadoPrimaId);

        var pagada = await servicio.ActualizarPrimaAsync(id,
            new ActualizarPrimaRequest(100m, 100m, null, new DateOnly(2026, 10, 9)), version: version);
        Assert.Equal(EstadoPrima.Pagada, pagada.EstadoPrimaId);
    }

    [Fact]
    public async Task Permiso_se_revisa_antes_que_los_catalogos()
    {
        using var db = CrearDb();
        var id = await SembrarAsync(db, EstadoPrima.Pagada);
        var servicio = CrearServicio(db, Roles.AsistenteVentas);

        // Ids que no existen en ningún catálogo: aun así, el error es de permiso.
        await Assert.ThrowsAsync<ProhibidoException>(() => servicio.ActualizarDatosAsync(id, new ActualizarDatosRequest(
            "Ana", "Vargas", "123", "01-20013", "", new DateOnly(2026, 10, 9), 999, null, null, null, null, null)));
        await Assert.ThrowsAsync<ProhibidoException>(() => servicio.ActualizarVentaAsync(id,
            new ActualizarVentaRequest(999, 999, 1000m, "")));
        await Assert.ThrowsAsync<ProhibidoException>(() => servicio.ConvertirEnClienteAsync(id,
            new ConvertirClienteRequest("", 999, 999)));
    }

    [Fact]
    public async Task Catalogo_eliminado_con_la_ficha_abierta_da_error_de_validacion()
    {
        using var db = CrearDb();
        var id = await SembrarAsync(db, EstadoPrima.Pendiente);
        var servicio = CrearServicio(db, Roles.Administrador);
        var vendedor = new Vendedor { Codigo = "30", Nombre = "Marta" };
        var procedencia = new ProcedenciaVenta { Nombre = "Feria" };
        var metodo = new MetodoVenta { Nombre = "Llamada" };
        var origen = new OrigenCliente { Codigo = "P1", Nombre = "Referido" };
        var origenEliminado = new OrigenCliente { Codigo = "P2", Nombre = "Feria" };
        var estado = new EstadoCliente { Nombre = "Moroso" };
        db.AddRange(vendedor, procedencia, metodo, origen, origenEliminado, estado);
        await db.SaveChangesAsync();
        var ficha = await servicio.ObtenerAsync(id);

        // Otra sesión elimina los catálogos mientras la ficha sigue mostrando esas opciones.
        db.RemoveRange(vendedor, procedencia, metodo, origenEliminado, estado);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        var guardados = db.Guardados;

        var datos = await Assert.ThrowsAsync<ValidacionException>(() => servicio.ActualizarDatosAsync(id, new ActualizarDatosRequest(
            "Ana", "Vargas", "123", "01-20013", "", new DateOnly(2026, 10, 9), vendedor.Id, "88888888", null, null, null, null),
            version: ficha.Versiones["datos"]));
        Assert.Contains("vendedorId", datos.Errores.Keys);
        await Assert.ThrowsAsync<ValidacionException>(() => servicio.ActualizarVentaAsync(id,
            new ActualizarVentaRequest(procedencia.Id, null, 1000m, ""), version: ficha.Versiones["venta"]));
        await Assert.ThrowsAsync<ValidacionException>(() => servicio.ActualizarVentaAsync(id,
            new ActualizarVentaRequest(null, metodo.Id, 1000m, ""), version: ficha.Versiones["venta"]));

        // Convertir en cliente: un origen o un estado eliminado se rechaza (el estado ya no cae en "Al día").
        var sinOrigen = await Assert.ThrowsAsync<ValidacionException>(() => servicio.ConvertirEnClienteAsync(id,
            new ConvertirClienteRequest("", origenEliminado.Id, null), version: ficha.Versiones["datos"]));
        Assert.Contains("ya no existe", sinOrigen.Errores["origenClienteId"][0]);
        var sinEstado = await Assert.ThrowsAsync<ValidacionException>(() => servicio.ConvertirEnClienteAsync(id,
            new ConvertirClienteRequest("", origen.Id, estado.Id), version: ficha.Versiones["datos"]));
        Assert.Contains("estadoClienteId", sinEstado.Errores.Keys);
        Assert.Equal(guardados, db.Guardados);
    }

    [Fact]
    public async Task Edicion_sin_version_no_guarda()
    {
        using var db = CrearDb();
        var id = await SembrarAsync(db, EstadoPrima.Pendiente);
        var servicio = CrearServicio(db, Roles.Administrador);
        Assert.Equal(4, (await servicio.ObtenerAsync(id)).Versiones.Count);
        var guardados = db.Guardados;
        await Assert.ThrowsAsync<VersionRequeridaException>(() =>
            servicio.ReemplazarNumerosAsync(id, new[] { new NumeroItem(0, "99999999", "Teléfono", true) }));
        Assert.Equal(guardados, db.Guardados);
    }

    [Fact]
    public async Task Version_desactualizada_rechaza_el_reemplazo_de_contactos()
    {
        using var db = CrearDb();
        var id = await SembrarAsync(db, EstadoPrima.Pendiente);
        var servicio = CrearServicio(db, Roles.Administrador);
        var version = (await servicio.ObtenerAsync(id)).Versiones["datos"];
        await servicio.ReemplazarNumerosAsync(id, new[] { new NumeroItem(0, "88888888", "Teléfono", true) }, version: version);
        await Assert.ThrowsAsync<ConflictoVersionException>(() =>
            servicio.ReemplazarNumerosAsync(id, new[] { new NumeroItem(0, "99999999", "Teléfono", true) }, version: version));
        Assert.Single(await db.Numeros.ToListAsync());
    }

    [Fact]
    public async Task Telefonos_guardan_pais_y_sin_pais_toman_el_primero()
    {
        using var db = CrearDb();
        var id = await SembrarAsync(db, EstadoPrima.Pendiente);
        var eeuu = new Pais { Codigo = "+1", Nombre = "Estados Unidos" };
        db.Paises.Add(eeuu);
        await db.SaveChangesAsync();
        var cr = await db.Paises.Where(p => p.Codigo == "+506").Select(p => p.Id).SingleAsync();
        var servicio = CrearServicio(db, Roles.Administrador);
        var ficha = await servicio.ObtenerAsync(id);

        var numeros = await servicio.ReemplazarNumerosAsync(id, new[]
        {
            new NumeroItem(0, "3055550100", "WhatsApp", true, eeuu.Id),
            new NumeroItem(0, "8888-88 88", "Teléfono", true),
        }, version: ficha.Versiones["datos"]);
        Assert.Equal(new[] { eeuu.Id, cr }, numeros.Select(n => n.PaisId));
        Assert.Equal("88888888", numeros[1].Numero); // solo dígitos

        var familiares = await servicio.ReemplazarFamiliaresAsync(id, new[]
        {
            new FamiliarItem(0, "Luis", null, "70001111", "", "", null, eeuu.Id),
        }, version: ficha.Versiones["familiares"]);
        Assert.Equal(cr, familiares[0].TelefonoPaisId);
        Assert.Null(familiares[0].WhatsappPaisId); // sin número no se guarda país

        var detalle = await servicio.ObtenerAsync(id);
        Assert.Equal(eeuu.Id, detalle.WhatsappPrincipalPaisId);
        await Assert.ThrowsAsync<ValidacionException>(() => servicio.ReemplazarNumerosAsync(id,
            new[] { new NumeroItem(0, "1", "Teléfono", true, 999) }, version: detalle.Versiones["datos"]));
    }

    [Fact]
    public async Task Pais_valida_el_prefijo_y_no_se_elimina_si_tiene_telefonos()
    {
        using var db = CrearDb();
        var id = await SembrarAsync(db, EstadoPrima.Pendiente);
        var servicio = CrearServicio(db, Roles.Administrador);
        await servicio.ReemplazarNumerosAsync(id, new[] { new NumeroItem(0, "88888888", "Teléfono", true) },
            version: (await servicio.ObtenerAsync(id)).Versiones["datos"]);
        var catalogos = new Catalogos.CatalogosService(db, new IdentityPrueba(), new ActualizadorCobros(db, new FechaPrueba()));

        var nuevo = await catalogos.CrearAsync("paises", new Catalogos.GuardarCatalogoItemRequest("Panamá", "507", null));
        Assert.Equal("+507", nuevo.Codigo);
        await Assert.ThrowsAsync<ValidacionException>(() =>
            catalogos.CrearAsync("paises", new Catalogos.GuardarCatalogoItemRequest("Error", "+50a", null)));

        var cr = await db.Paises.Where(p => p.Codigo == "+506").Select(p => p.Id).SingleAsync();
        await Assert.ThrowsAsync<ValidacionException>(() => catalogos.EliminarAsync("paises", cr));
        await catalogos.EliminarAsync("paises", nuevo.Id);
        Assert.Null(await db.Paises.FindAsync(nuevo.Id));
    }

    [Theory]
    [InlineData("vendedores")]
    [InlineData("procedencias")]
    [InlineData("metodos")]
    public async Task Catalogo_de_venta_no_se_elimina_si_esta_en_uso(string tipo)
    {
        using var db = CrearDb();
        var id = await SembrarAsync(db, EstadoPrima.Pendiente);
        var catalogos = new Catalogos.CatalogosService(db, new IdentityPrueba(), new ActualizadorCobros(db, new FechaPrueba()));
        var usado = await catalogos.CrearAsync(tipo, new Catalogos.GuardarCatalogoItemRequest("Usado", null, null));
        var libre = await catalogos.CrearAsync(tipo, new Catalogos.GuardarCatalogoItemRequest("Libre", null, null));
        var persona = await db.Personas.Include(p => p.Ventas).SingleAsync(p => p.Id == id);
        switch (tipo)
        {
            case "vendedores": persona.VendedorId = usado.Id; break;
            case "procedencias": persona.Ventas[0].ProcedenciaVentaId = usado.Id; break;
            case "metodos": persona.Ventas[0].MetodoVentaId = usado.Id; break;
        }
        await db.SaveChangesAsync();

        await Assert.ThrowsAsync<ValidacionException>(() => catalogos.EliminarAsync(tipo, usado.Id));
        await catalogos.EliminarAsync(tipo, libre.Id);

        IQueryable<Domain.Common.Catalogo> items = tipo == "vendedores" ? db.Vendedores : tipo == "procedencias" ? db.ProcedenciasVenta : db.MetodosVenta;
        Assert.Equal(new[] { "Usado" }, await items.Where(x => x.Id == usado.Id).Select(x => x.Nombre).ToListAsync());
    }

    [Fact]
    public async Task Carrera_real_revierte_todos_los_cambios_del_segundo_guardado()
    {
        using var conexion = new SqliteConnection("Data Source=:memory:;Foreign Keys=False");
        await conexion.OpenAsync();
        var options = new DbContextOptionsBuilder<TestDb>().UseSqlite(conexion).Options;
        int id;
        using (var seed = new TestDb(options))
        {
            await seed.Database.EnsureCreatedAsync();
            id = await SembrarAsync(seed, EstadoPrima.Pendiente);
        }
        using var a = new TestDb(options);
        using var b = new TestDb(options);
        var sa = CrearServicio(a, Roles.Administrador);
        var sb = CrearServicio(b, Roles.Administrador);
        var version = (await sa.ObtenerAsync(id)).Versiones["datos"];
        await sb.ObtenerAsync(id); // ambas sesiones ya cargaron la misma versión en EF
        await sa.ReemplazarNumerosAsync(id, new[] { new NumeroItem(0, "88888888", "Teléfono", true) }, version: version);
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => sb.ActualizarDatosAsync(id,
            new ActualizarDatosRequest("Cambio perdido", "Vargas", "123", "01-20013", "", new DateOnly(2026, 10, 9),
                1, "99999999", null, null, null, null), version: version));
        using var verificar = new TestDb(options);
        Assert.Equal("Ana", (await verificar.Personas.SingleAsync()).Nombres);
        Assert.Equal("88888888", (await verificar.Numeros.SingleAsync()).Valor);
    }

    [Fact]
    public async Task Secciones_distintas_pueden_guardarse_desde_la_misma_lectura()
    {
        using var conexion = new SqliteConnection("Data Source=:memory:;Foreign Keys=False");
        await conexion.OpenAsync();
        var options = new DbContextOptionsBuilder<TestDb>().UseSqlite(conexion).Options;
        int id;
        using (var seed = new TestDb(options))
        {
            await seed.Database.EnsureCreatedAsync();
            id = await SembrarAsync(seed, EstadoPrima.Pendiente);
        }
        using var a = new TestDb(options);
        using var b = new TestDb(options);
        var sa = CrearServicio(a, Roles.Administrador);
        var sb = CrearServicio(b, Roles.Administrador);
        var versiones = (await sa.ObtenerAsync(id)).Versiones;
        await sb.ObtenerAsync(id);
        await sa.ActualizarVentaAsync(id, new ActualizarVentaRequest(1, 1, 2000m, "Venta"), version: versiones["venta"]);
        await sb.ActualizarPrimaAsync(id, new ActualizarPrimaRequest(100m, 50m, null, null), version: versiones["prima"]);
        using var verificar = new TestDb(options);
        Assert.Equal(2000m, (await verificar.Ventas.SingleAsync()).Monto);
        Assert.Equal(50m, (await verificar.Primas.SingleAsync()).MontoCancelado);
    }

    [Fact]
    public async Task Http_exige_version_devuelve_etag_y_rechaza_version_antigua()
    {
        using var conexion = new SqliteConnection("Data Source=:memory:;Foreign Keys=False");
        await conexion.OpenAsync();
        var options = new DbContextOptionsBuilder<TestDb>().UseSqlite(conexion)
            .AddInterceptors(new AuditoriaInterceptor(new UsuarioPrueba(Roles.Administrador), new FechaPrueba())).Options;
        int id;
        using (var seed = new TestDb(options))
        {
            await seed.Database.EnsureCreatedAsync();
            id = await SembrarAsync(seed, EstadoPrima.Pendiente);
        }
        using var server = new TestServer(new WebHostBuilder()
            .ConfigureServices(services =>
            {
                services.AddControllers().AddApplicationPart(typeof(PersonasController).Assembly);
                // La autenticación se prueba por separado; aquí se aísla el contrato HTTP de concurrencia.
                services.AddAuthorization(o => o.DefaultPolicy = new AuthorizationPolicyBuilder().RequireAssertion(_ => true).Build());
                services.AddScoped(_ => new TestDb(options));
                services.AddScoped(sp => CrearServicio(sp.GetRequiredService<TestDb>(), Roles.Administrador));
                services.AddProblemDetails();
                services.AddExceptionHandler<ManejadorExcepciones>();
            })
            .Configure(app =>
            {
                app.UseExceptionHandler();
                app.UseRouting();
                app.UseAuthorization();
                app.UseEndpoints(e => e.MapControllers());
            }));
        using var http = server.CreateClient();
        var ficha = await http.GetFromJsonAsync<PersonaDetalleDto>($"/api/personas/{id}");
        var original = $"\"{ficha!.Versiones["datos"]:D}\"";
        var ruta = $"/api/personas/{id}/numeros";
        Assert.Equal((HttpStatusCode)428, (await http.PutAsJsonAsync(ruta, new[] { new NumeroItem(0, "99999999", "Teléfono", true) })).StatusCode);
        http.DefaultRequestHeaders.Add("If-Match", original);
        var primera = await http.PutAsJsonAsync(ruta, new[] { new NumeroItem(0, "88888888", "Teléfono", true) });
        Assert.Equal(HttpStatusCode.OK, primera.StatusCode);
        Assert.NotNull(primera.Headers.ETag);
        Assert.NotEqual(original, primera.Headers.ETag!.Tag);
        var conflicto = await http.PutAsJsonAsync(ruta, new[] { new NumeroItem(0, "99999999", "Teléfono", true) });
        Assert.Equal(HttpStatusCode.Conflict, conflicto.StatusCode);
        // El aviso informa quién guardó la sección y cuándo.
        var problema = await conflicto.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        Assert.Equal(1, problema.GetProperty("modificadoPorId").GetInt32());
        Assert.Equal("Carlos Pérez", problema.GetProperty("modificadoPor").GetString());
        Assert.Equal(new DateTime(2026, 10, 9, 12, 0, 0, DateTimeKind.Utc), problema.GetProperty("modificadoEn").GetDateTime());
        var actual = await http.GetFromJsonAsync<PersonaDetalleDto>($"/api/personas/{id}");
        Assert.Equal("88888888", Assert.Single(actual!.Numeros).Numero);
        // Un ID positivo desconocido devuelve 400 y conserva la lista y su versión.
        http.DefaultRequestHeaders.Remove("If-Match");
        http.DefaultRequestHeaders.Add("If-Match", $"\"{actual.Versiones["datos"]:D}\"");
        var invalido = await http.PutAsJsonAsync(ruta, new[] { new NumeroItem(int.MaxValue, "99999999", "Teléfono", true) });
        Assert.Equal(HttpStatusCode.BadRequest, invalido.StatusCode);
        var conservada = await http.GetFromJsonAsync<PersonaDetalleDto>($"/api/personas/{id}");
        Assert.Equal("88888888", Assert.Single(conservada!.Numeros).Numero);
        Assert.Equal(actual.Versiones["datos"], conservada.Versiones["datos"]);
        var longitudInvalida = await http.PutAsJsonAsync(ruta, new[] { new NumeroItem(actual.Numeros[0].Id, "8888777", "Teléfono", true) });
        Assert.Equal(HttpStatusCode.BadRequest, longitudInvalida.StatusCode);
        var longitudConservada = await http.GetFromJsonAsync<PersonaDetalleDto>($"/api/personas/{id}");
        Assert.Equal(actual.Versiones["datos"], longitudConservada!.Versiones["datos"]);
        Assert.Equal("88888888", longitudConservada.Numeros[0].Numero);
        var repetido = await http.PutAsJsonAsync(ruta, new[]
        {
            new NumeroItem(actual.Numeros[0].Id, "11111111", "Teléfono", true),
            new NumeroItem(actual.Numeros[0].Id, "22222222", "Teléfono", false)
        });
        Assert.Equal(HttpStatusCode.BadRequest, repetido.StatusCode);
        var erroresRepetido = await repetido.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        Assert.True(erroresRepetido.GetProperty("errors").TryGetProperty("numeros[1].id", out _));
        var despuesRepetido = await http.GetFromJsonAsync<PersonaDetalleDto>($"/api/personas/{id}");
        Assert.Equal(actual.Versiones["datos"], despuesRepetido!.Versiones["datos"]);
        Assert.Equal("88888888", Assert.Single(despuesRepetido.Numeros).Numero);
        foreach (var item in new[]
        {
            new NumeroItem(actual.Numeros[0].Id, "88888888", "Otro", true),
            new NumeroItem(actual.Numeros[0].Id, "88888888", "Teléfono", true, -1)
        })
        {
            Assert.Equal(HttpStatusCode.BadRequest, (await http.PutAsJsonAsync(ruta, new[] { item })).StatusCode);
            var despuesInvalido = await http.GetFromJsonAsync<PersonaDetalleDto>($"/api/personas/{id}");
            Assert.Equal(actual.Versiones["datos"], despuesInvalido!.Versiones["datos"]);
            Assert.Equal("88888888", Assert.Single(despuesInvalido.Numeros).Numero);
        }
        // Validación HTTP: no se pierde el último contacto ni cambia la versión.
        var vacio = await http.PutAsJsonAsync(ruta, Array.Empty<NumeroItem>());
        Assert.Equal(HttpStatusCode.BadRequest, vacio.StatusCode);
        var primaAntes = await http.GetFromJsonAsync<PersonaDetalleDto>($"/api/personas/{id}");
        http.DefaultRequestHeaders.Remove("If-Match");
        http.DefaultRequestHeaders.Add("If-Match", $"\"{primaAntes!.Versiones["prima"]:D}\"");
        // Monto cancelado omitido no equivale a cero.
        var omitido = await http.PutAsJsonAsync($"/api/personas/{id}/prima", new { monto = 100m });
        Assert.Equal(HttpStatusCode.BadRequest, omitido.StatusCode);
        var primaDespues = await http.GetFromJsonAsync<PersonaDetalleDto>($"/api/personas/{id}");
        Assert.Equal(primaAntes.Versiones["prima"], primaDespues!.Versiones["prima"]);
        Assert.Equal(actual.Versiones["datos"], primaDespues.Versiones["datos"]);
        http.DefaultRequestHeaders.Remove("If-Match");
        http.DefaultRequestHeaders.Add("If-Match", "*");
        Assert.Equal(HttpStatusCode.BadRequest, (await http.PutAsJsonAsync(ruta, new[] { new NumeroItem(0, "99999999", "Teléfono", true) })).StatusCode);
    }

    [Theory]
    [InlineData("correos", "correos[1].correo")]
    [InlineData("familiares", "familiares[1].correoElectronico")]
    [InlineData("parentesco", "familiares[1].parentescoId")]
    [InlineData("numeros", "numeros[1].numero")]
    public async Task Error_de_segunda_fila_conserva_lista_y_version(string tipo, string campo)
    {
        using var db = CrearDb();
        var id = await SembrarAsync(db, EstadoPrima.Pendiente);
        var servicio = CrearServicio(db, Roles.Administrador);
        var antes = await servicio.ObtenerAsync(id);
        Task Guardar() => tipo switch
        {
            "correos" => servicio.ReemplazarCorreosAsync(id,
                new[] { new CorreoItem(0, "uno@mail.cr", true), new CorreoItem(0, "incorrecto", false) },
                version: antes.Versiones["datos"]),
            "numeros" => servicio.ReemplazarNumerosAsync(id,
                new[] { new NumeroItem(0, "88887777", "Teléfono", true), new NumeroItem(0, "", "WhatsApp", true) },
                version: antes.Versiones["datos"]),
            _ => servicio.ReemplazarFamiliaresAsync(id,
                new[] { new FamiliarItem(0, "Uno", null, "", "uno@mail.cr", ""),
                    new FamiliarItem(0, "Dos", tipo == "parentesco" ? 999999 : null, "",
                        tipo == "familiares" ? "incorrecto" : "dos@mail.cr", "") },
                version: antes.Versiones["familiares"])
        };
        var error = await Assert.ThrowsAsync<ValidacionException>(Guardar);
        Assert.Contains(campo, error.Errores.Keys);
        var despues = await servicio.ObtenerAsync(id);
        Assert.Equal(antes.Versiones.OrderBy(x => x.Key), despues.Versiones.OrderBy(x => x.Key));
        Assert.Equal(System.Text.Json.JsonSerializer.Serialize(antes.Numeros), System.Text.Json.JsonSerializer.Serialize(despues.Numeros));
        Assert.Equal(System.Text.Json.JsonSerializer.Serialize(antes.Correos), System.Text.Json.JsonSerializer.Serialize(despues.Correos));
        Assert.Equal(System.Text.Json.JsonSerializer.Serialize(antes.Familiares), System.Text.Json.JsonSerializer.Serialize(despues.Familiares));
    }

    [Theory]
    [InlineData("numeros", true)]
    [InlineData("numeros", false)]
    [InlineData("correos", true)]
    [InlineData("correos", false)]
    [InlineData("familiares", true)]
    [InlineData("familiares", false)]
    public async Task Id_positivo_ajeno_o_inexistente_rechaza_toda_la_lista_sin_mutaciones(string tipo, bool ajeno)
    {
        using var db = CrearDb();
        var id = await SembrarAsync(db, EstadoPrima.Pendiente);
        var otroId = await SembrarAsync(db, EstadoPrima.Pendiente);
        var servicio = CrearServicio(db, Roles.Administrador);
        var seccion = tipo == "familiares" ? "familiares" : "datos";

        Task Guardar(int personaId, (int Id, string Valor)[] filas, Guid version) => tipo switch
        {
            "numeros" => servicio.ReemplazarNumerosAsync(personaId,
                filas.Select(x => new NumeroItem(x.Id, x.Valor, "Teléfono", true)).ToArray(), version: version),
            "correos" => servicio.ReemplazarCorreosAsync(personaId,
                filas.Select(x => new CorreoItem(x.Id, x.Valor, true)).ToArray(), version: version),
            _ => servicio.ReemplazarFamiliaresAsync(personaId,
                filas.Select(x => new FamiliarItem(x.Id, x.Valor, null, "88887777", "", "")).ToArray(), version: version)
        };
        int Primero(PersonaDetalleDto ficha) => tipo switch
        {
            "numeros" => ficha.Numeros[0].Id,
            "correos" => ficha.Correos[0].Id,
            _ => ficha.Familiares[0].Id
        };
        string Lista(PersonaDetalleDto ficha) => tipo switch
        {
            "numeros" => System.Text.Json.JsonSerializer.Serialize(ficha.Numeros),
            "correos" => System.Text.Json.JsonSerializer.Serialize(ficha.Correos),
            _ => System.Text.Json.JsonSerializer.Serialize(ficha.Familiares)
        };
        var valores = tipo == "numeros" ? new[] { "11111111", "22222222", "33333333", "99999999" } :
            tipo == "correos" ? new[] { "uno@mail.cr", "dos@mail.cr", "ajeno@mail.cr", "cambio@mail.cr" } :
            new[] { "Uno", "Dos", "Ajeno", "Cambio" };

        await Guardar(id, new[] { (0, valores[0]), (-1, valores[1]) }, (await servicio.ObtenerAsync(id)).Versiones[seccion]);
        await Guardar(otroId, new[] { (0, valores[2]) }, (await servicio.ObtenerAsync(otroId)).Versiones[seccion]);
        var antes = await servicio.ObtenerAsync(id);
        var otroAntes = await servicio.ObtenerAsync(otroId);
        var guardados = db.Guardados;
        var invalido = ajeno ? Primero(otroAntes) : int.MaxValue;
        // Incluye edición propia y alta; omite otra fila propia que el reemplazo borraría.
        var error = await Assert.ThrowsAsync<ValidacionException>(() => Guardar(id,
            new[] { (Primero(antes), valores[3]), (-2, valores[3]), (invalido, valores[3]) }, antes.Versiones[seccion]));
        Assert.Contains(tipo + "[2].id", error.Errores.Keys);
        Assert.Equal(guardados, db.Guardados);
        Assert.DoesNotContain(db.ChangeTracker.Entries(), e =>
            e.State == EntityState.Added || e.State == EntityState.Modified || e.State == EntityState.Deleted);
        db.ChangeTracker.Clear();
        var despues = await servicio.ObtenerAsync(id);
        Assert.Equal(Lista(antes), Lista(despues));
        Assert.Equal(antes.Versiones[seccion], despues.Versiones[seccion]);
        Assert.Equal(Lista(otroAntes), Lista(await servicio.ObtenerAsync(otroId)));
    }

    [Theory]
    [InlineData("numeros")]
    [InlineData("correos")]
    [InlineData("familiares")]
    public async Task Id_propio_se_conserva_y_ids_cero_o_negativos_siguen_creando_filas(string tipo)
    {
        using var db = CrearDb();
        var id = await SembrarAsync(db, EstadoPrima.Pendiente);
        var servicio = CrearServicio(db, Roles.Administrador);
        var seccion = tipo == "familiares" ? "familiares" : "datos";
        Task Guardar((int Id, string Valor)[] filas, Guid version) => tipo switch
        {
            "numeros" => servicio.ReemplazarNumerosAsync(id,
                filas.Select((x, i) => new NumeroItem(x.Id, x.Valor, i == 2 ? "WhatsApp" : "Teléfono", true)).ToArray(), version: version),
            "correos" => servicio.ReemplazarCorreosAsync(id,
                filas.Select(x => new CorreoItem(x.Id, x.Valor, true)).ToArray(), version: version),
            _ => servicio.ReemplazarFamiliaresAsync(id,
                filas.Select(x => new FamiliarItem(x.Id, x.Valor, null, "88887777", "", "")).ToArray(), version: version)
        };
        int[] Ids(PersonaDetalleDto ficha) => tipo switch
        {
            "numeros" => ficha.Numeros.Select(x => x.Id).ToArray(),
            "correos" => ficha.Correos.Select(x => x.Id).ToArray(),
            _ => ficha.Familiares.Select(x => x.Id).ToArray()
        };
        var valores = tipo == "numeros" ? new[] { "11111111", "22222222", "33333333", "44444444" } :
            tipo == "correos" ? new[] { "uno@mail.cr", "dos@mail.cr", "tres@mail.cr", "cuatro@mail.cr" } :
            new[] { "Uno", "Dos", "Tres", "Cuatro" };
        await Guardar(new[] { (0, valores[0]) }, (await servicio.ObtenerAsync(id)).Versiones[seccion]);
        var antes = await servicio.ObtenerAsync(id);
        var propio = Ids(antes)[0];
        await Guardar(new[] { (propio, valores[1]), (0, valores[2]), (-1, valores[3]) }, antes.Versiones[seccion]);
        var despues = await servicio.ObtenerAsync(id);
        var ids = Ids(despues);
        Assert.Equal(3, ids.Length);
        Assert.Contains(propio, ids);
        Assert.All(ids, i => Assert.True(i > 0));
        Assert.Equal(3, ids.Distinct().Count());
        var valorEditado = tipo == "numeros" ? despues.Numeros.Single(x => x.Id == propio).Numero :
            tipo == "correos" ? despues.Correos.Single(x => x.Id == propio).Correo :
            despues.Familiares.Single(x => x.Id == propio).NombreCompleto;
        Assert.Equal(valores[1], valorEditado);
    }

    [Theory]
    [InlineData("telefonos", 2)]
    [InlineData("whatsapp", 2)]
    [InlineData("correos", 3)]
    [InlineData("familiares", 4)]
    public async Task Listas_aceptan_maximo_y_rechazan_exceso_sin_cambiar_datos_ni_version(string tipo, int maximo)
    {
        using var db = CrearDb();
        var id = await SembrarAsync(db, EstadoPrima.Pendiente);
        var servicio = CrearServicio(db, Roles.Administrador);
        var seccion = tipo == "familiares" ? "familiares" : "datos";
        Task Guardar(int cantidad, Guid version) => tipo switch
        {
            "telefonos" or "whatsapp" => servicio.ReemplazarNumerosAsync(id, Enumerable.Range(0, cantidad)
                .Select(i => new NumeroItem(-i, "8888000" + i, tipo == "telefonos" ? "Teléfono" : "WhatsApp", i == 0)).ToArray(), version: version),
            "correos" => servicio.ReemplazarCorreosAsync(id, Enumerable.Range(0, cantidad)
                .Select(i => new CorreoItem(-i, "correo" + i + "@mail.cr", i == 0)).ToArray(), version: version),
            _ => servicio.ReemplazarFamiliaresAsync(id, Enumerable.Range(0, cantidad)
                .Select(i => new FamiliarItem(-i, "Familiar " + i, null, "88887777", "", "")).ToArray(), version: version)
        };
        string Lista(PersonaDetalleDto ficha) => tipo switch
        {
            "telefonos" or "whatsapp" => System.Text.Json.JsonSerializer.Serialize(ficha.Numeros),
            "correos" => System.Text.Json.JsonSerializer.Serialize(ficha.Correos),
            _ => System.Text.Json.JsonSerializer.Serialize(ficha.Familiares)
        };
        await Guardar(maximo, (await servicio.ObtenerAsync(id)).Versiones[seccion]);
        var antes = await servicio.ObtenerAsync(id);
        var guardados = db.Guardados;
        var error = await Assert.ThrowsAsync<ValidacionException>(() => Guardar(maximo + 1, antes.Versiones[seccion]));
        Assert.Contains(tipo == "telefonos" || tipo == "whatsapp" ? "numeros" : tipo, error.Errores.Keys);
        Assert.Equal(guardados, db.Guardados);
        Assert.DoesNotContain(db.ChangeTracker.Entries(), e =>
            e.State == EntityState.Added || e.State == EntityState.Modified || e.State == EntityState.Deleted);
        db.ChangeTracker.Clear();
        var despues = await servicio.ObtenerAsync(id);
        Assert.Equal(Lista(antes), Lista(despues));
        Assert.Equal(antes.Versiones[seccion], despues.Versiones[seccion]);
        // El mismo token sigue sirviendo después del rechazo y permite reducir la lista.
        await Guardar(maximo - 1, antes.Versiones[seccion]);
    }

    [Fact]
    public async Task Telefonos_y_whatsapp_tienen_cupos_separados_y_el_cambio_de_tipo_respeta_el_maximo()
    {
        using var db = CrearDb();
        var id = await SembrarAsync(db, EstadoPrima.Pendiente);
        var servicio = CrearServicio(db, Roles.Administrador);
        var items = new[]
        {
            new NumeroItem(0, "11111111", "Teléfono", true),
            new NumeroItem(-1, "22222222", "Teléfono", false),
            new NumeroItem(-2, "33333333", "WhatsApp", true),
            new NumeroItem(-3, "44444444", "WhatsApp", false)
        };
        var resultado = await servicio.ReemplazarNumerosAsync(id, items,
            version: (await servicio.ObtenerAsync(id)).Versiones["datos"]);
        Assert.Equal(4, resultado.Count);
        var antes = await servicio.ObtenerAsync(id);
        var cambio = resultado.Select((x, i) => new NumeroItem(x.Id, x.Numero, i == 0 ? "WhatsApp" : x.Tipo, x.Principal, x.PaisId)).ToArray();
        await Assert.ThrowsAsync<ValidacionException>(() => servicio.ReemplazarNumerosAsync(id, cambio, version: antes.Versiones["datos"]));
        Assert.Equal(2, (await servicio.ObtenerAsync(id)).Numeros.Count(x => x.Tipo == "Teléfono"));
        Assert.Equal(2, (await servicio.ObtenerAsync(id)).Numeros.Count(x => x.Tipo == "WhatsApp"));
    }

    [Fact]
    public async Task Crear_prospecto_admite_cuatro_familiares_y_rechaza_cinco_sin_crear_persona()
    {
        using var db = CrearDb();
        var vendedor = new Vendedor { Codigo = "30", Nombre = "Marta" };
        var procedencia = new ProcedenciaVenta { Nombre = "Feria" };
        var metodo = new MetodoVenta { Nombre = "Llamada" };
        db.AddRange(vendedor, procedencia, metodo, new Pais { Codigo = "+506", Nombre = "Costa Rica" });
        await db.SaveChangesAsync();
        var servicio = CrearServicio(db, Roles.Administrador);
        CrearProspectoRequest Request(int cantidad) => new(
            "Ana", "Vargas", "123", "01-20013", "", new DateOnly(2026, 10, 9), vendedor.Id,
            "88888888", "", "", procedencia.Id, metodo.Id, 1000m, "", 100m, 0m, null, null,
            Enumerable.Range(0, cantidad).Select(i => new FamiliarItem(-i, "Familiar " + i, null, "88887777", "", "")).ToArray());
        var guardados = db.Guardados;
        await Assert.ThrowsAsync<ValidacionException>(() => servicio.CrearProspectoAsync(Request(5)));
        Assert.Equal(guardados, db.Guardados);
        Assert.Empty(await db.Personas.ToListAsync());
        var creada = await servicio.CrearProspectoAsync(Request(4));
        Assert.Equal(4, creada.Familiares.Count);
    }

    private static TestDb CrearDb() => new(new DbContextOptionsBuilder<TestDb>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static async Task<int> SembrarAsync(TestDb db, int estado)
    {
        var prima = new Prima
        {
            Monto = 100m,
            MontoCancelado = estado == EstadoPrima.Pagada ? 100m :
                estado == EstadoPrima.Incompleta ? 50m : 0m
        };
        prima.Recalcular();
        if (!db.Vendedores.Any()) db.Vendedores.Add(new Vendedor { Codigo = "1", Nombre = "Asesor" });
        if (!db.ProcedenciasVenta.Any()) db.ProcedenciasVenta.Add(new ProcedenciaVenta { Nombre = "Procedencia" });
        if (!db.MetodosVenta.Any()) db.MetodosVenta.Add(new MetodoVenta { Nombre = "Método" });
        var persona = new Persona { Nombres = "Ana", Apellidos = "Vargas" };
        persona.Ventas.Add(new Venta { Monto = 1000m, Prima = prima });
        persona.Comentarios.Add(new Comentario { UsuarioId = 1, Texto = "Existente", Fecha = DateTime.UtcNow });
        if (!db.Paises.Any()) db.Paises.Add(new Pais { Codigo = "+506", Nombre = "Costa Rica" });
        db.Personas.Add(persona);
        await db.SaveChangesAsync();
        var id = persona.Id;
        db.ChangeTracker.Clear();
        return id;
    }

    [Theory]
    [InlineData("nombres")]
    [InlineData("apellidos")]
    [InlineData("cedula")]
    [InlineData("finca")]
    [InlineData("fechaIngreso")]
    [InlineData("vendedorId")]
    [InlineData("contacto")]
    public async Task Editar_datos_requeridos_rechaza_vacios_sin_cambiar_version(string campo)
    {
        using var db = CrearDb();
        var id = await SembrarAsync(db, EstadoPrima.Pendiente);
        var servicio = CrearServicio(db, Roles.Administrador);
        var antes = await servicio.ObtenerAsync(id);
        var valido = new ActualizarDatosRequest("Ana", "Vargas", "123", "01-20013", "", new DateOnly(2026, 10, 9),
            1, "88888888", "", "", null, null);
        var r = campo switch
        {
            "nombres" => valido with { Nombres = " " },
            "apellidos" => valido with { Apellidos = "" },
            "cedula" => valido with { Cedula = "abc" },
            "finca" => valido with { Finca = "" },
            "fechaIngreso" => valido with { FechaIngreso = default },
            "vendedorId" => valido with { VendedorId = null },
            _ => valido with { TelefonoPrincipal = "", WhatsappPrincipal = "" }
        };
        var guardados = db.Guardados;
        await Assert.ThrowsAsync<ValidacionException>(() => servicio.ActualizarDatosAsync(id, r, version: antes.Versiones["datos"]));
        Assert.Equal(guardados, db.Guardados);
        db.ChangeTracker.Clear();
        var despues = await servicio.ObtenerAsync(id);
        Assert.Equal(antes.Nombres, despues.Nombres);
        Assert.Equal(antes.Versiones["datos"], despues.Versiones["datos"]);
    }

    [Theory]
    [InlineData("", "", false)]
    [InlineData("abc", "", false)]
    [InlineData("88887777", "", true)]
    [InlineData("", "luis@mail.cr", true)]
    [InlineData("88887777", "correo-invalido", false)]
    public async Task Familiar_requiere_telefono_o_correo_valido(string telefono, string correo, bool valido)
    {
        using var db = CrearDb();
        var id = await SembrarAsync(db, EstadoPrima.Pendiente);
        var servicio = CrearServicio(db, Roles.Administrador);
        var antes = await servicio.ObtenerAsync(id);
        var guardados = db.Guardados;
        Task Guardar() => servicio.ReemplazarFamiliaresAsync(id,
            new[] { new FamiliarItem(0, "Luis", null, telefono, correo, "88887777") }, version: antes.Versiones["familiares"]);
        if (valido) await Guardar();
        else
        {
            await Assert.ThrowsAsync<ValidacionException>(Guardar);
            Assert.Equal(guardados, db.Guardados);
            Assert.Equal(antes.Versiones["familiares"], (await servicio.ObtenerAsync(id)).Versiones["familiares"]);
        }
    }

    [Theory]
    [InlineData("crear", "+506", 8)]
    [InlineData("crear", "+1", 10)]
    [InlineData("crear-whatsapp", "+506", 8)]
    [InlineData("crear-whatsapp", "+1", 10)]
    [InlineData("datos-whatsapp", "+506", 8)]
    [InlineData("datos-whatsapp", "+1", 10)]
    [InlineData("datos", "+506", 8)]
    [InlineData("datos", "+1", 10)]
    [InlineData("numeros", "+506", 8)]
    [InlineData("numeros", "+1", 10)]
    [InlineData("whatsapp", "+506", 8)]
    [InlineData("whatsapp", "+1", 10)]
    [InlineData("familiar", "+506", 8)]
    [InlineData("familiar", "+1", 10)]
    [InlineData("familiar-whatsapp", "+506", 8)]
    [InlineData("familiar-whatsapp", "+1", 10)]
    [InlineData("numeros", "+44", 12)]
    [InlineData("familiar", "+44", 12)]
    public async Task Telefonos_respetan_longitud_por_pais_sin_escrituras_parciales(string operacion, string codigo, int largo)
    {
        using var db = CrearDb();
        var id = await SembrarAsync(db, EstadoPrima.Pendiente);
        if (!await db.Paises.AnyAsync(p => p.Codigo == codigo))
        {
            db.Paises.Add(new Pais { Codigo = codigo, Nombre = codigo == "+1" ? "Estados Unidos" : "Otro país" });
            await db.SaveChangesAsync();
        }
        var paisId = await db.Paises.Where(p => p.Codigo == codigo).Select(p => p.Id).SingleAsync();
        var servicio = CrearServicio(db, Roles.Administrador);
        var antes = await servicio.ObtenerAsync(id);
        var seccion = operacion.StartsWith("familiar") ? "familiares" : "datos";
        Task Guardar(string numero) => operacion switch
        {
            "crear" or "crear-whatsapp" => servicio.CrearProspectoAsync(new CrearProspectoRequest(
                "Ana", "Vargas", "123", "01-20013", "", new DateOnly(2026, 10, 13), 1,
                operacion == "crear" ? numero : "", operacion == "crear-whatsapp" ? numero : "", "",
                1, 1, 1000m, "", 100m, 0m, null, null, null, paisId, paisId)),
            "datos" or "datos-whatsapp" => servicio.ActualizarDatosAsync(id, new ActualizarDatosRequest(
                "Cambio", "Vargas", "123", "01-20013", "", new DateOnly(2026, 10, 13), 1,
                operacion == "datos" ? numero : "", operacion == "datos-whatsapp" ? numero : "", "",
                null, null, paisId, paisId), version: antes.Versiones[seccion]),
            "numeros" or "whatsapp" => servicio.ReemplazarNumerosAsync(id, new[]
            {
                new NumeroItem(0, "88888888", "Teléfono", true),
                new NumeroItem(-1, numero, operacion == "whatsapp" ? "WhatsApp" : "Teléfono", false, paisId)
            }, version: antes.Versiones[seccion]),
            _ => servicio.ReemplazarFamiliaresAsync(id, new[]
            {
                new FamiliarItem(0, "Luis", null, "88887777", "", ""),
                new FamiliarItem(-1, "María", null, operacion == "familiar" ? numero : "", "maria@mail.cr",
                    operacion == "familiar-whatsapp" ? numero : "", paisId, paisId)
            }, version: antes.Versiones[seccion])
        };
        foreach (var cantidad in new[] { largo - 1, largo + 1 })
        {
            if (codigo != "+506")
            {
                await Guardar(new string('8', cantidad));
                antes = await servicio.ObtenerAsync(id);
                continue;
            }
            var guardados = db.Guardados;
            await Assert.ThrowsAsync<ValidacionException>(() => Guardar(new string('8', cantidad)));
            Assert.Equal(guardados, db.Guardados);
            Assert.DoesNotContain(db.ChangeTracker.Entries(), e =>
                e.State == EntityState.Added || e.State == EntityState.Modified || e.State == EntityState.Deleted);
            db.ChangeTracker.Clear();
            var despues = await servicio.ObtenerAsync(id);
            Assert.Equal(antes.Versiones[seccion], despues.Versiones[seccion]);
            Assert.Equal("Ana", despues.Nombres);
            Assert.Empty(despues.Numeros);
            Assert.Empty(despues.Familiares);
        }
        await Guardar(new string('8', largo));
    }

    [Fact]
    public async Task Cambiar_pais_revalida_longitud_y_conserva_numero_y_version_si_falla()
    {
        using var db = CrearDb();
        var id = await SembrarAsync(db, EstadoPrima.Pendiente);
        db.Paises.Add(new Pais { Codigo = "+1", Nombre = "Estados Unidos" });
        await db.SaveChangesAsync();
        var eeuu = await db.Paises.Where(p => p.Codigo == "+1").Select(p => p.Id).SingleAsync();
        var servicio = CrearServicio(db, Roles.Administrador);
        var guardados = await servicio.ReemplazarNumerosAsync(id,
            new[] { new NumeroItem(0, "305555010012", "Teléfono", true, eeuu) }, version: (await servicio.ObtenerAsync(id)).Versiones["datos"]);
        var antes = await servicio.ObtenerAsync(id);
        await Assert.ThrowsAsync<ValidacionException>(() => servicio.ReemplazarNumerosAsync(id,
            new[] { new NumeroItem(guardados[0].Id, "305555010012", "Teléfono", true) }, version: antes.Versiones["datos"]));
        db.ChangeTracker.Clear();
        var despues = await servicio.ObtenerAsync(id);
        Assert.Equal(antes.Versiones["datos"], despues.Versiones["datos"]);
        Assert.Equal(antes.Numeros[0], despues.Numeros[0]);
    }

    [Theory]
    [InlineData("finca")]
    [InlineData("expediente")]
    [InlineData("notas")]
    [InlineData("conversion")]
    public async Task Formatos_invalidos_no_modifican_datos_ni_versiones(string campo)
    {
        using var db = CrearDb();
        var id = await SembrarAsync(db, EstadoPrima.Pendiente);
        var servicio = CrearServicio(db, Roles.Administrador);
        var antes = await servicio.ObtenerAsync(id);
        var guardados = db.Guardados;
        var r = new ActualizarDatosRequest("Cambio", "Vargas", "123", "12-12345", "", new DateOnly(2026, 10, 13),
            1, "88887777", "", "", null, null);
        Task Guardar() => campo switch
        {
            "finca" => servicio.ActualizarDatosAsync(id, r with { Finca = "12-123456" }, version: antes.Versiones["datos"]),
            "expediente" => servicio.ActualizarDatosAsync(id, r with { Expediente = "12-123456-1234-A1" }, version: antes.Versiones["datos"]),
            "notas" => servicio.ActualizarVentaAsync(id, new ActualizarVentaRequest(1, 1, 1000m, new string('x', 1001)), version: antes.Versiones["venta"]),
            _ => servicio.ConvertirEnClienteAsync(id, new ConvertirClienteRequest("12-123456-1234-A1", 1, null), version: antes.Versiones["datos"])
        };
        await Assert.ThrowsAsync<ValidacionException>(Guardar);
        Assert.Equal(guardados, db.Guardados);
        Assert.DoesNotContain(db.ChangeTracker.Entries(), e =>
            e.State == EntityState.Added || e.State == EntityState.Modified || e.State == EntityState.Deleted);
        db.ChangeTracker.Clear();
        var despues = await servicio.ObtenerAsync(id);
        Assert.Equal(antes.Nombres, despues.Nombres);
        Assert.Equal(antes.Finca, despues.Finca);
        Assert.Equal(antes.Expediente, despues.Expediente);
        Assert.Equal(antes.Venta!.Notas, despues.Venta!.Notas);
        Assert.Equal(antes.Versiones["datos"], despues.Versiones["datos"]);
        Assert.Equal(antes.Versiones["venta"], despues.Versiones["venta"]);
    }

    [Theory]
    [InlineData("numeros", true)]
    [InlineData("numeros", false)]
    [InlineData("correos", true)]
    [InlineData("correos", false)]
    [InlineData("familiares", true)]
    [InlineData("familiares", false)]
    public async Task Id_positivo_repetido_rechaza_lista_sin_modificar_datos_o_version(string tipo, bool distinto)
    {
        using var db = CrearDb();
        var id = await SembrarAsync(db, EstadoPrima.Pendiente);
        var servicio = CrearServicio(db, Roles.Administrador);
        var seccion = tipo == "familiares" ? "familiares" : "datos";
        Task Guardar((int Id, string Valor)[] filas, Guid version) => tipo switch
        {
            "numeros" => servicio.ReemplazarNumerosAsync(id, filas.Select((x, i) =>
                new NumeroItem(x.Id, x.Valor, i == 2 ? "WhatsApp" : "Teléfono", true)).ToArray(), version: version),
            "correos" => servicio.ReemplazarCorreosAsync(id, filas.Select(x => new CorreoItem(x.Id, x.Valor, true)).ToArray(), version: version),
            _ => servicio.ReemplazarFamiliaresAsync(id, filas.Select(x => new FamiliarItem(x.Id, x.Valor, null, "88887777", "", "")).ToArray(), version: version)
        };
        int[] Ids(PersonaDetalleDto p) => tipo switch
        {
            "numeros" => p.Numeros.Select(x => x.Id).ToArray(),
            "correos" => p.Correos.Select(x => x.Id).ToArray(),
            _ => p.Familiares.Select(x => x.Id).ToArray()
        };
        string Lista(PersonaDetalleDto p) => tipo switch
        {
            "numeros" => System.Text.Json.JsonSerializer.Serialize(p.Numeros),
            "correos" => System.Text.Json.JsonSerializer.Serialize(p.Correos),
            _ => System.Text.Json.JsonSerializer.Serialize(p.Familiares)
        };
        var a = tipo == "numeros" ? "11111111" : tipo == "correos" ? "uno@mail.cr" : "Uno";
        var b = tipo == "numeros" ? "22222222" : tipo == "correos" ? "dos@mail.cr" : "Dos";
        // IDs temporales repetidos se tratan como altas independientes.
        await Guardar(new[] { (0, a), (0, b) }, (await servicio.ObtenerAsync(id)).Versiones[seccion]);
        var antes = await servicio.ObtenerAsync(id);
        Assert.Equal(2, Ids(antes).Distinct().Count());
        var propio = Ids(antes)[0];
        var guardados = db.Guardados;
        var error = await Assert.ThrowsAsync<ValidacionException>(() => Guardar(
            new[] { (propio, a), (propio, distinto ? b : a), (-1, b) }, antes.Versiones[seccion]));
        Assert.Contains(tipo + "[1].id", error.Errores.Keys);
        Assert.Equal(guardados, db.Guardados);
        Assert.DoesNotContain(db.ChangeTracker.Entries(), e =>
            e.State == EntityState.Added || e.State == EntityState.Modified || e.State == EntityState.Deleted);
        db.ChangeTracker.Clear();
        var despues = await servicio.ObtenerAsync(id);
        Assert.Equal(Lista(antes), Lista(despues));
        Assert.Equal(antes.Versiones[seccion], despues.Versiones[seccion]);
        // El rechazo no consume la versión y las altas negativas siguen permitidas.
        await Guardar(new[] { (-1, a), (-1, b) }, antes.Versiones[seccion]);
        Assert.Equal(2, Ids(await servicio.ObtenerAsync(id)).Distinct().Count());
    }

    [Theory]
    [InlineData("crear", "telefonoPrincipalPaisId")]
    [InlineData("crear", "whatsappPrincipalPaisId")]
    [InlineData("crear", "vendedorId")]
    [InlineData("crear", "procedenciaVentaId")]
    [InlineData("crear", "metodoVentaId")]
    [InlineData("datos", "telefonoPrincipalPaisId")]
    [InlineData("datos", "whatsappPrincipalPaisId")]
    [InlineData("datos", "vendedorId")]
    [InlineData("datos", "estadoClienteId")]
    [InlineData("datos", "origenClienteId")]
    [InlineData("venta", "procedenciaVentaId")]
    [InlineData("venta", "metodoVentaId")]
    [InlineData("convertir", "estadoClienteId")]
    [InlineData("convertir", "origenClienteId")]
    [InlineData("numeros", "numeros[0].paisId")]
    [InlineData("familiares", "familiares[0].parentescoId")]
    [InlineData("familiares", "familiares[0].telefonoPaisId")]
    [InlineData("familiares", "familiares[0].whatsappPaisId")]
    [InlineData("crear-familiar", "familiares[0].parentescoId")]
    [InlineData("crear-familiar", "familiares[0].telefonoPaisId")]
    [InlineData("crear-familiar", "familiares[0].whatsappPaisId")]
    public async Task Ids_negativos_de_catalogos_rechazan_sin_escrituras(string operacion, string campo)
    {
        using var db = CrearDb();
        var id = await SembrarAsync(db, EstadoPrima.Pendiente);
        var origen = new OrigenCliente { Codigo = "OR", Nombre = "Referido" };
        db.OrigenesCliente.Add(origen);
        await db.SaveChangesAsync();
        var servicio = CrearServicio(db, Roles.Administrador);
        var antes = await servicio.ObtenerAsync(id);
        var guardados = db.Guardados;
        var crear = new CrearProspectoRequest("Ana", "Vargas", "123", "12-12345", "", new DateOnly(2026, 10, 13), 1,
            "88887777", "", "", 1, 1, 1000m, "", 100m, 0m, null, null, null);
        var datos = new ActualizarDatosRequest("Cambio", "Vargas", "123", "12-12345", "", new DateOnly(2026, 10, 13), 1,
            "88887777", "", "", null, null);
        // Países explícitos se rechazan incluso con teléfono/WhatsApp vacío y correo válido.
        var familiar = new FamiliarItem(0, "Luis", campo.EndsWith("parentescoId") ? -1 : null, "", "luis@mail.cr", "",
            campo.EndsWith("telefonoPaisId") ? -1 : null, campo.EndsWith("whatsappPaisId") ? -1 : null);
        Task Guardar() => operacion switch
        {
            "crear" => servicio.CrearProspectoAsync(campo switch
            {
                "vendedorId" => crear with { VendedorId = -1 },
                "procedenciaVentaId" => crear with { ProcedenciaVentaId = -1 },
                "metodoVentaId" => crear with { MetodoVentaId = -1 },
                "telefonoPrincipalPaisId" => crear with { TelefonoPrincipalPaisId = -1 },
                _ => crear with { WhatsappPrincipalPaisId = -1 }
            }),
            "crear-familiar" => servicio.CrearProspectoAsync(crear with { Familiares = new[] { familiar } }),
            "datos" => servicio.ActualizarDatosAsync(id, campo switch
            {
                "vendedorId" => datos with { VendedorId = -1 },
                "estadoClienteId" => datos with { EstadoClienteId = -1 },
                "origenClienteId" => datos with { OrigenClienteId = -1 },
                "telefonoPrincipalPaisId" => datos with { TelefonoPrincipalPaisId = -1 },
                _ => datos with { WhatsappPrincipalPaisId = -1 }
            }, version: antes.Versiones["datos"]),
            "venta" => servicio.ActualizarVentaAsync(id, new ActualizarVentaRequest(
                campo == "procedenciaVentaId" ? -1 : 1, campo == "metodoVentaId" ? -1 : 1, 1000m, ""), version: antes.Versiones["venta"]),
            "convertir" => servicio.ConvertirEnClienteAsync(id, new ConvertirClienteRequest("",
                campo == "origenClienteId" ? -1 : origen.Id, campo == "estadoClienteId" ? -1 : null), version: antes.Versiones["datos"]),
            "numeros" => servicio.ReemplazarNumerosAsync(id, new[] { new NumeroItem(0, "88887777", "Teléfono", true, -1) }, version: antes.Versiones["datos"]),
            _ => servicio.ReemplazarFamiliaresAsync(id, new[] { familiar }, version: antes.Versiones["familiares"])
        };
        var error = await Assert.ThrowsAsync<ValidacionException>(Guardar);
        Assert.Contains(campo, error.Errores.Keys);
        Assert.Equal(guardados, db.Guardados);
        Assert.DoesNotContain(db.ChangeTracker.Entries(), e =>
            e.State == EntityState.Added || e.State == EntityState.Modified || e.State == EntityState.Deleted);
        db.ChangeTracker.Clear();
        var despues = await servicio.ObtenerAsync(id);
        Assert.Equal(antes.Nombres, despues.Nombres);
        Assert.Equal(antes.EsCliente, despues.EsCliente);
        Assert.Equal(antes.Versiones.OrderBy(x => x.Key), despues.Versiones.OrderBy(x => x.Key));
        Assert.Empty(despues.Numeros);
        Assert.Empty(despues.Familiares);
    }

    [Theory]
    [InlineData("Otro")]
    [InlineData("Telefono")]
    [InlineData("")]
    [InlineData(null)]
    public async Task Tipo_desconocido_no_se_convierte_en_telefono(string? tipo)
    {
        using var db = CrearDb();
        var id = await SembrarAsync(db, EstadoPrima.Pendiente);
        var servicio = CrearServicio(db, Roles.Administrador);
        var antes = await servicio.ObtenerAsync(id);
        var guardados = db.Guardados;
        var error = await Assert.ThrowsAsync<ValidacionException>(() => servicio.ReemplazarNumerosAsync(id,
            new[] { new NumeroItem(0, "88887777", tipo!, true) }, version: antes.Versiones["datos"]));
        Assert.Contains("numeros[0].tipo", error.Errores.Keys);
        Assert.Equal(guardados, db.Guardados);
        Assert.Equal(antes.Versiones["datos"], (await servicio.ObtenerAsync(id)).Versiones["datos"]);
        Assert.Empty(await db.Numeros.ToListAsync());
    }

    [Theory]
    [InlineData(null)]
    [InlineData(0)]
    public async Task Opcionales_nulos_o_cero_conservan_predeterminados(int? opcional)
    {
        using var db = CrearDb();
        var id = await SembrarAsync(db, EstadoPrima.Pendiente);
        var origen = new OrigenCliente { Codigo = "OR", Nombre = "Referido" };
        db.OrigenesCliente.Add(origen);
        await db.SaveChangesAsync();
        var servicio = CrearServicio(db, Roles.Administrador);
        var numeros = await servicio.ReemplazarNumerosAsync(id,
            new[] { new NumeroItem(0, "88887777", "teléfono", true, opcional) }, version: (await servicio.ObtenerAsync(id)).Versiones["datos"]);
        Assert.Equal(await db.Paises.OrderBy(p => p.Id).Select(p => p.Id).FirstAsync(), numeros[0].PaisId);
        var familiares = await servicio.ReemplazarFamiliaresAsync(id,
            new[] { new FamiliarItem(-1, "Luis", opcional, "88887777", "", "", opcional, opcional) },
            version: (await servicio.ObtenerAsync(id)).Versiones["familiares"]);
        Assert.Null(familiares[0].ParentescoId);
        Assert.Equal(numeros[0].PaisId, familiares[0].TelefonoPaisId);
        var cliente = await servicio.ConvertirEnClienteAsync(id, new ConvertirClienteRequest("", origen.Id, opcional),
            version: (await servicio.ObtenerAsync(id)).Versiones["datos"]);
        Assert.Equal(EstadoCliente.AlDia, cliente.Cliente!.EstadoClienteId);
        await servicio.ActualizarDatosAsync(id, new ActualizarDatosRequest("Ana", "Vargas", "123", "12-12345", "",
            new DateOnly(2026, 10, 13), 1, null, null, null, opcional, opcional), version: cliente.Versiones["datos"]);
        var despues = await servicio.ObtenerAsync(id);
        Assert.Equal(cliente.Cliente, despues.Cliente);
    }

    private static PersonasService CrearServicio(TestDb db, string rol) =>
        new(db, new UsuarioPrueba(rol), new IdentityPrueba(), new FechaPrueba(), new ActualizadorCobros(db, new FechaPrueba()));

    private sealed class UsuarioPrueba(string rol) : IUsuarioActual
    {
        public int? Id => 1;
        public string? Rol => rol;
        public bool EsAdministrador => rol == Roles.Administrador;
    }

    private sealed class FechaPrueba : IFechaActual
    {
        public DateOnly Hoy => new(2026, 10, 9);
        public DateTime AhoraUtc => new(2026, 10, 9, 12, 0, 0, DateTimeKind.Utc);
    }

    private sealed class IdentityPrueba : IIdentityService
    {
        public Task<IReadOnlyDictionary<int, UsuarioDto>> MapaAsync(IEnumerable<int> ids) =>
            Task.FromResult<IReadOnlyDictionary<int, UsuarioDto>>(new Dictionary<int, UsuarioDto>());
        public Task<UsuarioDto?> ObtenerAsync(int id) =>
            Task.FromResult(id == 1 ? new UsuarioDto(1, "Carlos Pérez", "carlos", Domain.Constants.Roles.Administrador) : null);
        public Task<UsuarioDto?> ValidarCredencialesAsync(string usuario, string contrasena) => throw new NotSupportedException();
        public Task<IReadOnlyList<UsuarioDto>> ListarAsync() => throw new NotSupportedException();
        public Task<UsuarioDto> CrearAsync(GuardarUsuarioRequest request) => throw new NotSupportedException();
        public Task<UsuarioDto> ActualizarAsync(int id, GuardarUsuarioRequest request) => throw new NotSupportedException();
        public Task EliminarAsync(int id) => throw new NotSupportedException();
        public IReadOnlyList<RolDto> Roles() => throw new NotSupportedException();
    }

    private sealed class TestDb(DbContextOptions<TestDb> options) : DbContext(options), IApplicationDbContext
    {
        public void DescartarCambios() => ChangeTracker.Clear();
        public DbSet<Persona> Personas => Set<Persona>();
        public DbSet<Cliente> Clientes => Set<Cliente>();
        public DbSet<Venta> Ventas => Set<Venta>();
        public DbSet<Prima> Primas => Set<Prima>();
        public DbSet<Numero> Numeros => Set<Numero>();
        public DbSet<CorreoElectronico> Correos => Set<CorreoElectronico>();
        public DbSet<Finca> Fincas => Set<Finca>();
        public DbSet<PersonaFamiliar> Familiares => Set<PersonaFamiliar>();
        public DbSet<Comentario> Comentarios => Set<Comentario>();
        public DbSet<Vendedor> Vendedores => Set<Vendedor>();
        public DbSet<ProcedenciaVenta> ProcedenciasVenta => Set<ProcedenciaVenta>();
        public DbSet<MetodoVenta> MetodosVenta => Set<MetodoVenta>();
        public DbSet<EstadoPrima> EstadosPrima => Set<EstadoPrima>();
        public DbSet<OrigenCliente> OrigenesCliente => Set<OrigenCliente>();
        public DbSet<EstadoCliente> EstadosCliente => Set<EstadoCliente>();
        public DbSet<Pais> Paises => Set<Pais>();
        public DbSet<Parentesco> Parentescos => Set<Parentesco>();
        public DbSet<ConfiguracionSistema> Configuracion => Set<ConfiguracionSistema>();
        public DbSet<PlanPago> PlanesPago => Set<PlanPago>();
        public DbSet<Cuota> Cuotas => Set<Cuota>();
        public DbSet<Pago> Pagos => Set<Pago>();
        public DbSet<ArchivoPago> ArchivosPago => Set<ArchivoPago>();
        public DbSet<PromesaPago> PromesasPago => Set<PromesaPago>();
        public DbSet<EstadoCuotaPago> EstadosCuotaPago => Set<EstadoCuotaPago>();
        public DbSet<EstadoCuotaVencimiento> EstadosCuotaVencimiento => Set<EstadoCuotaVencimiento>();
        public DbSet<EstadoPromesa> EstadosPromesa => Set<EstadoPromesa>();
        public int Guardados { get; private set; }

        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            foreach (var entry in ChangeTracker.Entries<Persona>().Where(e => e.State == EntityState.Added).ToList())
                foreach (var seccion in VersionSeccion.Secciones)
                    if (!entry.Entity.Versiones.Any(v => v.Seccion == seccion))
                        entry.Entity.Versiones.Add(new VersionSeccion { Seccion = seccion });
            Guardados++;
            return base.SaveChangesAsync(cancellationToken);
        }

        protected override void OnModelCreating(ModelBuilder model)
        {
            model.Entity<VersionSeccion>().HasKey(x => new { x.PersonaId, x.Seccion });
            model.Entity<VersionSeccion>().Property(x => x.Version).IsConcurrencyToken();
            model.Entity<VersionSeccion>().HasOne<Persona>().WithMany(p => p.Versiones)
                .HasForeignKey(x => x.PersonaId).OnDelete(DeleteBehavior.Cascade);
            model.Entity<ConfiguracionSistema>().HasKey(c => c.Clave);
            model.Entity<Persona>().HasOne(p => p.Cliente).WithOne(c => c.Persona)
                .HasForeignKey<Persona>(p => p.ClienteId);
            model.Entity<Persona>().HasMany(p => p.Ventas).WithOne(v => v.Persona)
                .HasForeignKey(v => v.PersonaId);
            model.Entity<Venta>().HasOne(v => v.Prima).WithOne(p => p.Venta)
                .HasForeignKey<Prima>(p => p.VentaId);
            model.Entity<Persona>().HasMany(p => p.Comentarios).WithOne()
                .HasForeignKey(c => c.PersonaId);
            model.Entity<Persona>().Ignore(p => p.EsCliente).Ignore(p => p.NombreCompleto)
                .Ignore(p => p.Venta).Ignore(p => p.Prima).Ignore(p => p.Finca)
                .Ignore(p => p.CorreoPrincipal);
        }
    }
}






