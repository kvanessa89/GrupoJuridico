using GrupoJuridico.Gestion.Application.Cobros;
using GrupoJuridico.Gestion.Application.Common;
using GrupoJuridico.Gestion.Application.Common.Exceptions;
using GrupoJuridico.Gestion.Application.Common.Interfaces;
using GrupoJuridico.Gestion.Domain.Common;
using GrupoJuridico.Gestion.Domain.Constants;
using GrupoJuridico.Gestion.Domain.Entities;
using GrupoJuridico.Gestion.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace GrupoJuridico.Gestion.Application.Personas;

/// <summary>Ficha de la persona: lectura, registro de prospectos, autoguardado y conversión a cliente.</summary>
public class PersonasService
{
    private readonly IApplicationDbContext _db;
    private readonly IUsuarioActual _usuario;
    private readonly IIdentityService _identity;
    private readonly IFechaActual _fecha;

    private readonly ActualizarDatosValidator _datosValidator = new();
    private readonly CrearProspectoValidator _crearValidator = new();
    private readonly ActualizarVentaValidator _ventaValidator = new();
    private readonly ActualizarPrimaValidator _primaValidator = new();
    private readonly ConvertirClienteValidator _convertirValidator = new();
    private readonly ComentarioValidator _comentarioValidator = new();
    private readonly FamiliarItemValidator _familiarValidator = new();

    private readonly ActualizadorCobros _cobros;

    public PersonasService(IApplicationDbContext db, IUsuarioActual usuario, IIdentityService identity, IFechaActual fecha, ActualizadorCobros cobros)
    {
        _db = db;
        _usuario = usuario;
        _identity = identity;
        _fecha = fecha;
        _cobros = cobros;
    }

    public Guid VersionGuardada { get; private set; }

    private async Task PrepararVersionAsync(Persona persona, string seccion, Guid? esperada)
    {
        if (esperada == null) throw new VersionRequeridaException();
        var version = persona.Versiones.SingleOrDefault(v => v.Seccion == seccion);
        if (version == null) throw new ConflictoVersionException();
        if (version.Version != esperada.Value)
        {
            // Quién hizo el último cambio de la sección, para el aviso del frontend.
            var autor = version.ModificadoPorId is int autorId ? await _identity.ObtenerAsync(autorId) : null;
            throw new ConflictoVersionException(version.ModificadoPorId, autor?.Nombre, version.ModificadoEn);
        }
        // EF conserva la versión original para el WHERE del UPDATE. El cambio de versión
        // y los datos se guardan en la misma transacción; un conflicto revierte todo.
        version.Version = Guid.NewGuid();
        VersionGuardada = version.Version;
    }

    private static void ValidarIdCatalogo(int? id, string campo)
    {
        if (id is < 0) throw new ValidacionException(campo, "El identificador no puede ser negativo.");
    }

    /// <summary>
    /// Resuelve el país de un teléfono: valida el id recibido o, si viene nulo,
    /// toma el país por defecto (el primero del catálogo).
    /// </summary>
    private async Task<Func<int?, string, string?, string, int>> PaisesAsync(CancellationToken ct)
    {
        var paises = await _db.Paises.OrderBy(x => x.Id).Select(x => new { x.Id, x.Codigo }).ToListAsync(ct);
        return (id, campoPais, numero, campoNumero) =>
        {
            ValidarIdCatalogo(id, campoPais);
            var pais = id is > 0 ? paises.FirstOrDefault(p => p.Id == id.Value) : paises.FirstOrDefault();
            if (pais == null)
                throw new ValidacionException(campoPais, id is > 0
                    ? "El país seleccionado ya no existe. Recargue la página y elija otro."
                    : "No hay países configurados.");
            Validacion.Telefono(numero, pais.Codigo, campoNumero);
            return pais.Id;
        };
    }

    private static void ValidarTelefonosFamiliares(IEnumerable<FamiliarItem> items, Func<int?, string, string?, string, int> pais)
    {
        var indice = 0;
        foreach (var i in items)
        {
            if (!Validacion.Vacio(i.Telefono))
                pais(i.TelefonoPaisId, $"familiares[{indice}].telefonoPaisId", i.Telefono, $"familiares[{indice}].telefono");
            if (!Validacion.Vacio(i.Whatsapp))
                pais(i.WhatsappPaisId, $"familiares[{indice}].whatsappPaisId", i.Whatsapp, $"familiares[{indice}].whatsapp");
            indice++;
        }
    }

