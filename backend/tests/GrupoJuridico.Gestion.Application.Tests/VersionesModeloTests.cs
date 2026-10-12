using GrupoJuridico.Gestion.Domain.Entities;
using GrupoJuridico.Gestion.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace GrupoJuridico.Gestion.Application.Tests;

public class VersionesModeloTests
{
    [Fact]
    public void Snapshot_y_modelo_productivo_coinciden()
    {
        using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql("Host=localhost;Database=solo_modelo;Username=test;Password=test").Options);
        Assert.False(db.Database.HasPendingModelChanges());
        var entidad = db.Model.FindEntityType(typeof(VersionSeccion))!;
        Assert.True(entidad.FindProperty(nameof(VersionSeccion.Version))!.IsConcurrencyToken);
    }

    [Fact]
    public async Task Contexto_productivo_inicializa_versiones_y_detecta_carrera_atomica()
    {
        using var conexion = new SqliteConnection("Data Source=:memory:;Foreign Keys=False");
        await conexion.OpenAsync();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(conexion).Options;
        int id;
        using (var seed = new ApplicationDbContext(options))
        {
            await seed.Database.EnsureCreatedAsync();
            var p = new Persona { Nombres = "Ana" };
            seed.Personas.Add(p);
            await seed.SaveChangesAsync();
            id = p.Id;
            Assert.Equal(4, p.Versiones.Count);
        }
        using var a = new ApplicationDbContext(options);
        using var b = new ApplicationDbContext(options);
        var pa = await a.Personas.Include(p => p.Versiones).SingleAsync(p => p.Id == id);
        var pb = await b.Personas.Include(p => p.Versiones).SingleAsync(p => p.Id == id);
        pa.Versiones.Single(v => v.Seccion == "datos").Version = Guid.NewGuid();
        pa.Nombres = "Primera sesión";
        await a.SaveChangesAsync();
        pb.Versiones.Single(v => v.Seccion == "datos").Version = Guid.NewGuid();
        pb.Nombres = "Segunda sesión";
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => b.SaveChangesAsync());
        using var verificar = new ApplicationDbContext(options);
        Assert.Equal("Primera sesión", (await verificar.Personas.SingleAsync()).Nombres);
    }
}
