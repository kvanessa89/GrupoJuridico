using GrupoJuridico.Gestion.Domain.Common;

namespace GrupoJuridico.Gestion.Domain.Entities;

/// <summary>Vendedor ("Asesor"). Se muestra como "Asesor {código} - {nombre}".</summary>
public class Vendedor : Catalogo
{
    public string Apellidos { get; set; } = string.Empty;

    public string Etiqueta => "Asesor " + (string.IsNullOrWhiteSpace(Codigo) ? "" : Codigo + " - ") + Nombre;
}

/// <summary>Procedencia de la venta: Presencial, Virtual.</summary>
public class ProcedenciaVenta : Catalogo { }

/// <summary>Método de venta: Edictos, Whatsapp, Brouchure…</summary>
public class MetodoVenta : Catalogo { }

/// <summary>
/// Estado de la prima. Los ids 1, 2 y 3 tienen significado fijo (pendiente, incompleta, pagada)
/// porque el estado se deriva de los montos; el nombre es editable.
/// </summary>
public class EstadoPrima : Catalogo
{
    public const int Pendiente = 1;
    public const int Incompleta = 2;
    public const int Pagada = 3;
}

/// <summary>Origen del cliente (P1…P14).</summary>
public class OrigenCliente : Catalogo
{
    public string Etiqueta => Codigo + " - " + Nombre;
}

/// <summary>Estado del cliente oficial: Al Día, Moroso, Estrella, Terminado, Abandonado, Sin Expediente.</summary>
public class EstadoCliente : Catalogo
{
    public const int AlDia = 1;
    public const int Moroso = 2;
    public const int Estrella = 3;
    public const int Terminado = 4;
    public const int Abandonado = 5;
    public const int SinExpediente = 6;
}

/// <summary>Valores sueltos de configuración (por ejemplo el interés de morosidad).</summary>
public class ConfiguracionSistema : IAuditable
{
    public const string InteresMora = "InteresMora";

    public string Clave { get; set; } = string.Empty;
    public string Valor { get; set; } = string.Empty;

    public DateTime ModificadoEn { get; set; }
    public int? ModificadoPorId { get; set; }
}
