using GrupoJuridico.Gestion.Application.Common.Interfaces;
using GrupoJuridico.Gestion.Domain.Entities;

namespace GrupoJuridico.Gestion.Application.Tests;

/// <summary>Catálogos de estado del cliente con los mismos ids fijos que carga DbSeeder.</summary>
internal static class EstadosClientePrueba
{
    public static void Agregar(IApplicationDbContext db)
    {
        db.EstadosCliente.AddRange(
            new EstadoCliente { Id = EstadoCliente.Activo, Codigo = "EC-1", Nombre = "Activo" },
            new EstadoCliente { Id = EstadoCliente.Terminado, Codigo = "EC-4", Nombre = "Terminado" },
            new EstadoCliente { Id = EstadoCliente.Abandonado, Codigo = "EC-5", Nombre = "Abandonado" },
            new EstadoCliente { Id = EstadoCliente.SinExpediente, Codigo = "EC-6", Nombre = "Sin Expediente" });
        db.EstadosClienteCalculado.AddRange(
            new EstadoClienteCalculado { Id = EstadoClienteCalculado.AlDia, Codigo = "ECC-1", Nombre = "Al Día" },
            new EstadoClienteCalculado { Id = EstadoClienteCalculado.Moroso, Codigo = "ECC-2", Nombre = "Moroso" },
            new EstadoClienteCalculado { Id = EstadoClienteCalculado.Estrella, Codigo = "ECC-3", Nombre = "Estrella" });
    }
}
