using GrupoJuridico.Gestion.Application.Common.Exceptions;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace GrupoJuridico.Gestion.Api.Infrastructure;

/// <summary>Traduce las excepciones de la capa Application a respuestas ProblemDetails.</summary>
public class ManejadorExcepciones : IExceptionHandler
{
    private readonly ILogger<ManejadorExcepciones> _log;
    private readonly IHostEnvironment _env;

    public ManejadorExcepciones(ILogger<ManejadorExcepciones> log, IHostEnvironment env)
    {
        _log = log;
        _env = env;
    }

    public async ValueTask<bool> TryHandleAsync(HttpContext http, Exception ex, CancellationToken ct)
    {
        ProblemDetails problema = ex switch
        {
            ValidacionException v => new ValidationProblemDetails(v.Errores)
            {
                Status = StatusCodes.Status400BadRequest,
                Title = v.Message
            },
            NoEncontradoException => new ProblemDetails { Status = StatusCodes.Status404NotFound, Title = ex.Message },
            ProhibidoException => new ProblemDetails { Status = StatusCodes.Status403Forbidden, Title = ex.Message },
            _ => new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "Ocurrió un error inesperado.",
                Detail = _env.IsDevelopment() ? ex.ToString() : null
            }
        };
        if (problema.Status == StatusCodes.Status500InternalServerError)
            _log.LogError(ex, "Error no controlado en {Ruta}", http.Request.Path);

        http.Response.StatusCode = problema.Status!.Value;
        await http.Response.WriteAsJsonAsync(problema, problema.GetType(), options: null, contentType: "application/problem+json", ct);
        return true;
    }
}
