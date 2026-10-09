namespace GrupoJuridico.Gestion.Application.Common.Models;

public record Paginado<T>(IReadOnlyList<T> Filas, int Total, int Pagina, int PorPagina)
{
    public static Paginado<T> Crear(IReadOnlyList<T> todos, int pagina, int porPagina)
    {
        porPagina = Math.Clamp(porPagina, 1, 200);
        var totalPaginas = Math.Max(1, (int)Math.Ceiling(todos.Count / (double)porPagina));
        pagina = Math.Clamp(pagina, 1, totalPaginas);
        var filas = todos.Skip((pagina - 1) * porPagina).Take(porPagina).ToList();
        return new Paginado<T>(filas, todos.Count, pagina, porPagina);
    }
}
