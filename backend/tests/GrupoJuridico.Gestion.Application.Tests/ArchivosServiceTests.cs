using GrupoJuridico.Gestion.Application.Archivos;
using GrupoJuridico.Gestion.Application.Catalogos;
using GrupoJuridico.Gestion.Application.Cobros;
using GrupoJuridico.Gestion.Application.Common.Exceptions;
using GrupoJuridico.Gestion.Application.Common.Interfaces;
using GrupoJuridico.Gestion.Application.Personas;
using GrupoJuridico.Gestion.Application.Usuarios;
using GrupoJuridico.Gestion.Domain.Constants;
using GrupoJuridico.Gestion.Domain.Entities;
using GrupoJuridico.Gestion.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace GrupoJuridico.Gestion.Application.Tests;

/// <summary>Archivos del expediente con permisos por tipo de documento y rol (modelo productivo sobre SQLite).</summary>
public sealed class ArchivosServiceTests : IDisposable
{
    private static readonly DateOnly Hoy = new(2026, 10, 20);
    private readonly SqliteConnection _conexion = new("Data Source=:memory:");
    private readonly DbContextOptions<ApplicationDbContext> _opciones;
    private readonly AlmacenMemoria _almacen = new();
    private readonly Dictionary<string, int> _tipo = new();

    public ArchivosServiceTests()
    {
        _conexion.Open();
        _opciones = new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(_conexion).Options;
        using var db = new ApplicationDbContext(_opciones);
        db.Database.EnsureCreated();
        EstadosClientePrueba.Agregar(db);
        db.OrigenesCliente.Add(new OrigenCliente { Codigo = "P1", Nombre = "Origen" });
        db.EstadosPrima.AddRange(
            new EstadoPrima { Id = 1, Nombre = "Pendiente" }, new EstadoPrima { Id = 2, Nombre = "Incompleta" }, new EstadoPrima { Id = 3, Nombre = "Pagada" });
        // Los mismos tipos y permisos iniciales que carga DbSeeder (diseño).
        const string A = Roles.Administrador, V = Roles.AsistenteVentas, C = Roles.Cobros;
        foreach (var (nombre, subir, ver) in new (string, string[], string[])[]
        {
            ("Poderes", new[] { A }, new[] { A, V }),
            ("Contrato", new[] { A, V }, new[] { A, V, C }),
            ("Comprobantes de pago", new[] { A, C }, new[] { A, C }),
            ("Gestión legal", new[] { A }, new[] { A }),
        })
        {
            var t = new TipoDocumento { Nombre = nombre, Codigo = nombre == "Comprobantes de pago" ? TipoDocumento.CodigoComprobantesPago : "" };
            foreach (var rol in Roles.Todos)
                t.Permisos.Add(new PermisoDocumento { Rol = rol, PuedeSubir = subir.Contains(rol), PuedeVer = ver.Contains(rol) });
            db.TiposDocumento.Add(t);
            db.SaveChanges();
            _tipo[nombre] = t.Id;
        }
    }

    public void Dispose() => _conexion.Dispose();

    private ApplicationDbContext Db() => new(_opciones);
    private ArchivosService Servicio(ApplicationDbContext db, string rol) => new(db, new Usuario(rol), new Fecha(), _almacen);

    private int CrearPersona(bool cliente = true, bool primaPagada = false)
    {
        using var db = Db();
        var prima = new Prima { Monto = 100m, MontoCancelado = primaPagada ? 100m : 0m };
        prima.Recalcular();
        var p = new Persona { Nombres = "Ana", Apellidos = "Vargas", FechaIngreso = Hoy };
        p.Ventas.Add(new Venta { Monto = 1000m, Prima = prima });
        if (cliente) p.Cliente = new Cliente { OrigenClienteId = 1, EstadoClienteCalculadoId = 1 };
        db.Personas.Add(p);
        db.SaveChanges();
        return p.Id;
    }

    private static ArchivoSubido Archivo(string nombre = "contrato.pdf", long tamano = 4) =>
        new(nombre, "application/octet-stream", tamano, new MemoryStream(new byte[] { 1, 2, 3, 4 }));

    private async Task<ArchivosPersonaDto> SubirAsync(int personaId, string tipo, string rol = Roles.Administrador, params ArchivoSubido[] archivos)
    {
        using var db = Db();
        return await Servicio(db, rol).SubirAsync(personaId, _tipo[tipo], archivos.Length == 0 ? new[] { Archivo() } : archivos);
    }