    /// <summary>Cada familiar con parentesco debe usar uno del catálogo.</summary>
    private async Task ValidarParentescosAsync(IEnumerable<FamiliarItem> items, CancellationToken ct)
    {
        var indice = 0;
        foreach (var i in items)
        {
            ValidarIdCatalogo(i.ParentescoId, $"familiares[{indice}].parentescoId");
            ValidarIdCatalogo(i.TelefonoPaisId, $"familiares[{indice}].telefonoPaisId");
            ValidarIdCatalogo(i.WhatsappPaisId, $"familiares[{indice}].whatsappPaisId");
            indice++;
        }
        var usados = items.Where(i => i.ParentescoId is > 0).Select(i => i.ParentescoId!.Value).Distinct().ToList();
        if (usados.Count == 0) return;
        var existentes = (await _db.Parentescos.Where(x => usados.Contains(x.Id)).Select(x => x.Id).ToListAsync(ct)).ToHashSet();
        indice = 0;
        foreach (var i in items)
        {
            if (i.ParentescoId is > 0 && !existentes.Contains(i.ParentescoId.Value))
                throw new ValidacionException($"familiares[{indice}].parentescoId", "El parentesco seleccionado ya no existe. Recargue la página y elija otro.");
            indice++;
        }
    }

    /// <summary>
    /// El id elegido sigue en el catálogo. Un vendedor, procedencia o método puede eliminarse
    /// mientras otra sesión tiene la ficha abierta: sin esto, el guardado violaría la FK (500).
    /// </summary>
    private async Task ValidarCatalogoAsync<T>(IQueryable<T> catalogo, int? id, string campo, string mensaje, CancellationToken ct)
        where T : Catalogo
    {
        ValidarIdCatalogo(id, campo);
        if (id is > 0 && !await catalogo.AnyAsync(x => x.Id == id, ct))
            throw new ValidacionException(campo, mensaje);
    }

    private static NumeroDto NumeroDe(Numero n) => new(n.Id, n.Valor, TipoTexto(n.Tipo), n.Principal, n.PaisId);

    private static FamiliarDto FamiliarDe(PersonaFamiliar f) =>
        new(f.Id, f.NombreCompleto, f.ParentescoId, f.Telefono, f.CorreoElectronico, f.Whatsapp, f.TelefonoPaisId, f.WhatsappPaisId);

    private static void ValidarCantidad(int cantidad, int maximo, string campo, string etiqueta)
    {
        if (cantidad > maximo)
            throw new ValidacionException(campo, $"Se permiten como máximo {maximo} {etiqueta} por persona.");
    }

    /// <summary>Un ID positivo identifica una fila existente de esta persona; nunca es un alta implícita.</summary>
    private static void ValidarIdsPropios(IEnumerable<int> ids, IEnumerable<int> propios, string campo)
    {
        var permitidos = propios.ToHashSet();
        var vistos = new HashSet<int>();
        var indice = 0;
        foreach (var id in ids)
        {
            if (id > 0 && !permitidos.Contains(id))
                throw new ValidacionException($"{campo}[{indice}].id",
                    "El registro indicado no existe entre los contactos de esta persona. Recargá la sección antes de guardar.");
            if (id > 0 && !vistos.Add(id))
                throw new ValidacionException($"{campo}[{indice}].id",
                    "El mismo registro no puede aparecer más de una vez en la lista.");
            indice++;
        }
    }

    public static string TipoTexto(TipoNumero t) => t == TipoNumero.WhatsApp ? "WhatsApp" : "Teléfono";
    public static TipoNumero TipoDesde(string? t, string campo = "tipo")
    {
        if (string.Equals(t?.Trim(), "WhatsApp", StringComparison.OrdinalIgnoreCase)) return TipoNumero.WhatsApp;
        if (string.Equals(t?.Trim(), "Teléfono", StringComparison.OrdinalIgnoreCase)) return TipoNumero.Telefono;
        throw new ValidacionException(campo, "Seleccione Teléfono o WhatsApp.");
    }

    private async Task<Persona> CargarAsync(int id, CancellationToken ct)
    {
        var persona = await _db.Personas
            .Include(p => p.Cliente)
            .Include(p => p.Ventas).ThenInclude(v => v.Prima)
            .Include(p => p.Numeros)
            .Include(p => p.Correos)
            .Include(p => p.Fincas)
            .Include(p => p.Familiares)
            .Include(p => p.Comentarios)
            .Include(p => p.Versiones)
            .AsSingleQuery()
            .FirstOrDefaultAsync(p => p.Id == id, ct)
        ?? throw new NoEncontradoException("Persona", id);

        VerificarAcceso(persona);
        return persona;
    }

    private void VerificarAcceso(Persona persona)
    {
        if (!AccesoPermitido(_usuario.Rol, persona))
            throw new ProhibidoException("No tiene permiso para acceder a esta persona.");
    }

