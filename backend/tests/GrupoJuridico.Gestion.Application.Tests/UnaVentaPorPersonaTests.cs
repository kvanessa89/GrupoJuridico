using GrupoJuridico.Gestion.Domain.Entities;
using GrupoJuridico.Gestion.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace GrupoJuridico.Gestion.Application.Tests;

/// <summary>Una persona tiene una sola venta: la base rechaza una segunda.</summary>
public sealed class UnaVentaPorPersonaTests : IDisposable
{
    private readonly SqliteConnection _conexion = new("Data Source=:memory:");
    private readonly DbContextOptions<ApplicationDbContext> _opciones;

    public UnaVentaPorPersonaTests()
    {
        _conexion.Open();
        _opciones = new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(_conexion).Options;
        using var db = new ApplicationDbContext(_opciones);
        db.Database.EnsureCreated();
    }

    public void Dispose() => _conexion.Dispose();

    [Fact]
    public void Una_segunda_venta_para_la_misma_persona_se_rechaza()
    {
        int personaId;
        using (var db = new ApplicationDbContext(_opciones))
        {
            var persona = new Persona { Nombres = "Ana", Apellidos = "Vargas", FechaIngreso = new DateOnly(2026, 10, 20) };
            persona.Ventas.Add(new Venta { Monto = 1000m });
            db.Personas.Add(persona);
            db.SaveChanges();
            personaId = persona.Id;
        }

        using (var db = new ApplicationDbContext(_opciones))
        {
            db.Ventas.Add(new Venta { PersonaId = personaId, Monto = 500m });
            Assert.Throws<DbUpdateException>(() => db.SaveChanges());
        }

        using (var db = new ApplicationDbContext(_opciones))
            Assert.Equal(1, db.Ventas.Count(v => v.PersonaId == personaId));
    }

    [Fact]
    public void Personas_distintas_tienen_cada_una_su_venta()
    {
        using var db = new ApplicationDbContext(_opciones);
        foreach (var nombre in new[] { "Ana", "Luis" })
        {
            var persona = new Persona { Nombres = nombre, Apellidos = "Vargas", FechaIngreso = new DateOnly(2026, 10, 20) };
            persona.Ventas.Add(new Venta { Monto = 1000m });
            db.Personas.Add(persona);
        }
        db.SaveChanges();
        Assert.Equal(2, db.Ventas.Count());
    }
}
