using GrupoJuridico.Gestion.Application.Common;

namespace GrupoJuridico.Gestion.Application.Common.Exceptions;

public class NoEncontradoException : Exception
{
    public NoEncontradoException(string entidad, object id) : base($"{entidad} {id} no existe.") { }
}

public class ProhibidoException : Exception
{
    public ProhibidoException(string mensaje = "No tiene permiso para esta acción.") : base(mensaje) { }
}

/// <summary>Errores de validación por campo; la API los devuelve como ValidationProblemDetails (400).</summary>
public class ValidacionException : Exception
{
    public IDictionary<string, string[]> Errores { get; }

    public ValidacionException(IDictionary<string, string[]> errores)
        : base("Uno o más campos no son válidos.")
    {
        Errores = errores;
    }

    public ValidacionException(string campo, string mensaje)
        : this(new Dictionary<string, string[]> { [Validacion.CamelCase(campo)] = new[] { mensaje } }) { }
}


public class VersionRequeridaException : Exception
{
    public VersionRequeridaException() : base("Debe enviar la versión de la sección en If-Match.") { }
}
public class ConflictoVersionException : Exception
{
    public ConflictoVersionException(int? modificadoPorId = null, string? modificadoPor = null, DateTime? modificadoEn = null)
        : base("Otra sesión modificó esta sección. Revise la versión guardada antes de continuar.")
    {
        ModificadoPorId = modificadoPorId;
        ModificadoPor = modificadoPor;
        ModificadoEn = modificadoEn;
    }

    /// <summary>Último usuario que guardó la sección; nulo si no se conoce.</summary>
    public int? ModificadoPorId { get; }
    public string? ModificadoPor { get; }
    public DateTime? ModificadoEn { get; }
}