    /// <summary>El Asistente de Ventas no accede a personas con la prima pagada (requiere la prima cargada).</summary>
    public static bool AccesoPermitido(string? rol, Persona persona) =>
        !(rol == Roles.AsistenteVentas && persona.Prima?.EstadoPrimaId == EstadoPrima.Pagada);

    public async Task<PersonaDetalleDto> ObtenerAsync(int id, CancellationToken ct = default)
    {
        // Antes de cargar la ficha se pone al día el plan de pago: así el estado del cliente y la versión
        // de "datos" que recibe la ficha ya son los vigentes (sin conflictos falsos al autoguardar).
        var acceso = await _db.Personas.AsNoTracking().Include(x => x.Ventas).ThenInclude(v => v.Prima)
            .FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw new NoEncontradoException("Persona", id);
        VerificarAcceso(acceso);
        if (acceso.ClienteId is int clienteId) await _cobros.ActualizarClienteAsync(clienteId, ct);
        var p = await CargarAsync(id, ct);
        return await CrearDetalleAsync(p);
    }

    private async Task<PersonaDetalleDto> CrearDetalleAsync(Persona p)
    {
        var autores = await _identity.MapaAsync(p.Comentarios.Select(c => c.UsuarioId).Distinct());
        var v = p.Venta;
        var pr = p.Prima;
        return new PersonaDetalleDto(
            p.Id, p.EsCliente, p.Expediente, p.Nombres, p.Apellidos, p.Cedula,
            p.Finca?.Numero ?? "", p.FechaIngreso, p.VendedorId,
            p.NumeroPrincipal(TipoNumero.Telefono)?.Valor ?? "",
            p.NumeroPrincipal(TipoNumero.WhatsApp)?.Valor ?? "",
            p.CorreoPrincipal?.Correo ?? "",
            v == null ? null : new VentaDto(v.Id, v.ProcedenciaVentaId, v.MetodoVentaId, v.Monto, v.Notas),
            pr == null ? null : new PrimaDto(pr.Id, pr.Monto, pr.MontoCancelado, pr.SaldoPendiente, pr.FechaEstimadaPago, pr.FechaPago, pr.EstadoPrimaId),
            p.Cliente == null ? null : new ClienteDto(p.Cliente.Id, p.Cliente.OrigenClienteId, p.Cliente.EstadoClienteId, p.Cliente.Notas),
            p.Numeros.OrderBy(n => n.Id).Select(NumeroDe).ToList(),
            p.Correos.OrderBy(c => c.Id).Select(c => new CorreoDto(c.Id, c.Correo, c.Principal)).ToList(),
            p.Familiares.OrderBy(f => f.Id).Select(FamiliarDe).ToList(),
            p.Comentarios.OrderByDescending(c => c.Fecha).ThenByDescending(c => c.Id).Select(c =>
            {
                autores.TryGetValue(c.UsuarioId, out var u);
                return new ComentarioDto(c.Id, c.UsuarioId, u?.Nombre ?? "Usuario eliminado", u?.Rol, c.Texto,
                    DateTime.SpecifyKind(c.Fecha, DateTimeKind.Utc), c.UsuarioId == _usuario.Id);
            }).ToList(), p.Versiones.ToDictionary(x => x.Seccion, x => x.Version),
            p.NumeroPrincipal(TipoNumero.Telefono)?.PaisId,
            p.NumeroPrincipal(TipoNumero.WhatsApp)?.PaisId);
    }

