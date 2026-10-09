using System.Globalization;
using FluentValidation;
using GrupoJuridico.Gestion.Application.Common;
using GrupoJuridico.Gestion.Application.Common.Exceptions;
using GrupoJuridico.Gestion.Application.Common.Interfaces;
using GrupoJuridico.Gestion.Application.Usuarios;
using GrupoJuridico.Gestion.Domain.Common;
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
    IReadOnlyList<RolDto> Roles,
    decimal InteresMora);

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
    public static readonly string[] Tipos = { "vendedores", "procedencias", "metodos", "estados-prima", "origenes", "estados-cliente" };

    private readonly IApplicationDbContext _db;
    private readonly IIdentityService _identity;
    private readonly GuardarCatalogoItemValidator _validator = new();

    public CatalogosService(IApplicationDbContext db, IIdentityService identity)
    {
        _db = db;
        _identity = identity;
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
            _identity.Roles(),
            interes != null && decimal.TryParse(interes.Valor, NumberStyles.Number, CultureInfo.InvariantCulture, out var pct) ? pct : 2.5m);
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
            _ => throw new NoEncontradoException("Catálogo", tipo)
        };
        item.Nombre = request.Nombre.Trim();
        // Solo vendedores y orígenes muestran el código al usuario; en los demás es interno.
        if ((tipo == "vendedores" || tipo == "origenes") && request.Codigo != null) item.Codigo = request.Codigo.Trim();
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
                // Las personas vendidas por este vendedor quedan sin vendedor.
                await _db.Personas.Where(p => p.VendedorId == id).ExecuteUpdateAsync(s => s.SetProperty(p => p.VendedorId, (int?)null), ct);
                _db.Vendedores.Remove(v);
                break;
            }
            case "procedencias":
            {
                var x = await _db.ProcedenciasVenta.FindAsync(new object[] { id }, ct) ?? throw new NoEncontradoException("Procedencia", id);
                await _db.Ventas.Where(v => v.ProcedenciaVentaId == id).ExecuteUpdateAsync(s => s.SetProperty(v => v.ProcedenciaVentaId, (int?)null), ct);
                _db.ProcedenciasVenta.Remove(x);
                break;
            }
            case "metodos":
            {
                var x = await _db.MetodosVenta.FindAsync(new object[] { id }, ct) ?? throw new NoEncontradoException("Método", id);
                await _db.Ventas.Where(v => v.MetodoVentaId == id).ExecuteUpdateAsync(s => s.SetProperty(v => v.MetodoVentaId, (int?)null), ct);
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
                var enUso = await _db.Clientes.CountAsync(c => c.EstadoClienteId == id, ct);
                if (enUso > 0) throw new ValidacionException("id", $"No se puede eliminar: {enUso} cliente(s) tienen este estado.");
                _db.EstadosCliente.Remove(x);
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
        return request.Porcentaje;
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
