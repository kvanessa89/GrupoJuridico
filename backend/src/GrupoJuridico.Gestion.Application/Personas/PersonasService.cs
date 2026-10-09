using GrupoJuridico.Gestion.Application.Common;
using GrupoJuridico.Gestion.Application.Common.Exceptions;
using GrupoJuridico.Gestion.Application.Common.Interfaces;
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

    private readonly CrearProspectoValidator _crearValidator = new();
    private readonly ActualizarVentaValidator _ventaValidator = new();
    private readonly ActualizarPrimaValidator _primaValidator = new();
    private readonly ConvertirClienteValidator _convertirValidator = new();
    private readonly ComentarioValidator _comentarioValidator = new();

    public PersonasService(IApplicationDbContext db, IUsuarioActual usuario, IIdentityService identity, IFechaActual fecha)
    {
        _db = db;
        _usuario = usuario;
        _identity = identity;
        _fecha = fecha;
    }

    public static string TipoTexto(TipoNumero t) => t == TipoNumero.WhatsApp ? "WhatsApp" : "Teléfono";
    public static TipoNumero TipoDesde(string? t) =>
        string.Equals(t, "WhatsApp", StringComparison.OrdinalIgnoreCase) ? TipoNumero.WhatsApp : TipoNumero.Telefono;

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
            .AsSplitQuery()
            .FirstOrDefaultAsync(p => p.Id == id, ct)
        ?? throw new NoEncontradoException("Persona", id);

        VerificarAcceso(persona);
        return persona;
    }

    private void VerificarAcceso(Persona persona)
    {
        if (_usuario.Rol == Roles.AsistenteVentas &&
            persona.Prima?.EstadoPrimaId == EstadoPrima.Pagada)
            throw new ProhibidoException("No tenés permiso para acceder a esta persona.");
    }

    public async Task<PersonaDetalleDto> ObtenerAsync(int id, CancellationToken ct = default)
    {
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
            p.Numeros.OrderBy(n => n.Id).Select(n => new NumeroDto(n.Id, n.Valor, TipoTexto(n.Tipo), n.Principal)).ToList(),
            p.Correos.OrderBy(c => c.Id).Select(c => new CorreoDto(c.Id, c.Correo, c.Principal)).ToList(),
            p.Familiares.OrderBy(f => f.Id).Select(f => new FamiliarDto(f.Id, f.NombreCompleto, f.Parentesco, f.Telefono, f.CorreoElectronico, f.Whatsapp)).ToList(),
            p.Comentarios.OrderByDescending(c => c.Fecha).ThenByDescending(c => c.Id).Select(c =>
            {
                autores.TryGetValue(c.UsuarioId, out var u);
                return new ComentarioDto(c.Id, c.UsuarioId, u?.Nombre ?? "Usuario eliminado", u?.Rol, c.Texto,
                    DateTime.SpecifyKind(c.Fecha, DateTimeKind.Utc), c.UsuarioId == _usuario.Id);
            }).ToList());
    }

    public async Task<PersonaDetalleDto> CrearProspectoAsync(CrearProspectoRequest r, CancellationToken ct = default)
    {
        await _crearValidator.ValidarAsync(r, ct);
        var persona = new Persona
        {
            Nombres = r.Nombres!.Trim(),
            Apellidos = r.Apellidos!.Trim(),
            Cedula = r.Cedula!.Trim(),
            Expediente = r.Expediente?.Trim() ?? "",
            FechaIngreso = r.FechaIngreso!.Value,
            VendedorId = r.VendedorId,
            FechaCreacion = _fecha.AhoraUtc
        };
        persona.Fincas.Add(new Finca { Numero = r.Finca!.Trim() });
        if (!Validacion.Vacio(r.TelefonoPrincipal))
            persona.Numeros.Add(new Numero { Valor = r.TelefonoPrincipal!.Trim(), Tipo = TipoNumero.Telefono, Principal = true });
        if (!Validacion.Vacio(r.WhatsappPrincipal))
            persona.Numeros.Add(new Numero { Valor = r.WhatsappPrincipal!.Trim(), Tipo = TipoNumero.WhatsApp, Principal = true });
        if (!Validacion.Vacio(r.CorreoPrincipal))
            persona.Correos.Add(new CorreoElectronico { Correo = r.CorreoPrincipal!.Trim(), Principal = true });
        foreach (var f in (r.Familiares ?? Array.Empty<FamiliarItem>()).Where(f => !FamiliarVacio(f)))
            persona.Familiares.Add(NuevoFamiliar(f));

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

    public async Task ActualizarDatosAsync(int id, ActualizarDatosRequest r, CancellationToken ct = default)
    {
        var p = await CargarAsync(id, ct);
        p.Nombres = r.Nombres?.Trim() ?? "";
        p.Apellidos = r.Apellidos?.Trim() ?? "";
        p.Cedula = r.Cedula?.Trim() ?? "";
        p.Expediente = r.Expediente?.Trim() ?? "";
        p.FechaIngreso = r.FechaIngreso;
        p.VendedorId = r.VendedorId is > 0 ? r.VendedorId : null;

        // Una persona tiene una sola finca.
        var finca = p.Finca;
        if (finca == null) p.Fincas.Add(new Finca { Numero = r.Finca?.Trim() ?? "" });
        else finca.Numero = r.Finca?.Trim() ?? "";

        if (r.TelefonoPrincipal != null) FijarNumeroPrincipal(p, TipoNumero.Telefono, r.TelefonoPrincipal);
        if (r.WhatsappPrincipal != null) FijarNumeroPrincipal(p, TipoNumero.WhatsApp, r.WhatsappPrincipal);
        if (r.CorreoPrincipal != null) FijarCorreoPrincipal(p, r.CorreoPrincipal);

        if (p.Cliente != null)
        {
            if (r.EstadoClienteId is > 0)
            {
                if (!await _db.EstadosCliente.AnyAsync(e => e.Id == r.EstadoClienteId, ct))
                    throw new ValidacionException(nameof(r.EstadoClienteId), "Estado del cliente no válido.");
                p.Cliente.EstadoClienteId = r.EstadoClienteId.Value;
            }
            if (r.OrigenClienteId is > 0)
            {
                if (!await _db.OrigenesCliente.AnyAsync(o => o.Id == r.OrigenClienteId, ct))
                    throw new ValidacionException(nameof(r.OrigenClienteId), "Origen del cliente no válido.");
                p.Cliente.OrigenClienteId = r.OrigenClienteId.Value;
            }
        }
        await _db.SaveChangesAsync(ct);
    }

    public async Task<VentaDto> ActualizarVentaAsync(int id, ActualizarVentaRequest r, CancellationToken ct = default)
    {
        await _ventaValidator.ValidarAsync(r, ct);
        var p = await CargarAsync(id, ct);
        var v = p.Venta ?? throw new NoEncontradoException("Venta de la persona", id);
        v.ProcedenciaVentaId = r.ProcedenciaVentaId is > 0 ? r.ProcedenciaVentaId : null;
        v.MetodoVentaId = r.MetodoVentaId is > 0 ? r.MetodoVentaId : null;
        v.Monto = r.Monto;
        v.Notas = r.Notas ?? "";
        await _db.SaveChangesAsync(ct);
        return new VentaDto(v.Id, v.ProcedenciaVentaId, v.MetodoVentaId, v.Monto, v.Notas);
    }

    public async Task<PrimaDto> ActualizarPrimaAsync(int id, ActualizarPrimaRequest r, CancellationToken ct = default)
    {
        await _primaValidator.ValidarAsync(r, ct);
        var p = await CargarAsync(id, ct);
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
    public async Task<IReadOnlyList<NumeroDto>> ReemplazarNumerosAsync(int id, IReadOnlyList<NumeroItem> items, CancellationToken ct = default)
    {
        var p = await CargarAsync(id, ct);
        var conservar = items.Where(i => i.Id > 0).Select(i => i.Id).ToHashSet();
        _db.Numeros.RemoveRange(p.Numeros.Where(n => !conservar.Contains(n.Id)));
        var resultado = new List<Numero>();
        foreach (var i in items)
        {
            var n = i.Id > 0 ? p.Numeros.FirstOrDefault(x => x.Id == i.Id) : null;
            if (n == null) { n = new Numero { PersonaId = p.Id }; _db.Numeros.Add(n); }
            n.Valor = i.Numero?.Trim() ?? "";
            n.Tipo = TipoDesde(i.Tipo);
            n.Principal = i.Principal;
            resultado.Add(n);
        }
        foreach (var grupo in resultado.GroupBy(n => n.Tipo))
            NormalizarPrincipal(grupo.ToList(), n => n.Principal, (n, v) => n.Principal = v);
        await _db.SaveChangesAsync(ct);
        return resultado.Select(n => new NumeroDto(n.Id, n.Valor, TipoTexto(n.Tipo), n.Principal)).ToList();
    }

    public async Task<IReadOnlyList<CorreoDto>> ReemplazarCorreosAsync(int id, IReadOnlyList<CorreoItem> items, CancellationToken ct = default)
    {
        var p = await CargarAsync(id, ct);
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

    public async Task<IReadOnlyList<FamiliarDto>> ReemplazarFamiliaresAsync(int id, IReadOnlyList<FamiliarItem> items, CancellationToken ct = default)
    {
        var p = await CargarAsync(id, ct);
        var conservar = items.Where(i => i.Id > 0).Select(i => i.Id).ToHashSet();
        _db.Familiares.RemoveRange(p.Familiares.Where(f => !conservar.Contains(f.Id)));
        var resultado = new List<PersonaFamiliar>();
        foreach (var i in items)
        {
            var f = i.Id > 0 ? p.Familiares.FirstOrDefault(x => x.Id == i.Id) : null;
            if (f == null) { f = new PersonaFamiliar { PersonaId = p.Id }; _db.Familiares.Add(f); }
            CopiarFamiliar(i, f);
            resultado.Add(f);
        }
        await _db.SaveChangesAsync(ct);
        return resultado.Select(f => new FamiliarDto(f.Id, f.NombreCompleto, f.Parentesco, f.Telefono, f.CorreoElectronico, f.Whatsapp)).ToList();
    }

    public async Task<PersonaDetalleDto> ConvertirEnClienteAsync(int id, ConvertirClienteRequest r, CancellationToken ct = default)
    {
        await _convertirValidator.ValidarAsync(r, ct);
        var p = await CargarAsync(id, ct);
        if (p.EsCliente) throw new ValidacionException("id", "La persona ya es cliente oficial.");
        if (!await _db.OrigenesCliente.AnyAsync(o => o.Id == r.OrigenClienteId, ct))
            throw new ValidacionException(nameof(r.OrigenClienteId), "El origen del cliente es requerido.");
        var estado = r.EstadoClienteId is > 0 && await _db.EstadosCliente.AnyAsync(e => e.Id == r.EstadoClienteId, ct)
            ? r.EstadoClienteId.Value
            : EstadoCliente.AlDia;

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
        if (c.UsuarioId != _usuario.Id) throw new ProhibidoException("Solo podés eliminar tus propios comentarios.");
        await CargarAsync(c.PersonaId, ct);
        _db.Comentarios.Remove(c);
        await _db.SaveChangesAsync(ct);
    }

    private void FijarNumeroPrincipal(Persona p, TipoNumero tipo, string valor)
    {
        var actual = p.NumeroPrincipal(tipo);
        var limpio = valor.Trim();
        if (actual != null && limpio.Length == 0) { _db.Numeros.Remove(actual); return; }
        if (actual != null) { actual.Valor = limpio; actual.Principal = true; return; }
        if (limpio.Length > 0) p.Numeros.Add(new Numero { Valor = limpio, Tipo = tipo, Principal = true });
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

    private static bool FamiliarVacio(FamiliarItem f) =>
        Validacion.Vacio(f.NombreCompleto) && Validacion.Vacio(f.Parentesco) && Validacion.Vacio(f.Telefono) && Validacion.Vacio(f.CorreoElectronico);

    private static PersonaFamiliar NuevoFamiliar(FamiliarItem i)
    {
        var f = new PersonaFamiliar();
        CopiarFamiliar(i, f);
        return f;
    }

    private static void CopiarFamiliar(FamiliarItem i, PersonaFamiliar f)
    {
        f.NombreCompleto = i.NombreCompleto?.Trim() ?? "";
        f.Parentesco = i.Parentesco?.Trim() ?? "";
        f.Telefono = i.Telefono?.Trim() ?? "";
        f.CorreoElectronico = i.CorreoElectronico?.Trim() ?? "";
        f.Whatsapp = i.Whatsapp?.Trim() ?? "";
    }
}
