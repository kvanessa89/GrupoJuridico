using FluentValidation;
using GrupoJuridico.Gestion.Application.Common.Exceptions;

namespace GrupoJuridico.Gestion.Application.Common;

public static class Validacion
{
    /// <summary>Ejecuta el validador y lanza <see cref="ValidacionException"/> con los errores agrupados por campo.</summary>
    public static async Task ValidarAsync<T>(this IValidator<T> validador, T instancia, CancellationToken ct = default)
    {
        var resultado = await validador.ValidateAsync(instancia, ct);
        if (resultado.IsValid) return;
        var errores = resultado.Errors
            .GroupBy(e => CamelCase(e.PropertyName))
            .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).Distinct().ToArray());
        throw new ValidacionException(errores);
    }

    public static bool Vacio(string? s) => string.IsNullOrWhiteSpace(s);

    /// <summary>Las claves de error siguen el nombre JSON del campo (camelCase).</summary>
    public static string CamelCase(string nombre) =>
        string.IsNullOrEmpty(nombre) ? nombre : char.ToLowerInvariant(nombre[0]) + nombre[1..];
}