    public async Task<PersonaDetalleDto> CrearProspectoAsync(CrearProspectoRequest r, CancellationToken ct = default)
    {
        await _crearValidator.ValidarAsync(r, ct);
        ValidarIdCatalogo(r.TelefonoPrincipalPaisId, nameof(r.TelefonoPrincipalPaisId));
        ValidarIdCatalogo(r.WhatsappPrincipalPaisId, nameof(r.WhatsappPrincipalPaisId));
        ValidarCantidad((r.Familiares ?? Array.Empty<FamiliarItem>()).Count(f => !FamiliarVacio(f)), Persona.MaxFamiliares, "familiares", "familiares");
        await ValidarParentescosAsync(r.Familiares ?? Array.Empty<FamiliarItem>(), ct);
        await ValidarCatalogoAsync(_db.Vendedores, r.VendedorId, nameof(r.VendedorId), "El vendedor seleccionado ya no existe. Recargue la página y elija otro.", ct);
        await ValidarCatalogoAsync(_db.ProcedenciasVenta, r.ProcedenciaVentaId, nameof(r.ProcedenciaVentaId), "La procedencia seleccionada ya no existe. Recargue la página y elija otra.", ct);
        await ValidarCatalogoAsync(_db.MetodosVenta, r.MetodoVentaId, nameof(r.MetodoVentaId), "El método seleccionado ya no existe. Recargue la página y elija otro.", ct);
        var pais = await PaisesAsync(ct);
        if (!Validacion.Vacio(r.TelefonoPrincipal))
            pais(r.TelefonoPrincipalPaisId, nameof(r.TelefonoPrincipalPaisId), r.TelefonoPrincipal, nameof(r.TelefonoPrincipal));
        if (!Validacion.Vacio(r.WhatsappPrincipal))
            pais(r.WhatsappPrincipalPaisId, nameof(r.WhatsappPrincipalPaisId), r.WhatsappPrincipal, nameof(r.WhatsappPrincipal));
        ValidarTelefonosFamiliares(r.Familiares ?? Array.Empty<FamiliarItem>(), pais);
        var persona = new Persona
        {
            Nombres = r.Nombres!.Trim(),
            Apellidos = r.Apellidos!.Trim(),
            Cedula = Validacion.SoloDigitos(r.Cedula),
            Expediente = r.Expediente?.Trim() ?? "",
            FechaIngreso = r.FechaIngreso!.Value,
            VendedorId = r.VendedorId,
            FechaCreacion = _fecha.AhoraUtc
        };
        persona.Fincas.Add(new Finca { Numero = r.Finca!.Trim() });
        var telefono = Validacion.SoloDigitos(r.TelefonoPrincipal);
        var whatsapp = Validacion.SoloDigitos(r.WhatsappPrincipal);
        if (telefono.Length > 0)
            persona.Numeros.Add(new Numero { PaisId = pais(r.TelefonoPrincipalPaisId, nameof(r.TelefonoPrincipalPaisId), r.TelefonoPrincipal, nameof(r.TelefonoPrincipal)), Valor = telefono, Tipo = TipoNumero.Telefono, Principal = true });
        if (whatsapp.Length > 0)
            persona.Numeros.Add(new Numero { PaisId = pais(r.WhatsappPrincipalPaisId, nameof(r.WhatsappPrincipalPaisId), r.WhatsappPrincipal, nameof(r.WhatsappPrincipal)), Valor = whatsapp, Tipo = TipoNumero.WhatsApp, Principal = true });
        if (!Validacion.Vacio(r.CorreoPrincipal))
            persona.Correos.Add(new CorreoElectronico { Correo = r.CorreoPrincipal!.Trim(), Principal = true });
        foreach (var f in (r.Familiares ?? Array.Empty<FamiliarItem>()).Where(f => !FamiliarVacio(f)))
            persona.Familiares.Add(NuevoFamiliar(f, pais));

        var prima = new Prima
        {
            Monto = r.MontoPrima,
            MontoCancelado = r.MontoCancelado,
            FechaEstimadaPago = r.FechaEstimadaPago,
            FechaPago = r.FechaPago
        };
        prima.Recalcular();
        persona.Ventas.Add(new Venta
        {
            ProcedenciaVentaId = r.ProcedenciaVentaId,
            MetodoVentaId = r.MetodoVentaId,
            Monto = r.MontoVenta,
            Notas = r.NotasVenta?.Trim() ?? "",
            Prima = prima
        });

        _db.Personas.Add(persona);
        await _db.SaveChangesAsync(ct);
        // Devuelve el resultado de la creación, incluso si la prima nace pagada.
        // Las consultas y modificaciones posteriores pasan por CargarAsync.
        return await CrearDetalleAsync(persona);
    }

