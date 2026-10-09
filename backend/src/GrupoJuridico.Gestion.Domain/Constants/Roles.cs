namespace GrupoJuridico.Gestion.Domain.Constants;

/// <summary>Roles fijos del sistema. Cada rol ve secciones y columnas distintas.</summary>
public static class Roles
{
    public const string Administrador = "Administrador";
    public const string AsistenteVentas = "Asistente de Ventas";
    public const string Cobros = "Cobros";

    public static readonly string[] Todos = { Administrador, AsistenteVentas, Cobros };

    /// <summary>Roles que no ven primas pagadas en la tabla de ventas.</summary>
    public static bool OcultaPrimasPagadas(string? rol) => rol is AsistenteVentas or Cobros;

    public static bool VeClientes(string? rol) => rol is Administrador or Cobros;
}
