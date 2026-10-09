using System.Globalization;
using FluentValidation;
using GrupoJuridico.Gestion.Application.Auth;
using GrupoJuridico.Gestion.Application.Catalogos;
using GrupoJuridico.Gestion.Application.Personas;
using GrupoJuridico.Gestion.Application.Usuarios;
using Microsoft.Extensions.DependencyInjection;

namespace GrupoJuridico.Gestion.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        // Mensajes por defecto de FluentValidation en español.
        ValidatorOptions.Global.LanguageManager.Culture = new CultureInfo("es");

        services.AddScoped<AuthService>();
        services.AddScoped<UsuariosService>();
        services.AddScoped<CatalogosService>();
        services.AddScoped<ListasService>();
        services.AddScoped<PersonasService>();
        return services;
    }
}