    public async Task ActualizarDatosAsync(int id, ActualizarDatosRequest r, CancellationToken ct = default, Guid? version = null)
    {
        var p = await CargarAsync(id, ct);
        await _datosValidator.ValidarAsync(r, ct);
        ValidarIdCatalogo(r.TelefonoPrincipalPaisId, nameof(r.TelefonoPrincipalPaisId));
        ValidarIdCatalogo(r.WhatsappPrincipalPaisId, nameof(r.WhatsappPrincipalPaisId));
        ValidarIdCatalogo(r.EstadoClienteId, nameof(r.EstadoClienteId));
        ValidarIdCatalogo(r.OrigenClienteId, nameof(r.OrigenClienteId));
        // Nulos conservan contactos existentes; cadenas vacías solicitan quitarlos.
        var telefono = r.TelefonoPrincipal == null ? p.NumeroPrincipal(TipoNumero.Telefono)?.Valor : r.TelefonoPrincipal;
        var whatsapp = r.WhatsappPrincipal == null ? p.NumeroPrincipal(TipoNumero.WhatsApp)?.Valor : r.WhatsappPrincipal;
        if ((r.TelefonoPrincipal != null || r.WhatsappPrincipal != null || !p.EsCliente)
            && Validacion.SoloDigitos(telefono).Length == 0 && Validacion.SoloDigitos(whatsapp).Length == 0)
            throw new ValidacionException("telefonoPrincipal", CrearProspectoValidator.MensajeContacto);
        var nombres = r.Nombres?.Trim() ?? "";
        var apellidos = r.Apellidos?.Trim() ?? "";
        if (nombres.Length > Persona.MaxNombre)
            throw new ValidacionException(nameof(r.Nombres), $"El nombre admite como máximo {Persona.MaxNombre} caracteres.");
        if (apellidos.Length > Persona.MaxNombre)
            throw new ValidacionException(nameof(r.Apellidos), $"Los apellidos admiten como máximo {Persona.MaxNombre} caracteres.");
        var cedula = Validacion.SoloDigitos(r.Cedula);
        if (cedula.Length > Persona.MaxCedula)
            throw new ValidacionException(nameof(r.Cedula), $"La cédula admite como máximo {Persona.MaxCedula} dígitos.");
        var correo = r.CorreoPrincipal == null ? null : Validacion.Correo(r.CorreoPrincipal, nameof(r.CorreoPrincipal), Persona.MaxCorreo);
        // Después del control de acceso y antes de tomar la versión.
        await ValidarCatalogoAsync(_db.Vendedores, r.VendedorId, nameof(r.VendedorId), "El vendedor seleccionado ya no existe. Recargue la página y elija otro.", ct);
        var pais = await PaisesAsync(ct);
        if (r.TelefonoPrincipal != null)
            pais(r.TelefonoPrincipalPaisId, nameof(r.TelefonoPrincipalPaisId), r.TelefonoPrincipal, nameof(r.TelefonoPrincipal));
        if (r.WhatsappPrincipal != null)
            pais(r.WhatsappPrincipalPaisId, nameof(r.WhatsappPrincipalPaisId), r.WhatsappPrincipal, nameof(r.WhatsappPrincipal));
        await PrepararVersionAsync(p, "datos", version);
        p.Nombres = nombres;
        p.Apellidos = apellidos;
        p.Cedula = cedula;
        p.Expediente = r.Expediente?.Trim() ?? "";
        p.FechaIngreso = r.FechaIngreso;
        p.VendedorId = r.VendedorId is > 0 ? r.VendedorId : null;

        // Una persona tiene una sola finca.
        var finca = p.Finca;
        if (finca == null) p.Fincas.Add(new Finca { Numero = r.Finca?.Trim() ?? "" });
        else finca.Numero = r.Finca?.Trim() ?? "";

        if (r.TelefonoPrincipal != null || r.WhatsappPrincipal != null)
        {
            if (r.TelefonoPrincipal != null)
                FijarNumeroPrincipal(p, TipoNumero.Telefono, r.TelefonoPrincipal, pais(r.TelefonoPrincipalPaisId, nameof(r.TelefonoPrincipalPaisId), r.TelefonoPrincipal, nameof(r.TelefonoPrincipal)));
            if (r.WhatsappPrincipal != null)
                FijarNumeroPrincipal(p, TipoNumero.WhatsApp, r.WhatsappPrincipal, pais(r.WhatsappPrincipalPaisId, nameof(r.WhatsappPrincipalPaisId), r.WhatsappPrincipal, nameof(r.WhatsappPrincipal)));
        }
        if (correo != null) FijarCorreoPrincipal(p, correo);

        if (p.Cliente != null)
        {
            if (r.EstadoClienteId is > 0)
            {
                if (!await _db.EstadosCliente.AnyAsync(e => e.Id == r.EstadoClienteId, ct))
                    throw new ValidacionException(nameof(r.EstadoClienteId), "El estado seleccionado ya no existe. Recargue la página y elija otro.");
                p.Cliente.EstadoClienteId = r.EstadoClienteId.Value;
            }
            if (r.OrigenClienteId is > 0)
            {
                if (!await _db.OrigenesCliente.AnyAsync(o => o.Id == r.OrigenClienteId, ct))
                    throw new ValidacionException(nameof(r.OrigenClienteId), "El origen seleccionado ya no existe. Recargue la página y elija otro.");
                p.Cliente.OrigenClienteId = r.OrigenClienteId.Value;
            }
        }
        await _db.SaveChangesAsync(ct);
    }

