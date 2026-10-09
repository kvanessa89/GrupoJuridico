using GrupoJuridico.Gestion.Application.Common.Interfaces;
using GrupoJuridico.Gestion.Domain.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace GrupoJuridico.Gestion.Infrastructure.Persistence;

/// <summary>
/// Antes de cada SaveChanges llena ModificadoEn / ModificadoPorId de los registros IAuditable
/// creados o modificados con la fecha UTC y el usuario autenticado de la petición.
/// </summary>
public class AuditoriaInterceptor : SaveChangesInterceptor
{
    private readonly IUsuarioActual _usuario;
    private readonly IFechaActual _fecha;

    public AuditoriaInterceptor(IUsuarioActual usuario, IFechaActual fecha)
    {
        _usuario = usuario;
        _fecha = fecha;
    }

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        Auditar(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        Auditar(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void Auditar(DbContext? db)
    {
        if (db == null) return;
        var ahora = _fecha.AhoraUtc;
        var usuarioId = _usuario.Id;

        foreach (var entry in db.ChangeTracker.Entries<IAuditable>())
        {
            if (entry.State is not (EntityState.Added or EntityState.Modified)) continue;
            entry.Entity.ModificadoEn = ahora;
            entry.Entity.ModificadoPorId = usuarioId;
        }
    }
}
