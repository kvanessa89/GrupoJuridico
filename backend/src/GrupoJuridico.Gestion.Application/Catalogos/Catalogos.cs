using System.Globalization;
using FluentValidation;
using GrupoJuridico.Gestion.Application.Cobros;
using GrupoJuridico.Gestion.Application.Common;
using GrupoJuridico.Gestion.Application.Common.Exceptions;
using GrupoJuridico.Gestion.Application.Common.Interfaces;
using GrupoJuridico.Gestion.Application.Usuarios;
using GrupoJuridico.Gestion.Domain.Common;
using GrupoJuridico.Gestion.Domain.Constants;
using GrupoJuridico.Gestion.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace GrupoJuridico.Gestion.Application.Catalogos;

public record CatalogoItemDto(int Id, string Codigo, string Nombre, string? Apellidos = null);

public record CatalogosDto(
    IReadOnlyList<CatalogoItemDto> Vendedores,
    IReadOnlyList<CatalogoItemDto> Procedencias,
    IReadOnlyList<CatalogoItemDto> Metodos,
    IReadOnlyList<CatalogoItemDto> EstadosPrima,
    IReadOnlyList<CatalogoItemDto> Origenes,
    IReadOnlyList<CatalogoItemDto> EstadosCliente,
    IReadOnlyList<CatalogoItemDto> Paises,
    IReadOnlyList<CatalogoItemDto> Parentescos,
    IReadOnlyList<RolDto> Roles,
    decimal InteresMora,
    IReadOnlyList<CatalogoItemDto> EstadosCuotaPago,
    IReadOnlyList<CatalogoItemDto> EstadosCuotaVencimiento,
    IReadOnlyList<CatalogoItemDto> EstadosPromesa,
    IReadOnlyList<CatalogoItemDto> TiposDocumento);

public record GuardarCatalogoItemRequest(string Nombre, string? Codigo, string? Apellidos);

public record InteresMoraRequest(decimal Porcentaje);

public class GuardarCatalogoItemValidator : AbstractValidator<GuardarCatalogoItemRequest>
{
    public GuardarCatalogoItemValidator()
    {
        RuleFor(x => x.Nombre).NotEmpty().WithMessage("El nombre es requerido.").MaximumLength(150);
        RuleFor(x => x.Codigo).MaximumLength(20);
        RuleFor(x => x.Apellidos).MaximumLength(150);
    }
}

/// <summary>Listas de referencia administrables desde Configuración.</summary>
public class CatalogosService
{
    /// <summary>Tipos de catálogo expuestos por la API (segmento de la ruta).</summary>
    public static readonly string[] Tipos = { "vendedores", "procedencias", "metodos", "estados-prima", "origenes", "estados-cliente", "paises", "parentescos", "tipos-documento" };

    private readonly IApplicationDbContext _db;
    private readonly IIdentityService _identity;
    private readonly GuardarCatalogoItemValidator _validator = new();

    private readonly ActualizadorCobros _cobros;

    public CatalogosService(IApplicationDbContext db, IIdentityService identity, ActualizadorCobros cobros)
    {
        _db = db;
        _identity = identity;
        _cobros = cobros;
    }

    public async Task<CatalogosDto> ObtenerAsync(CancellationToken ct = default)
    {
        static CatalogoItemDto Map(Catalogo c) => new(c.Id, c.Codigo, c.Nombre);
        var interes = await _db.Configuracion.AsNoTracking().FirstOrDefaultAsync(c => c.Clave == ConfiguracionSistema.InteresMora, ct);
        return new CatalogosDto(
            (await _db.Vendedores.AsNoTracking().OrderBy(x => x.Id).ToListAsync(ct))
                .OrderBy(v => int.TryParse(v.Codigo, out var n) ? n : int.MaxValue).ThenBy(v => v.Id)
                .Select(v => new CatalogoItemDto(v.Id, v.Codigo, v.Nombre, v.Apellidos)).ToList(),
            (await _db.ProcedenciasVenta.AsNoTracking().OrderBy(x => x.Id).ToListAsync(ct)).Select(Map).ToList(),
            (await _db.MetodosVenta.AsNoTracking().OrderBy(x => x.Id).ToListAsync(ct)).Select(Map).ToList(),
            (await _db.EstadosPrima.AsNoTracking().OrderBy(x => x.Id).ToListAsync(ct)).Select(Map).ToList(),
            (await _db.OrigenesCliente.AsNoTracking().OrderBy(x => x.Id).ToListAsync(ct)).Select(Map).ToList(),
            (await _db.EstadosCliente.AsNoTracking().OrderBy(x => x.Id).ToListAsync(ct)).Select(Map).ToList(),
            (await _db.Paises.AsNoTracking().OrderBy(x => x.Id).ToListAsync(ct)).Select(Map).ToList(),
            (await _db.Parentescos.AsNoTracking().OrderBy(x => x.Id).ToListAsync(ct)).Select(Map).ToList(),
            _identity.Roles(),
            interes != null && decimal.TryParse(interes.Valor, NumberStyles.Number, CultureInfo.InvariantCulture, out var pct) ? pct : 2.5m,
            // Estados de Cobros: solo lectura, se calculan de los pagos y fechas.
            (await _db.EstadosCuotaPago.AsNoTracking().OrderBy(x => x.Id).ToListAsync(ct)).Select(Map).ToList(),
            (await _db.EstadosCuotaVencimiento.AsNoTracking().OrderBy(x => x.Id).ToListAsync(ct)).Select(Map).ToList(),
            (await _db.EstadosPromesa.AsNoTracking().OrderBy(x => x.Id).ToListAsync(ct)).Select(Map).ToList(),
            (await _db.TiposDocumento.AsNoTracking().OrderBy(x => x.Id).ToListAsync(ct)).Select(Map).ToList());
    }

