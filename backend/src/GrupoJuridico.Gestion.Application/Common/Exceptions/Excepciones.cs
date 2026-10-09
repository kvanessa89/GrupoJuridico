using GrupoJuridico.Gestion.Application.Common;

namespace GrupoJuridico.Gestion.Application.Common.Exceptions;

public class NoEncontradoException : Exception
{
    public NoEncontradoException(string entidad, object id) : base($"{entidad} {id} no existe.") { }
}

public class ProhibidoException : Exception
{
    public ProhibidoException(string mensaje = "No tenés permiso para esta acción.") : base(mensaje) { }
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
