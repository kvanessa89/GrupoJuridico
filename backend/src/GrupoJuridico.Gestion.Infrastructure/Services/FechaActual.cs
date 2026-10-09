using GrupoJuridico.Gestion.Application.Common.Interfaces;

namespace GrupoJuridico.Gestion.Infrastructure.Services;

/// <summary>"Hoy" según la hora de Costa Rica, para que el mes actual no cambie a las 6 p. m. por UTC.</summary>
public class FechaActual : IFechaActual
{
    private static readonly TimeZoneInfo Zona = BuscarZona();

    public DateOnly Hoy => DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, Zona));
    public DateTime AhoraUtc => DateTime.UtcNow;

    private static TimeZoneInfo BuscarZona()
    {
        foreach (var id in new[] { "America/Costa_Rica", "Central America Standard Time" })
        {
            try { return TimeZoneInfo.FindSystemTimeZoneById(id); }
            catch (TimeZoneNotFoundException) { }
            catch (InvalidTimeZoneException) { }
        }
        return TimeZoneInfo.CreateCustomTimeZone("CR", TimeSpan.FromHours(-6), "Costa Rica", "Costa Rica");
    }
}
