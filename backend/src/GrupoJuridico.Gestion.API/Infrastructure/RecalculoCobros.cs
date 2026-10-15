using GrupoJuridico.Gestion.Application.Cobros;

namespace GrupoJuridico.Gestion.Api.Infrastructure;

/// <summary>
/// Recalcula periódicamente los estados de Cobros (cuotas vencidas, promesas incumplidas, clientes morosos),
/// porque cambian con el paso de los días aunque nadie abra la sección.
/// </summary>
public class RecalculoCobros : BackgroundService
{
    private readonly IServiceScopeFactory _scopes;
    private readonly ILogger<RecalculoCobros> _log;
    private readonly TimeSpan _intervalo;

    public RecalculoCobros(IServiceScopeFactory scopes, ILogger<RecalculoCobros> log, IConfiguration config)
    {
        _scopes = scopes;
        _log = log;
        _intervalo = TimeSpan.FromMinutes(Math.Max(1, config.GetValue("Cobros:RecalculoMinutos", 60)));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var temporizador = new PeriodicTimer(_intervalo);
        do
        {
            try
            {
                using var scope = _scopes.CreateScope();
                await scope.ServiceProvider.GetRequiredService<CobrosService>().NormalizarTodosAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "No se pudo recalcular los estados de cobros.");
            }
        } while (await temporizador.WaitForNextTickAsync(stoppingToken));
    }
}