    public async Task<VentaDto> ActualizarVentaAsync(int id, ActualizarVentaRequest r, CancellationToken ct = default, Guid? version = null)
    {
        var p = await CargarAsync(id, ct);
        await _ventaValidator.ValidarAsync(r, ct);
        // Después del control de acceso y antes de tomar la versión.
        await ValidarCatalogoAsync(_db.ProcedenciasVenta, r.ProcedenciaVentaId, nameof(r.ProcedenciaVentaId), "La procedencia seleccionada ya no existe. Recargue la página y elija otra.", ct);
        await ValidarCatalogoAsync(_db.MetodosVenta, r.MetodoVentaId, nameof(r.MetodoVentaId), "El método seleccionado ya no existe. Recargue la página y elija otro.", ct);
        await PrepararVersionAsync(p, "venta", version);
        var v = p.Venta ?? throw new NoEncontradoException("Venta de la persona", id);
        v.ProcedenciaVentaId = r.ProcedenciaVentaId is > 0 ? r.ProcedenciaVentaId : null;
        v.MetodoVentaId = r.MetodoVentaId is > 0 ? r.MetodoVentaId : null;
        v.Monto = r.Monto;
        v.Notas = r.Notas ?? "";
        await _db.SaveChangesAsync(ct);
        return new VentaDto(v.Id, v.ProcedenciaVentaId, v.MetodoVentaId, v.Monto, v.Notas);
    }

    public async Task<PrimaDto> ActualizarPrimaAsync(int id, ActualizarPrimaRequest r, CancellationToken ct = default, Guid? version = null)
    {
        await _primaValidator.ValidarAsync(r, ct);
        var p = await CargarAsync(id, ct);
        await PrepararVersionAsync(p, "prima", version);
        var pr = p.Prima ?? throw new NoEncontradoException("Prima de la persona", id);
        pr.Monto = r.Monto;
        pr.MontoCancelado = r.MontoCancelado;
        pr.FechaEstimadaPago = r.FechaEstimadaPago;
        pr.FechaPago = r.FechaPago;
        pr.Recalcular();
        await _db.SaveChangesAsync(ct);
        return new PrimaDto(pr.Id, pr.Monto, pr.MontoCancelado, pr.SaldoPendiente, pr.FechaEstimadaPago, pr.FechaPago, pr.EstadoPrimaId);
    }

    /// <summary>Reemplaza la lista de números. Queda a lo sumo un principal por tipo.</summary>
    public async Task<IReadOnlyList<NumeroDto>> ReemplazarNumerosAsync(int id, IReadOnlyList<NumeroItem> items, CancellationToken ct = default, Guid? version = null)
    {
        var p = await CargarAsync(id, ct);
        ValidarIdsPropios(items.Select(i => i.Id), p.Numeros.Select(x => x.Id), "numeros");
        for (var indice = 0; indice < items.Count; indice++)
        {
            TipoDesde(items[indice].Tipo, $"numeros[{indice}].tipo");
            ValidarIdCatalogo(items[indice].PaisId, $"numeros[{indice}].paisId");
        }
        ValidarCantidad(items.Count(i => TipoDesde(i.Tipo) == TipoNumero.Telefono), Persona.MaxTelefonos, "numeros", "teléfonos");
        ValidarCantidad(items.Count(i => TipoDesde(i.Tipo) == TipoNumero.WhatsApp), Persona.MaxWhatsApp, "numeros", "números de WhatsApp");
        if (!items.Any(i => Validacion.SoloDigitos(i.Numero).Length > 0))
            throw new ValidacionException("numeros", CrearProspectoValidator.MensajeContacto);
        for (var indice = 0; indice < items.Count; indice++)
            if (Validacion.SoloDigitos(items[indice].Numero).Length == 0)
                throw new ValidacionException($"numeros[{indice}].numero", "Complete el número o quite la fila vacía.");
        var pais = await PaisesAsync(ct);
        for (var indice = 0; indice < items.Count; indice++)
            pais(items[indice].PaisId, $"numeros[{indice}].paisId", items[indice].Numero, $"numeros[{indice}].numero");
        await PrepararVersionAsync(p, "datos", version);
        var conservar = items.Where(i => i.Id > 0).Select(i => i.Id).ToHashSet();
        _db.Numeros.RemoveRange(p.Numeros.Where(n => !conservar.Contains(n.Id)));
        var resultado = new List<Numero>();
        foreach (var i in items)
        {
            var n = i.Id > 0 ? p.Numeros.FirstOrDefault(x => x.Id == i.Id) : null;
            if (n == null) { n = new Numero { PersonaId = p.Id }; _db.Numeros.Add(n); }
            n.PaisId = pais(i.PaisId, nameof(i.PaisId), i.Numero, nameof(i.Numero));
            n.Valor = Validacion.SoloDigitos(i.Numero);
            n.Tipo = TipoDesde(i.Tipo);
            n.Principal = i.Principal;
            resultado.Add(n);
        }
        foreach (var grupo in resultado.GroupBy(n => n.Tipo))
            NormalizarPrincipal(grupo.ToList(), n => n.Principal, (n, v) => n.Principal = v);
        await _db.SaveChangesAsync(ct);
        return resultado.Select(NumeroDe).ToList();
    }

