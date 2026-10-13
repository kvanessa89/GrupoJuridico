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

    public const int MaxNotasVenta = 1000;
    public const string MensajeFinca = "Use el formato 12-12345 (2 dígitos, guion y 5 dígitos).";
    public const string MensajeExpediente = "Use el formato 12-123456-1234-AB (2, 6 y 4 dígitos, y 2 letras).";

    public static bool FincaValida(string? valor) =>
        System.Text.RegularExpressions.Regex.IsMatch(valor?.Trim() ?? "", @"\A[0-9]{2}-[0-9]{5}\z");
    public static bool ExpedienteValido(string? valor) => Vacio(valor) ||
        System.Text.RegularExpressions.Regex.IsMatch(valor!.Trim(), @"\A[0-9]{2}-[0-9]{6}-[0-9]{4}-[A-Za-z]{2}\z");

    public static bool Vacio(string? s) => string.IsNullOrWhiteSpace(s);

    /// <summary>Los teléfonos se guardan solo con dígitos; el prefijo va en el país.</summary>
    public static string SoloDigitos(string? s) => new((s ?? "").Where(char.IsAsciiDigit).ToArray());

    /// <summary>Valida el número local (el prefijo se guarda aparte). Vacío permite contactos opcionales.</summary>
    public static void Telefono(string? numero, string codigoPais, string campo)
    {
        if (Vacio(numero)) return;
        var largo = SoloDigitos(codigoPais) switch { "506" => 8, _ => 0 };
        if (largo > 0 && SoloDigitos(numero).Length != largo)
            throw new ValidacionException(campo, $"El número debe tener {largo} dígitos, sin el prefijo del país.");
    }

    /// <summary>Correo con forma usuario@dominio.ext, sin espacios. Vacío se considera válido (es opcional).</summary>
    public static bool CorreoValido(string? s) =>
        Vacio(s) || System.Text.RegularExpressions.Regex.IsMatch(s!.Trim(), @"^[^@\s]+@[^@\s]+\.[^@\s]+$");

    /// <summary>Limpia y valida un correo opcional: formato y largo máximo.</summary>
    public static string Correo(string? s, string campo, int maximo)
    {
        var limpio = s?.Trim() ?? "";
        if (limpio.Length > maximo)
            throw new ValidacionException(campo, $"El correo admite como máximo {maximo} caracteres.");
        if (!CorreoValido(limpio))
            throw new ValidacionException(campo, "Correo electrónico no válido.");
        return limpio;
    }

    /// <summary>Las claves de error siguen el nombre JSON del campo (camelCase).</summary>
    public static string CamelCase(string nombre) =>
        string.IsNullOrEmpty(nombre) ? nombre : char.ToLowerInvariant(nombre[0]) + nombre[1..];
}


