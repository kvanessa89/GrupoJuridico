using GrupoJuridico.Gestion.Application.Common.Exceptions;
using GrupoJuridico.Gestion.Application.Common.Interfaces;
using GrupoJuridico.Gestion.Application.Common.Models;
using GrupoJuridico.Gestion.Domain.Constants;
using GrupoJuridico.Gestion.Domain.Entities;
using GrupoJuridico.Gestion.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace GrupoJuridico.Gestion.Application.Personas;

/// <summary>
/// Tabla de ventas y tabla de clientes. La cartera es de cientos a pocos miles de personas,
/// así que se carga filtrada desde la base y se ordena y pagina en memoria.
/// </summary>
public class ListasService
{
    private readonly IApplicationDbContext _db;
    private readonly IUsuarioActual _usuario;

    public ListasService(IApplicationDbContext db, IUsuarioActual usuario)
    {
        _db = db;
        _usuario = usuario;
    }

    private IQueryable<Persona> PersonasCompletas() => _db.Personas.AsNoTracking()
        .Include(p => p.Cliente).ThenInclude(c => c!.OrigenCliente)
        .Include(p => p.Cliente).ThenInclude(c => c!.EstadoCliente)
        .Include(p => p.Vendedor)
        .Include(p => p.Ventas).ThenInclude(v => v.Prima).ThenInclude(pr => pr!.EstadoPrima)
        .Include(p => p.Ventas).ThenInclude(v => v.ProcedenciaVenta)
        .Include(p => p.Ventas).ThenInclude(v => v.MetodoVenta)
        .Include(p => p.Numeros)
        .Include(p => p.Correos)
        .Include(p => p.Fincas)
        .AsSplitQuery();

    private static bool Coincide(Persona p, string? q, bool conExpediente)
    {
        if (string.IsNullOrWhiteSpace(q)) return true;
        var blob = string.Join(" ", new[]
        {
            p.NombreCompleto, p.Cedula, conExpediente ? p.Expediente : "",
            string.Join(" ", p.Numeros.Select(n => n.Valor)),
            p.CorreoPrincipal?.Correo ?? "",
            string.Join(" ", p.Fincas.Select(f => f.Numero))
        }).ToLowerInvariant();
        return blob.Contains(q.Trim().ToLowerInvariant());
    }

    public async Task<VentasListaDto> VentasAsync(ListaQuery f, CancellationToken ct = default)
    {
        var ocultaPagadas = Roles.OcultaPrimasPagadas(_usuario.Rol);
        var todas = await PersonasCompletas().Where(p => p.Ventas.Any(v => v.Prima != null)).ToListAsync(ct);

        var visibles = todas.Where(p => !(ocultaPagadas && p.Prima!.EstadoPrimaId == EstadoPrima.Pagada)).ToList();
        var mesesPago = visibles.Where(p => p.Prima!.FechaEstimadaPago.HasValue)
            .Select(p => p.Prima!.FechaEstimadaPago!.Value.ToString("yyyy-MM"))
            .Distinct().OrderBy(m => m).ToList();

        var filtradas = visibles.Where(p =>
        {
            var pr = p.Prima!;
            var v = p.Venta!;
            if (f.VendedorId.HasValue && p.VendedorId != f.VendedorId) return false;
            if (f.EstadoPrimaId.HasValue && pr.EstadoPrimaId != f.EstadoPrimaId) return false;
            if (!string.IsNullOrEmpty(f.Mes) && pr.FechaEstimadaPago?.ToString("yyyy-MM") != f.Mes) return false;
            if (f.ProcedenciaId.HasValue && v.ProcedenciaVentaId != f.ProcedenciaId) return false;
            return Coincide(p, f.Q, conExpediente: true);
        }).ToList();

        var ordenadas = Ordenar(filtradas, f.Asc, p => (f.Orden ?? "fechaEstimadaPago") switch
        {
            "expediente" => p.Expediente,
            "nombre" => p.NombreCompleto.ToLowerInvariant(),
            "procedencia" => p.Venta?.ProcedenciaVenta?.Nombre ?? "",
            "vendedor" => p.Vendedor?.Nombre.ToLowerInvariant() ?? "",
            "prima" => p.Prima!.Monto,
            "saldo" => p.Prima!.SaldoPendiente,
            "estado" => p.Prima!.EstadoPrimaId,
            "tipo" => p.EsCliente ? 0 : 1,
            _ => p.Prima!.FechaEstimadaPago?.ToString("yyyy-MM-dd") ?? "0000"
        });

        var filas = ordenadas.Select(p =>
        {
            var pr = p.Prima!;
            return new VentaFilaDto(
                p.Id, p.Expediente, p.NombreCompleto,
                p.NumeroPrincipal(TipoNumero.Telefono)?.Valor,
                p.NumeroPrincipal(TipoNumero.WhatsApp)?.Valor,
                p.Venta?.ProcedenciaVenta?.Nombre,
                p.Venta?.MetodoVenta?.Nombre,
                p.Vendedor?.Etiqueta,
                pr.Monto, pr.SaldoPendiente, pr.FechaEstimadaPago,
                pr.EstadoPrimaId, pr.EstadoPrima?.Nombre ?? "",
                p.EsCliente);
        }).ToList();

        // Las tarjetas se recalculan con los filtros activos. El monto de ventas solo lo ve el administrador.
        var totales = new VentasTotalesDto(
            _usuario.EsAdministrador ? filtradas.Sum(p => p.Venta!.Monto) : null,
            filtradas.Sum(p => p.Prima!.Monto),
            filtradas.Sum(p => p.Prima!.MontoCancelado),
            filtradas.Sum(p => p.Prima!.SaldoPendiente));

        var pagina = Paginado<VentaFilaDto>.Crear(filas, f.Pagina, f.PorPagina);
        return new VentasListaDto(pagina.Filas, pagina.Total, pagina.Pagina, pagina.PorPagina, totales, mesesPago);
    }