    public async Task<IReadOnlyList<CorreoDto>> ReemplazarCorreosAsync(int id, IReadOnlyList<CorreoItem> items, CancellationToken ct = default, Guid? version = null)
    {
        for (var indice = 0; indice < items.Count; indice++)
            Validacion.Correo(items[indice].Correo, $"correos[{indice}].correo", Persona.MaxCorreo);
        var p = await CargarAsync(id, ct);
        ValidarIdsPropios(items.Select(i => i.Id), p.Correos.Select(x => x.Id), "correos");
        ValidarCantidad(items.Count, Persona.MaxCorreos, "correos", "correos");
        await PrepararVersionAsync(p, "datos", version);
        var conservar = items.Where(i => i.Id > 0).Select(i => i.Id).ToHashSet();
        _db.Correos.RemoveRange(p.Correos.Where(c => !conservar.Contains(c.Id)));
        var resultado = new List<CorreoElectronico>();
        foreach (var i in items)
        {
            var c = i.Id > 0 ? p.Correos.FirstOrDefault(x => x.Id == i.Id) : null;
            if (c == null) { c = new CorreoElectronico { PersonaId = p.Id }; _db.Correos.Add(c); }
            c.Correo = i.Correo?.Trim() ?? "";
            c.Principal = i.Principal;
            resultado.Add(c);
        }
        NormalizarPrincipal(resultado, c => c.Principal, (c, v) => c.Principal = v);
        await _db.SaveChangesAsync(ct);
        return resultado.Select(c => new CorreoDto(c.Id, c.Correo, c.Principal)).ToList();
    }

    public async Task<IReadOnlyList<FamiliarDto>> ReemplazarFamiliaresAsync(int id, IReadOnlyList<FamiliarItem> items, CancellationToken ct = default, Guid? version = null)
    {
        // La validación conserva la ruta de cada fila de la solicitud.
        for (var indice = 0; indice < items.Count; indice++)
            await _familiarValidator.ValidarAsync(items[indice], ct, $"familiares[{indice}]");
        await ValidarParentescosAsync(items, ct);
        var p = await CargarAsync(id, ct);
        ValidarIdsPropios(items.Select(i => i.Id), p.Familiares.Select(x => x.Id), "familiares");
        ValidarCantidad(items.Count, Persona.MaxFamiliares, "familiares", "familiares");
        var pais = await PaisesAsync(ct);
        ValidarTelefonosFamiliares(items, pais);
        await PrepararVersionAsync(p, "familiares", version);
        var conservar = items.Where(i => i.Id > 0).Select(i => i.Id).ToHashSet();
        _db.Familiares.RemoveRange(p.Familiares.Where(f => !conservar.Contains(f.Id)));
        var resultado = new List<PersonaFamiliar>();
        foreach (var i in items)
        {
            var f = i.Id > 0 ? p.Familiares.FirstOrDefault(x => x.Id == i.Id) : null;
            if (f == null) { f = new PersonaFamiliar { PersonaId = p.Id }; _db.Familiares.Add(f); }
            CopiarFamiliar(i, f, pais);
            resultado.Add(f);
        }
        await _db.SaveChangesAsync(ct);
        return resultado.Select(FamiliarDe).ToList();
    }

    public async Task<PersonaDetalleDto> ConvertirEnClienteAsync(int id, ConvertirClienteRequest r, CancellationToken ct = default, Guid? version = null)
    {
        await _convertirValidator.ValidarAsync(r, ct);
        var p = await CargarAsync(id, ct);
        // Después del control de acceso y antes de tomar la versión. El validador ya exige el origen;
        // aquí se comprueba que siga en el catálogo.
        await ValidarCatalogoAsync(_db.OrigenesCliente, r.OrigenClienteId, nameof(r.OrigenClienteId), "El origen seleccionado ya no existe. Recargue la página y elija otro.", ct);
        await ValidarCatalogoAsync(_db.EstadosCliente, r.EstadoClienteId, nameof(r.EstadoClienteId), "El estado seleccionado ya no existe. Recargue la página y elija otro.", ct);
        await PrepararVersionAsync(p, "datos", version);
        if (p.EsCliente) throw new ValidacionException("id", "La persona ya es cliente oficial.");
        // Sin estado elegido, el cliente nace al día.
        var estado = r.EstadoClienteId is > 0 ? r.EstadoClienteId.Value : EstadoCliente.AlDia;

        p.Cliente = new Cliente { OrigenClienteId = r.OrigenClienteId!.Value, EstadoClienteId = estado };
        if (r.Expediente != null) p.Expediente = r.Expediente.Trim();
        await _db.SaveChangesAsync(ct);
        return await ObtenerAsync(id, ct);
    }

