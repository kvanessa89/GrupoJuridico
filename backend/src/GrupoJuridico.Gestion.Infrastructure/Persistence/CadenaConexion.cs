using Npgsql;

namespace GrupoJuridico.Gestion.Infrastructure.Persistence;

/// <summary>
/// Acepta la cadena de conexión en formato Npgsql ("Host=…;Database=…") o como URL
/// ("postgresql://usuario:clave@host:puerto/base?sslmode=require"), que es como la entregan
/// servicios como Render. La URL se convierte al formato de Npgsql.
/// </summary>
public static class CadenaConexion
{
    public static string Normalizar(string cadena)
    {
        var valor = cadena.Trim();
        if (!valor.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase)
            && !valor.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase))
            return cadena;

        var uri = new Uri(valor);
        var usuario = uri.UserInfo.Split(':', 2);
        var b = new NpgsqlConnectionStringBuilder
        {
            Host = uri.Host,
            Port = uri.IsDefaultPort || uri.Port <= 0 ? 5432 : uri.Port,
            Database = Uri.UnescapeDataString(uri.AbsolutePath.TrimStart('/')),
            Username = Uri.UnescapeDataString(usuario[0]),
        };
        if (usuario.Length > 1) b.Password = Uri.UnescapeDataString(usuario[1]);

        foreach (var par in uri.Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var kv = par.Split('=', 2);
            if (kv.Length == 2 && kv[0].Equals("sslmode", StringComparison.OrdinalIgnoreCase)
                && Enum.TryParse<SslMode>(Uri.UnescapeDataString(kv[1]).Replace("-", ""), true, out var modo))
                b.SslMode = modo;
        }
        return b.ConnectionString;
    }
}