    public async Task<ClientesListaDto> ClientesAsync(ListaQuery f, CancellationToken ct = default)
    {
        if (!Roles.VeClientes(_usuario.Rol)) throw new ProhibidoException();
        var clientes = await PersonasCompletas().Where(p => p.ClienteId != null).ToListAsync(ct);
        var anios = clientes.Select(p => p.FechaIngreso.Year).Distinct().OrderBy(a => a).ToList();

        var filtradas = clientes.Where(p =>
        {
            if (f.VendedorId.HasValue && p.VendedorId != f.VendedorId) return false;
            if (f.Anio.HasValue && p.FechaIngreso.Year != f.Anio) return false;
            if (f.ProcedenciaId.HasValue && p.Venta != null && p.Venta.ProcedenciaVentaId != f.ProcedenciaId) return false;
            if (f.OrigenId.HasValue && p.Cliente!.OrigenClienteId != f.OrigenId) return false;
            if (f.EstadoClienteId.HasValue && p.Cliente!.EstadoClienteId != f.EstadoClienteId) return false;
            if (f.EstadoPrimaId.HasValue && (p.Prima == null || p.Prima.EstadoPrimaId != f.EstadoPrimaId)) return false;
            return Coincide(p, f.Q, conExpediente: true);
        }).ToList();

        var orden = f.Orden ?? "expediente";
        var ordenadas = Ordenar(filtradas, f.Asc, p => orden switch
        {
            "nombre" => p.NombreCompleto.ToLowerInvariant(),
            "ingreso" => p.FechaIngreso.ToString("yyyy-MM-dd"),
            "estado" => p.Prima?.EstadoPrimaId ?? 9,
            "estadoCliente" => p.Cliente!.EstadoClienteId,
            "origen" => p.Cliente!.OrigenCliente?.Etiqueta ?? "",
            _ => p.Expediente
        });

        var filas = ordenadas.Select(p => new ClienteFilaDto(
            p.Id, p.Expediente, p.NombreCompleto,
            p.NumeroPrincipal(TipoNumero.Telefono)?.Valor,
            p.CorreoPrincipal?.Correo,
            p.Finca?.Numero,
            p.FechaIngreso,
            p.Prima?.EstadoPrimaId,
            p.Prima?.EstadoPrima?.Nombre,
            p.Cliente!.EstadoClienteId,
            p.Cliente.EstadoCliente?.Nombre ?? "",
            // Cobros no ve el origen del cliente.
            _usuario.Rol == Roles.Cobros ? null : p.Cliente.OrigenCliente?.Etiqueta)).ToList();

        var pagina = Paginado<ClienteFilaDto>.Crear(filas, f.Pagina, f.PorPagina);
        return new ClientesListaDto(pagina.Filas, pagina.Total, pagina.Pagina, pagina.PorPagina, anios);
    }

    private static List<Persona> Ordenar(List<Persona> lista, bool asc, Func<Persona, object> clave)
    {
        var comparador = Comparer<object>.Create((a, b) => (a, b) switch
        {
            (int x, int y) => x.CompareTo(y),
            (decimal x, decimal y) => x.CompareTo(y),
            _ => string.Compare(a?.ToString(), b?.ToString(), new System.Globalization.CultureInfo("es-CR"), System.Globalization.CompareOptions.IgnoreCase)
        });
        var ordenada = asc ? lista.OrderBy(clave, comparador) : lista.OrderByDescending(clave, comparador);
        return ordenada.ThenBy(p => p.Id).ToList();
    }
}