    [Fact]
    public async Task Cada_rol_ve_y_sube_solo_los_tipos_permitidos()
    {
        var id = CrearPersona();
        await SubirAsync(id, "Poderes");
        await SubirAsync(id, "Contrato");
        await SubirAsync(id, "Comprobantes de pago");
        await SubirAsync(id, "Gestión legal");

        async Task<(string[] Ver, string[] Subir)> Vista(string rol)
        {
            using var db = Db();
            var dto = await Servicio(db, rol).ListarAsync(id);
            string Nombre(int tipoId) => _tipo.Single(t => t.Value == tipoId).Key;
            return (dto.Archivos.Select(a => Nombre(a.TipoDocumentoId)).OrderBy(x => x).ToArray(), dto.TiposSubir.Select(Nombre).OrderBy(x => x).ToArray());
        }

        var admin = await Vista(Roles.Administrador);
        Assert.Equal(4, admin.Ver.Length);
        Assert.Equal(4, admin.Subir.Length);

        var ventas = await Vista(Roles.AsistenteVentas);
        Assert.Equal(new[] { "Contrato", "Poderes" }, ventas.Ver);
        Assert.Equal(new[] { "Contrato" }, ventas.Subir);

        var cobros = await Vista(Roles.Cobros);
        Assert.Equal(new[] { "Comprobantes de pago", "Contrato" }, cobros.Ver);
        Assert.Equal(new[] { "Comprobantes de pago" }, cobros.Subir);
    }

    [Fact]
    public async Task Subir_exige_cliente_tipo_y_permiso()
    {
        var prospecto = CrearPersona(cliente: false);
        var ex = await Assert.ThrowsAsync<ValidacionException>(() => SubirAsync(prospecto, "Contrato"));
        Assert.Contains("cliente oficial", ex.Errores.Values.Single()[0]);

        var id = CrearPersona();
        using (var db = Db())
        {
            var sinTipo = await Assert.ThrowsAsync<ValidacionException>(() => Servicio(db, Roles.Administrador).SubirAsync(id, 0, new[] { Archivo() }));
            Assert.Equal("Seleccione el tipo de documento antes de subir.", sinTipo.Errores["tipoDocumentoId"][0]);
        }
        await Assert.ThrowsAsync<ProhibidoException>(() => SubirAsync(id, "Poderes", Roles.Cobros));
        await Assert.ThrowsAsync<ProhibidoException>(() => SubirAsync(id, "Poderes", Roles.AsistenteVentas));
        Assert.Empty(_almacen.Archivos);
    }

    [Theory]
    [InlineData("virus.exe", 4)]
    [InlineData("contrato.pdf", 0)]
    [InlineData("grande.pdf", 10 * 1024 * 1024 + 1)]
    public async Task Solo_se_aceptan_pdf_imagenes_word_y_excel_de_hasta_10_mb(string nombre, long tamano)
    {
        var id = CrearPersona();
        var ex = await Assert.ThrowsAsync<ValidacionException>(() => SubirAsync(id, "Contrato", Roles.Administrador, Archivo(nombre, tamano)));
        Assert.Contains("de hasta 10 MB", ex.Errores["archivos"][0]);
        Assert.Empty(_almacen.Archivos);
    }

    [Fact]
    public async Task Un_envio_tiene_limite_de_cantidad_y_de_tamano_total()
    {
        var id = CrearPersona();
        var muchos = Enumerable.Range(1, ArchivosService.MaxArchivosPorEnvio + 1).Select(i => Archivo($"a{i}.pdf")).ToArray();
        await Assert.ThrowsAsync<ValidacionException>(() => SubirAsync(id, "Contrato", Roles.Administrador, muchos));
        var pesados = Enumerable.Range(1, 3).Select(i => Archivo($"a{i}.pdf", 9 * 1024 * 1024)).ToArray();
        var ex = await Assert.ThrowsAsync<ValidacionException>(() => SubirAsync(id, "Contrato", Roles.Administrador, pesados));
        Assert.Contains("25 MB", ex.Errores["archivos"][0]);
    }