    /// <summary>Borra la persona con su venta, prima, contactos, finca, familiares, comentarios y cliente.</summary>
    public async Task EliminarAsync(int id, CancellationToken ct = default)
    {
        if (!_usuario.EsAdministrador) throw new ProhibidoException();
        var p = await CargarAsync(id, ct);
        var cliente = p.Cliente;
        _db.Personas.Remove(p);
        if (cliente != null) _db.Clientes.Remove(cliente);
        await _db.SaveChangesAsync(ct);
    }

    public async Task<ComentarioDto> ComentarAsync(int personaId, ComentarioRequest r, CancellationToken ct = default)
    {
        await _comentarioValidator.ValidarAsync(r, ct);
        await CargarAsync(personaId, ct);
        var usuarioId = _usuario.Id ?? throw new ProhibidoException();
        var c = new Comentario { PersonaId = personaId, UsuarioId = usuarioId, Texto = r.Texto.Trim(), Fecha = _fecha.AhoraUtc };
        _db.Comentarios.Add(c);
        await _db.SaveChangesAsync(ct);
        var u = await _identity.ObtenerAsync(usuarioId);
        return new ComentarioDto(c.Id, usuarioId, u?.Nombre ?? "", u?.Rol, c.Texto, c.Fecha, true);
    }

    /// <summary>Cada usuario solo puede eliminar sus propios comentarios (también el administrador).</summary>
    public async Task EliminarComentarioAsync(int comentarioId, CancellationToken ct = default)
    {
        var c = await _db.Comentarios.FindAsync(new object[] { comentarioId }, ct) ?? throw new NoEncontradoException("Comentario", comentarioId);
        if (c.UsuarioId != _usuario.Id) throw new ProhibidoException("Solo puede eliminar sus propios comentarios.");
        await CargarAsync(c.PersonaId, ct);
        _db.Comentarios.Remove(c);
        await _db.SaveChangesAsync(ct);
    }

    private void FijarNumeroPrincipal(Persona p, TipoNumero tipo, string valor, int paisId)
    {
        var actual = p.NumeroPrincipal(tipo);
        var limpio = Validacion.SoloDigitos(valor);
        if (actual != null && limpio.Length == 0) { _db.Numeros.Remove(actual); return; }
        if (actual != null) { actual.PaisId = paisId; actual.Valor = limpio; actual.Principal = true; return; }
        if (limpio.Length > 0) p.Numeros.Add(new Numero { PaisId = paisId, Valor = limpio, Tipo = tipo, Principal = true });
    }

    private void FijarCorreoPrincipal(Persona p, string valor)
    {
        var actual = p.CorreoPrincipal;
        var limpio = valor.Trim();
        if (actual != null && limpio.Length == 0) { _db.Correos.Remove(actual); return; }
        if (actual != null) { actual.Correo = limpio; actual.Principal = true; return; }
        if (limpio.Length > 0) p.Correos.Add(new CorreoElectronico { Correo = limpio, Principal = true });
    }

    private static void NormalizarPrincipal<T>(List<T> items, Func<T, bool> es, Action<T, bool> fijar)
    {
        if (items.Count == 0) return;
        var primero = items.FirstOrDefault(es) ?? items[0];
        foreach (var i in items) fijar(i, ReferenceEquals(i, primero));
    }

    /// <summary>Fila sin ningún dato (se ignora al guardar).</summary>
    public static bool FamiliarVacio(FamiliarItem f) =>
        Validacion.Vacio(f.NombreCompleto) && f.ParentescoId is not > 0 && Validacion.Vacio(f.Telefono)
        && Validacion.Vacio(f.CorreoElectronico) && Validacion.Vacio(f.Whatsapp);

    private static PersonaFamiliar NuevoFamiliar(FamiliarItem i, Func<int?, string, string?, string, int> pais)
    {
        var f = new PersonaFamiliar();
        CopiarFamiliar(i, f, pais);
        return f;
    }

    /// <summary>El país solo se guarda si hay número.</summary>
    private static void CopiarFamiliar(FamiliarItem i, PersonaFamiliar f, Func<int?, string, string?, string, int> pais)
    {
        f.NombreCompleto = i.NombreCompleto?.Trim() ?? "";
        f.ParentescoId = i.ParentescoId is > 0 ? i.ParentescoId : null;
        f.Telefono = Validacion.SoloDigitos(i.Telefono);
        f.TelefonoPaisId = f.Telefono.Length > 0 ? pais(i.TelefonoPaisId, nameof(i.TelefonoPaisId), i.Telefono, nameof(i.Telefono)) : null;
        f.CorreoElectronico = i.CorreoElectronico?.Trim() ?? "";
        f.Whatsapp = Validacion.SoloDigitos(i.Whatsapp);
        f.WhatsappPaisId = f.Whatsapp.Length > 0 ? pais(i.WhatsappPaisId, nameof(i.WhatsappPaisId), i.Whatsapp, nameof(i.Whatsapp)) : null;
    }
}





