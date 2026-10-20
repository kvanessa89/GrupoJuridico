using GrupoJuridico.Gestion.Application.Common.Interfaces;
using GrupoJuridico.Gestion.Infrastructure;
using GrupoJuridico.Gestion.Infrastructure.Persistence;
using GrupoJuridico.Gestion.Infrastructure.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Hosting.Internal;
using Npgsql;

namespace GrupoJuridico.Gestion.Application.Tests;

public class DespliegueTests
{
    [Fact]
    public void La_cadena_en_formato_url_se_convierte_al_formato_de_npgsql()
    {
        var b = new NpgsqlConnectionStringBuilder(CadenaConexion.Normalizar("postgresql://gj_user:cl%40ve%3Asegura@dpg-abc.virginia-postgres.render.com:5433/gj_gestion?sslmode=require"));
        Assert.Equal("dpg-abc.virginia-postgres.render.com", b.Host);
        Assert.Equal(5433, b.Port);
        Assert.Equal("gj_gestion", b.Database);
        Assert.Equal("gj_user", b.Username);
        Assert.Equal("cl@ve:segura", b.Password);
        Assert.Equal(SslMode.Require, b.SslMode);
    }

    [Fact]
    public void La_url_sin_puerto_usa_5432_y_la_cadena_npgsql_queda_igual()
    {
        var b = new NpgsqlConnectionStringBuilder(CadenaConexion.Normalizar("postgres://u:p@dpg-interno/gj"));
        Assert.Equal(5432, b.Port);
        Assert.Equal("dpg-interno", b.Host);

        const string npgsql = "Host=localhost;Database=gj;Username=u;Password=p";
        Assert.Equal(npgsql, CadenaConexion.Normalizar(npgsql));
    }

    [Fact]
    public void Las_claves_de_archivo_son_internas_y_no_aceptan_rutas()
    {
        var clave = ClavesArchivo.Nueva(".PDF");
        Assert.Matches(@"^\d{4}/\d{2}/[0-9a-f]{32}\.pdf$", clave);
        Assert.True(ClavesArchivo.Valida(clave));
        Assert.DoesNotContain(".", ClavesArchivo.Nueva("../../x").Split('/')[2]);
        Assert.False(ClavesArchivo.Valida("../2026/10/abc.pdf"));
        Assert.False(ClavesArchivo.Valida("2026/10/" + new string('a', 32) + "/../x"));
    }

    private static ServiceProvider Proveedor(Dictionary<string, string?> valores)
    {
        valores["ConnectionStrings:Gestion"] = "Host=localhost;Database=gj;Username=u;Password=p";
        var config = new ConfigurationBuilder().AddInMemoryCollection(valores).Build();
        var s = new ServiceCollection();
        s.AddSingleton<IHostEnvironment>(new HostingEnvironment { ContentRootPath = Path.GetTempPath(), EnvironmentName = "Staging" });
        s.AddLogging();
        s.AddInfrastructure(config);
        return s.BuildServiceProvider();
    }

    [Fact]
    public void Por_defecto_los_archivos_van_al_disco_y_con_R2_al_bucket()
    {
        using (var local = Proveedor(new()))
            Assert.IsType<AlmacenArchivosLocal>(local.GetRequiredService<IAlmacenArchivos>());

        using var r2 = Proveedor(new()
        {
            ["Archivos:Proveedor"] = "R2",
            ["Archivos:R2:CuentaId"] = "cuenta",
            ["Archivos:R2:AccessKeyId"] = "id",
            ["Archivos:R2:SecretAccessKey"] = "secreto",
            ["Archivos:R2:Bucket"] = "gj-archivos-pruebas",
        });
        Assert.IsType<AlmacenArchivosR2>(r2.GetRequiredService<IAlmacenArchivos>());
    }

    [Fact]
    public void R2_incompleto_falla_al_iniciar_nombrando_las_variables_sin_mostrar_valores()
    {
        using var sp = Proveedor(new() { ["Archivos:Proveedor"] = "R2", ["Archivos:R2:SecretAccessKey"] = "no-mostrar" });
        var e = Assert.Throws<InvalidOperationException>(() => sp.GetRequiredService<IAlmacenArchivos>());
        Assert.Contains("Archivos__R2__CuentaId", e.Message);
        Assert.Contains("Archivos__R2__AccessKeyId", e.Message);
        Assert.Contains("Archivos__R2__Bucket", e.Message);
        Assert.DoesNotContain("SecretAccessKey", e.Message);
        Assert.DoesNotContain("no-mostrar", e.Message);
    }

    [Fact]
    public void Un_proveedor_desconocido_no_se_acepta()
    {
        var e = Assert.Throws<InvalidOperationException>(() => Proveedor(new() { ["Archivos:Proveedor"] = "S4" }));
        Assert.Contains("'Local' o 'R2'", e.Message);
    }
}