    public async Task<CatalogoItemDto> CrearAsync(string tipo, GuardarCatalogoItemRequest request, CancellationToken ct = default)
    {
        await _validator.ValidarAsync(request, ct);
        var nombre = request.Nombre.Trim();
        switch (tipo)
        {
            case "vendedores":
            {
                var codigos = await _db.Vendedores.Select(v => v.Codigo).ToListAsync(ct);
                var siguiente = Math.Max(29, codigos.Select(c => int.TryParse(c, out var n) ? n : 0).DefaultIfEmpty(0).Max()) + 1;
                var v = new Vendedor
                {
                    Codigo = Validacion.Vacio(request.Codigo) ? siguiente.ToString(CultureInfo.InvariantCulture) : request.Codigo!.Trim(),
                    Nombre = nombre,
                    Apellidos = request.Apellidos?.Trim() ?? string.Empty
                };
                _db.Vendedores.Add(v);
                await _db.SaveChangesAsync(ct);
                return new CatalogoItemDto(v.Id, v.Codigo, v.Nombre, v.Apellidos);
            }
            case "origenes":
            {
                var codigos = await _db.OrigenesCliente.Select(o => o.Codigo).ToListAsync(ct);
                var siguiente = codigos.Select(c => int.TryParse(new string(c.Where(char.IsDigit).ToArray()), out var n) ? n : 0).DefaultIfEmpty(0).Max() + 1;
                var codigo = Validacion.Vacio(request.Codigo) ? "P" + siguiente : request.Codigo!.Trim();
                return await AgregarAsync(_db.OrigenesCliente, new OrigenCliente { Codigo = codigo, Nombre = nombre }, ct);
            }
            case "procedencias":
                return await AgregarAsync(_db.ProcedenciasVenta, new ProcedenciaVenta { Nombre = nombre }, ct, "PV-");
            case "metodos":
                return await AgregarAsync(_db.MetodosVenta, new MetodoVenta { Nombre = nombre }, ct, "MV-");
            case "estados-cliente":
                return await AgregarAsync(_db.EstadosCliente, new EstadoCliente { Nombre = nombre }, ct, "EC-");
            case "parentescos":
                return await AgregarAsync(_db.Parentescos, new Parentesco { Nombre = nombre }, ct, "PA-");
            case "paises":
                return await AgregarAsync(_db.Paises, new Pais { Codigo = Prefijo(request.Codigo), Nombre = nombre }, ct);
            case "tipos-documento":
            {
                // Un tipo nuevo empieza solo con el Administrador; los demás roles se habilitan en la matriz de permisos.
                var tipoDoc = new TipoDocumento { Nombre = nombre };
                tipoDoc.Permisos.Add(new PermisoDocumento { Rol = Roles.Administrador, PuedeVer = true, PuedeSubir = true });
                return await AgregarAsync(_db.TiposDocumento, tipoDoc, ct, "TD-");
            }
            case "estados-prima":
                throw new ValidacionException("tipo", "Los estados de prima son fijos: solo se puede cambiar su nombre.");
            default:
                throw new NoEncontradoException("Catálogo", tipo);
        }
    }