    [Fact]
    public async Task Se_suben_varios_archivos_a_la_vez_con_nombre_seguro()
    {
        var id = CrearPersona();
        var dto = await SubirAsync(id, "Contrato", Roles.AsistenteVentas,
            Archivo("contrato firmado.pdf"), Archivo("anexo.DOCX"), Archivo(@"C:\carpeta\foto.jpg"));
        Assert.Equal(3, dto.Archivos.Count);
        Assert.Equal(new[] { "anexo.DOCX", "contrato firmado.pdf", "foto.jpg" }, dto.Archivos.Select(a => a.Nombre).OrderBy(x => x));
        Assert.All(dto.Archivos, a => Assert.Equal(Hoy, a.FechaCarga));
        Assert.Equal(3, _almacen.Archivos.Count);
        using var db = Db();
        Assert.Equal("application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            (await db.ArchivosPersona.SingleAsync(a => a.Nombre == "anexo.DOCX")).TipoContenido);
    }

    [Fact]
    public async Task Si_falla_un_archivo_no_quedan_archivos_guardados()
    {
        var id = CrearPersona();
        _almacen.FallarEn = 2;
        await Assert.ThrowsAsync<IOException>(() => SubirAsync(id, "Contrato", Roles.Administrador, Archivo("a.pdf"), Archivo("b.pdf")));
        Assert.Empty(_almacen.Archivos);
        using var db = Db();
        Assert.Equal(0, await db.ArchivosPersona.CountAsync());
    }

    [Fact]
    public async Task Descargar_y_eliminar_respetan_los_permisos()
    {
        var id = CrearPersona();
        var poder = (await SubirAsync(id, "Poderes")).Archivos.Single();
        var comprobante = (await SubirAsync(id, "Comprobantes de pago")).Archivos.Single(a => a.TipoDocumentoId == _tipo["Comprobantes de pago"]);

        using (var db = Db())
        {
            // Asistente ve Poderes pero no lo puede eliminar; no ve Comprobantes.
            var ventas = Servicio(db, Roles.AsistenteVentas);
            var (contenido, nombre, _) = await ventas.DescargarAsync(poder.Id);
            using (contenido) Assert.Equal("contrato.pdf", nombre);
            await Assert.ThrowsAsync<ProhibidoException>(() => ventas.EliminarAsync(poder.Id));
            await Assert.ThrowsAsync<ProhibidoException>(() => ventas.DescargarAsync(comprobante.Id));
            Assert.False((await ventas.ListarAsync(id)).Archivos.Single().PuedeEliminar);
        }
        using (var db = Db())
        {
            var cobros = Servicio(db, Roles.Cobros);
            Assert.True((await cobros.ListarAsync(id)).Archivos.Single().PuedeEliminar);
            var dto = await cobros.EliminarAsync(comprobante.Id);
            Assert.Empty(dto.Archivos);
        }
        Assert.Single(_almacen.Archivos);
    }

    [Fact]
    public async Task El_asistente_no_accede_a_archivos_de_personas_con_prima_pagada()
    {
        var id = CrearPersona(primaPagada: true);
        await SubirAsync(id, "Contrato");
        using var db = Db();
        await Assert.ThrowsAsync<ProhibidoException>(() => Servicio(db, Roles.AsistenteVentas).ListarAsync(id));
        await Assert.ThrowsAsync<ProhibidoException>(() => SubirAsync(id, "Contrato", Roles.AsistenteVentas));
    }

    [Fact]
    public async Task La_matriz_de_permisos_solo_la_cambia_el_administrador_y_subir_implica_ver()
    {
        using (var db = Db())
        {
            await Assert.ThrowsAsync<ProhibidoException>(() => Servicio(db, Roles.Cobros).PermisosDocumentoAsync());
            await Assert.ThrowsAsync<ProhibidoException>(() =>
                Servicio(db, Roles.Cobros).GuardarPermisoAsync(new(_tipo["Poderes"], Roles.Cobros, true, true)));
            await Assert.ThrowsAsync<ValidacionException>(() =>
                Servicio(db, Roles.Administrador).GuardarPermisoAsync(new(_tipo["Poderes"], "Legal", true, false)));

            var guardado = await Servicio(db, Roles.Administrador).GuardarPermisoAsync(new(_tipo["Poderes"], Roles.Cobros, false, true));
            Assert.True(guardado.PuedeVer);
            Assert.True(guardado.PuedeSubir);
            Assert.Equal(12, (await Servicio(db, Roles.Administrador).PermisosDocumentoAsync()).Count);
        }
        var id = CrearPersona();
        var dto = await SubirAsync(id, "Poderes", Roles.Cobros);
        Assert.Single(dto.Archivos);
    }

    [Fact]
    public async Task Un_tipo_nuevo_empieza_solo_con_el_administrador_y_no_se_borra_si_tiene_archivos()
    {
        using var db = Db();
        var catalogos = new CatalogosService(db, new SinUsuarios(), new ActualizadorCobros(db, new Fecha()));
        var nuevo = await catalogos.CrearAsync("tipos-documento", new GuardarCatalogoItemRequest("Escrituras", null, null));
        Assert.StartsWith("TD-", nuevo.Codigo);
        var permisos = await db.PermisosDocumento.Where(p => p.TipoDocumentoId == nuevo.Id).ToListAsync();
        var admin = Assert.Single(permisos);
        Assert.Equal(Roles.Administrador, admin.Rol);
        Assert.True(admin.PuedeSubir);
        Assert.Contains((await catalogos.ObtenerAsync()).TiposDocumento, t => t.Nombre == "Escrituras");

        var id = CrearPersona();
        await SubirAsync(id, "Contrato");
        var ex = await Assert.ThrowsAsync<ValidacionException>(() => catalogos.EliminarAsync("tipos-documento", _tipo["Contrato"]));
        Assert.Contains("1 archivo(s)", ex.Errores["id"][0]);
        await catalogos.EliminarAsync("tipos-documento", nuevo.Id);
        var sistema = await Assert.ThrowsAsync<ValidacionException>(() => catalogos.EliminarAsync("tipos-documento", _tipo["Comprobantes de pago"]));
        Assert.Contains("comprobantes de pago de Cobros", sistema.Errores["id"][0]);
        await catalogos.ActualizarAsync("tipos-documento", _tipo["Comprobantes de pago"], new GuardarCatalogoItemRequest("Recibos de pago", null, null));
        Assert.False(await db.TiposDocumento.AnyAsync(t => t.Id == nuevo.Id));
        Assert.False(await db.PermisosDocumento.AnyAsync(p => p.TipoDocumentoId == nuevo.Id));
    }

    [Fact]
    public async Task Eliminar_la_persona_borra_sus_archivos_guardados()
    {
        var id = CrearPersona();
        await SubirAsync(id, "Contrato", Roles.Administrador, Archivo("a.pdf"), Archivo("b.pdf"));
        var otra = CrearPersona();
        await SubirAsync(otra, "Contrato");
        Assert.Equal(3, _almacen.Archivos.Count);

        using var db = Db();
        var personas = new PersonasService(db, new Usuario(Roles.Administrador), new SinUsuarios(), new Fecha(), new ActualizadorCobros(db, new Fecha()), _almacen);
        await personas.EliminarAsync(id);
        Assert.Single(_almacen.Archivos);
        Assert.Equal(1, await db.ArchivosPersona.CountAsync());
    }

    // ---------- Dobles de prueba ----------

    private sealed class AlmacenMemoria : IAlmacenArchivos
    {
        public readonly Dictionary<string, byte[]> Archivos = new();
        public int FallarEn;
        private int _guardados;

        public async Task<string> GuardarAsync(Stream contenido, string extension, CancellationToken ct = default)
        {
            if (++_guardados == FallarEn) throw new IOException("Falla simulada del almacenamiento.");
            using var copia = new MemoryStream();
            await contenido.CopyToAsync(copia, ct);
            var clave = "2026/10/" + Guid.NewGuid().ToString("N") + extension.ToLowerInvariant();
            Archivos[clave] = copia.ToArray();
            return clave;
        }

        public Task<Stream?> AbrirAsync(string clave, CancellationToken ct = default) =>
            Task.FromResult<Stream?>(Archivos.TryGetValue(clave, out var datos) ? new MemoryStream(datos) : null);

        public Task EliminarAsync(string clave, CancellationToken ct = default)
        {
            Archivos.Remove(clave);
            return Task.CompletedTask;
        }
    }

    private sealed class Fecha : IFechaActual
    {
        public DateOnly Hoy => ArchivosServiceTests.Hoy;
        public DateTime AhoraUtc => Hoy.ToDateTime(new TimeOnly(12, 0), DateTimeKind.Utc);
    }

    private sealed class Usuario(string rol) : IUsuarioActual
    {
        public int? Id => 1;
        public string? Rol => rol;
        public bool EsAdministrador => rol == Roles.Administrador;
    }

    private sealed class SinUsuarios : IIdentityService
    {
        public Task<IReadOnlyDictionary<int, UsuarioDto>> MapaAsync(IEnumerable<int> ids) =>
            Task.FromResult<IReadOnlyDictionary<int, UsuarioDto>>(new Dictionary<int, UsuarioDto>());
        public Task<UsuarioDto?> ObtenerAsync(int id) => Task.FromResult<UsuarioDto?>(null);
        public Task<UsuarioDto?> ValidarCredencialesAsync(string usuario, string contrasena) => throw new NotSupportedException();
        public Task<IReadOnlyList<UsuarioDto>> ListarAsync() => throw new NotSupportedException();
        public Task<UsuarioDto> CrearAsync(GuardarUsuarioRequest request) => throw new NotSupportedException();
        public Task<UsuarioDto> ActualizarAsync(int id, GuardarUsuarioRequest request) => throw new NotSupportedException();
        public Task EliminarAsync(int id) => throw new NotSupportedException();
        public IReadOnlyList<RolDto> Roles() => new[] { new RolDto(1, Domain.Constants.Roles.Administrador) };
    }
}