    public async Task<CatalogoItemDto> ActualizarAsync(string tipo, int id, GuardarCatalogoItemRequest request, CancellationToken ct = default)
    {
        await _validator.ValidarAsync(request, ct);
        Catalogo item = tipo switch
        {
            "vendedores" => await _db.Vendedores.FindAsync(new object[] { id }, ct) ?? throw new NoEncontradoException("Vendedor", id),
            "procedencias" => await _db.ProcedenciasVenta.FindAsync(new object[] { id }, ct) ?? throw new NoEncontradoException("Procedencia", id),
            "metodos" => await _db.MetodosVenta.FindAsync(new object[] { id }, ct) ?? throw new NoEncontradoException("Método", id),
            "estados-prima" => await _db.EstadosPrima.FindAsync(new object[] { id }, ct) ?? throw new NoEncontradoException("Estado de prima", id),
            "origenes" => await _db.OrigenesCliente.FindAsync(new object[] { id }, ct) ?? throw new NoEncontradoException("Origen", id),
            "estados-cliente" => await _db.EstadosCliente.FindAsync(new object[] { id }, ct) ?? throw new NoEncontradoException("Estado del cliente", id),
            "paises" => await _db.Paises.FindAsync(new object[] { id }, ct) ?? throw new NoEncontradoException("País", id),
            "parentescos" => await _db.Parentescos.FindAsync(new object[] { id }, ct) ?? throw new NoEncontradoException("Parentesco", id),
            "tipos-documento" => await _db.TiposDocumento.FindAsync(new object[] { id }, ct) ?? throw new NoEncontradoException("Tipo de documento", id),
            _ => throw new NoEncontradoException("Catálogo", tipo)
        };
        item.Nombre = request.Nombre.Trim();
        // Solo vendedores y orígenes muestran el código al usuario; en los demás es interno.
        if ((tipo == "vendedores" || tipo == "origenes") && request.Codigo != null) item.Codigo = request.Codigo.Trim();
        if (tipo == "paises" && request.Codigo != null) item.Codigo = Prefijo(request.Codigo);
        if (item is Vendedor v && request.Apellidos != null) v.Apellidos = request.Apellidos.Trim();
        await _db.SaveChangesAsync(ct);
        return new CatalogoItemDto(item.Id, item.Codigo, item.Nombre, (item as Vendedor)?.Apellidos);
    }

    public async Task EliminarAsync(string tipo, int id, CancellationToken ct = default)
    {
        switch (tipo)
        {
            case "vendedores":
            {
                var v = await _db.Vendedores.FindAsync(new object[] { id }, ct) ?? throw new NoEncontradoException("Vendedor", id);
                var enUso = await _db.Personas.CountAsync(p => p.VendedorId == id, ct);
                if (enUso > 0) throw new ValidacionException("id", $"No se puede eliminar: {enUso} persona(s) tienen este vendedor.");
                _db.Vendedores.Remove(v);
                break;
            }
            case "procedencias":
            {
                var x = await _db.ProcedenciasVenta.FindAsync(new object[] { id }, ct) ?? throw new NoEncontradoException("Procedencia", id);
                var enUso = await _db.Ventas.CountAsync(v => v.ProcedenciaVentaId == id, ct);
                if (enUso > 0) throw new ValidacionException("id", $"No se puede eliminar: {enUso} venta(s) tienen esta procedencia.");
                _db.ProcedenciasVenta.Remove(x);
                break;
            }
            case "metodos":
            {
                var x = await _db.MetodosVenta.FindAsync(new object[] { id }, ct) ?? throw new NoEncontradoException("Método", id);
                var enUso = await _db.Ventas.CountAsync(v => v.MetodoVentaId == id, ct);
                if (enUso > 0) throw new ValidacionException("id", $"No se puede eliminar: {enUso} venta(s) tienen este método.");
                _db.MetodosVenta.Remove(x);
                break;
            }
            case "origenes":
            {
                var x = await _db.OrigenesCliente.FindAsync(new object[] { id }, ct) ?? throw new NoEncontradoException("Origen", id);
                var enUso = await _db.Clientes.CountAsync(c => c.OrigenClienteId == id, ct);
                if (enUso > 0) throw new ValidacionException("id", $"No se puede eliminar: {enUso} cliente(s) usan este origen.");
                _db.OrigenesCliente.Remove(x);
                break;
            }
            case "estados-cliente":
            {
                var x = await _db.EstadosCliente.FindAsync(new object[] { id }, ct) ?? throw new NoEncontradoException("Estado del cliente", id);
                if (id <= EstadoCliente.SinExpediente)
                    throw new ValidacionException("id", "Este estado lo usa el sistema y no se puede eliminar.");
                var enUso = await _db.Clientes.CountAsync(c => c.EstadoClienteId == id || c.EstadoManualId == id, ct);
                if (enUso > 0) throw new ValidacionException("id", $"No se puede eliminar: {enUso} cliente(s) tienen este estado.");
                _db.EstadosCliente.Remove(x);
                break;
            }
            case "parentescos":
            {
                var x = await _db.Parentescos.FindAsync(new object[] { id }, ct) ?? throw new NoEncontradoException("Parentesco", id);
                var enUso = await _db.Familiares.CountAsync(f => f.ParentescoId == id, ct);
                if (enUso > 0) throw new ValidacionException("id", $"No se puede eliminar: {enUso} familiar(es) tienen este parentesco.");
                _db.Parentescos.Remove(x);
                break;
            }
            case "paises":
            {
                var x = await _db.Paises.FindAsync(new object[] { id }, ct) ?? throw new NoEncontradoException("País", id);
                var enUso = await _db.Numeros.CountAsync(n => n.PaisId == id, ct)
                    + await _db.Familiares.CountAsync(f => f.TelefonoPaisId == id, ct);
                if (enUso > 0) throw new ValidacionException("id", $"No se puede eliminar: {enUso} teléfono(s) usan este país.");
                _db.Paises.Remove(x);
                break;
            }
            case "tipos-documento":
            {
                var x = await _db.TiposDocumento.FindAsync(new object[] { id }, ct) ?? throw new NoEncontradoException("Tipo de documento", id);
                if (x.Codigo == TipoDocumento.CodigoComprobantesPago)
                    throw new ValidacionException("id", "Este tipo lo usan los comprobantes de pago de Cobros y no se puede eliminar; sí se puede renombrar.");
                var enUso = await _db.ArchivosPersona.CountAsync(a => a.TipoDocumentoId == id, ct);
                if (enUso > 0) throw new ValidacionException("id", $"No se puede eliminar: {enUso} archivo(s) tienen este tipo de documento.");
                _db.TiposDocumento.Remove(x);
                break;
            }
            case "estados-prima":
                throw new ValidacionException("tipo", "Los estados de prima son fijos y no se pueden eliminar.");
            default:
                throw new NoEncontradoException("Catálogo", tipo);
        }
        await _db.SaveChangesAsync(ct);
    }

    public async Task<decimal> GuardarInteresMoraAsync(InteresMoraRequest request, CancellationToken ct = default)
    {
        if (request.Porcentaje < 0 || request.Porcentaje > 100)
            throw new ValidacionException(nameof(request.Porcentaje), "El porcentaje debe estar entre 0 y 100.");
        var fila = await _db.Configuracion.FirstOrDefaultAsync(c => c.Clave == ConfiguracionSistema.InteresMora, ct);
        if (fila == null)
        {
            fila = new ConfiguracionSistema { Clave = ConfiguracionSistema.InteresMora };
            _db.Configuracion.Add(fila);
        }
        fila.Valor = request.Porcentaje.ToString(CultureInfo.InvariantCulture);
        await _db.SaveChangesAsync(ct);
        // El porcentaje cambia la mora de las cuotas vencidas (y con ella su estado): se recalculan todos los planes.
        await _cobros.ActualizarTodosAsync(ct);
        return request.Porcentaje;
    }

    /// <summary>Prefijo internacional: "+" y de 1 a 4 dígitos ("+506", "+1").</summary>
    private static string Prefijo(string? codigo)
    {
        var limpio = (codigo ?? "").Trim().Replace(" ", "");
        if (limpio.Length > 0 && limpio[0] != '+') limpio = "+" + limpio;
        if (!System.Text.RegularExpressions.Regex.IsMatch(limpio, @"^\+\d{1,4}$"))
            throw new ValidacionException("codigo", "El código del país debe ser + y de 1 a 4 dígitos, por ejemplo +506.");
        return limpio;
    }

    private async Task<CatalogoItemDto> AgregarAsync<T>(DbSet<T> set, T item, CancellationToken ct, string? prefijo = null) where T : Catalogo
    {
        set.Add(item);
        await _db.SaveChangesAsync(ct);
        if (prefijo != null && Validacion.Vacio(item.Codigo))
        {
            item.Codigo = prefijo + item.Id;
            await _db.SaveChangesAsync(ct);
        }
        return new CatalogoItemDto(item.Id, item.Codigo, item.Nombre);
    }
}
