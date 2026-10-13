using FluentValidation;
using GrupoJuridico.Gestion.Application.Common;
using GrupoJuridico.Gestion.Domain.Entities;

namespace GrupoJuridico.Gestion.Application.Personas;

/// <summary>Reglas al registrar un prospecto (solo aplican al crear, no al editar).</summary>
public class CrearProspectoValidator : AbstractValidator<CrearProspectoRequest>
{
    public const string MensajeContacto = "Ingrese al menos un teléfono o un WhatsApp.";
    public static readonly string MensajeMaxNombre = $"Máximo {Persona.MaxNombre} caracteres.";

    public CrearProspectoValidator()
    {
        RuleFor(x => x.Nombres).NotEmpty().WithMessage("Campo requerido.")
            .MaximumLength(Persona.MaxNombre).WithMessage(MensajeMaxNombre);
        RuleFor(x => x.Apellidos).NotEmpty().WithMessage("Campo requerido.")
            .MaximumLength(Persona.MaxNombre).WithMessage(MensajeMaxNombre);
        // La cédula se guarda solo con dígitos.
        RuleFor(x => x.Cedula).Must(c => Validacion.SoloDigitos(c).Length > 0).WithMessage("Campo requerido.")
            .Must(c => Validacion.SoloDigitos(c).Length <= Persona.MaxCedula).WithMessage($"Máximo {Persona.MaxCedula} dígitos.");
        RuleFor(x => x.CorreoPrincipal)
            .Must(c => (c?.Trim().Length ?? 0) <= Persona.MaxCorreo).WithMessage($"Máximo {Persona.MaxCorreo} caracteres.")
            .Must(Validacion.CorreoValido).WithMessage("Correo electrónico no válido.");
        RuleForEach(x => x.Familiares).Must(f => Validacion.CorreoValido(f.CorreoElectronico) && (f.CorreoElectronico?.Trim().Length ?? 0) <= Persona.MaxCorreo)
            .WithMessage("El correo de un familiar no es válido.");
        RuleForEach(x => x.Familiares).Must(f => !Validacion.Vacio(f.NombreCompleto))
            .WithMessage("El nombre del familiar es requerido.");
        RuleForEach(x => x.Familiares).Must(f => (f.NombreCompleto?.Trim().Length ?? 0) <= PersonaFamiliar.MaxNombreCompleto)
            .WithMessage($"El nombre de un familiar admite como máximo {PersonaFamiliar.MaxNombreCompleto} caracteres.");
        RuleForEach(x => x.Familiares).Must(f =>
                Validacion.SoloDigitos(f.Telefono).Length > 0 || !Validacion.Vacio(f.CorreoElectronico))
            .WithMessage("Ingrese un teléfono o un correo del familiar.");
        RuleFor(x => x.Finca).NotEmpty().WithMessage("Campo requerido.");
        RuleFor(x => x.FechaIngreso).Must(d => d.HasValue && d.Value != default).WithMessage("Campo requerido.");
        RuleFor(x => x.VendedorId).Must(v => v is > 0).WithMessage("Campo requerido.");
        RuleFor(x => x.ProcedenciaVentaId).Must(v => v is > 0).WithMessage("Campo requerido.");
        RuleFor(x => x.MetodoVentaId).Must(v => v is > 0).WithMessage("Campo requerido.");
        RuleFor(x => x.MontoVenta).GreaterThan(0).WithMessage("Campo requerido.");
        RuleFor(x => x.MontoPrima).GreaterThan(0).WithMessage("Campo requerido.");
        RuleFor(x => x.MontoCancelado).GreaterThanOrEqualTo(0);
        // Los teléfonos se guardan solo con dígitos: "abc" cuenta como vacío.
        RuleFor(x => x.TelefonoPrincipal)
            .Must((req, _) => TieneContacto(req))
            .WithMessage(MensajeContacto);
        RuleFor(x => x.WhatsappPrincipal)
            .Must((req, _) => TieneContacto(req))
            .WithMessage(MensajeContacto);
        RuleFor(x => x.FechaPago)
            .NotNull().When(x => Prima.EstadoPara(x.MontoPrima, x.MontoCancelado) == EstadoPrima.Pagada)
            .WithMessage("Requerida cuando la prima está pagada.");
    }

    private static bool TieneContacto(CrearProspectoRequest r) =>
        Validacion.SoloDigitos(r.TelefonoPrincipal).Length > 0 || Validacion.SoloDigitos(r.WhatsappPrincipal).Length > 0;
}

public class ActualizarDatosValidator : AbstractValidator<ActualizarDatosRequest>
{
    public ActualizarDatosValidator()
    {
        RuleFor(x => x.Nombres).NotEmpty().WithMessage("Campo requerido.");
        RuleFor(x => x.Apellidos).NotEmpty().WithMessage("Campo requerido.");
        RuleFor(x => x.Cedula).Must(c => Validacion.SoloDigitos(c).Length > 0).WithMessage("Campo requerido.");
        RuleFor(x => x.Finca).NotEmpty().WithMessage("Campo requerido.");
        RuleFor(x => x.FechaIngreso).NotEqual(default(DateOnly)).WithMessage("Campo requerido.");
        RuleFor(x => x.VendedorId).Must(v => v is > 0).WithMessage("Campo requerido.");
    }
}

public class ActualizarVentaValidator : AbstractValidator<ActualizarVentaRequest>
{
    public ActualizarVentaValidator()
    {
        RuleFor(x => x.ProcedenciaVentaId).Must(v => v is > 0).WithMessage("Campo requerido.");
        RuleFor(x => x.MetodoVentaId).Must(v => v is > 0).WithMessage("Campo requerido.");
        RuleFor(x => x.Monto).GreaterThan(0).WithMessage("Debe ser mayor que cero.");
    }
}

public class ActualizarPrimaValidator : AbstractValidator<ActualizarPrimaRequest>
{
    public ActualizarPrimaValidator()
    {
        RuleFor(x => x.Monto).GreaterThan(0).WithMessage("Debe ser mayor que cero.");
        RuleFor(x => x.MontoCancelado).GreaterThanOrEqualTo(0);
        // Igual que al crear: una prima pagada no se guarda sin su fecha de pago.
        RuleFor(x => x.FechaPago)
            .NotNull().When(x => Prima.EstadoPara(x.Monto, x.MontoCancelado) == EstadoPrima.Pagada)
            .WithMessage("Requerida cuando la prima está pagada.");
    }
}

public class ConvertirClienteValidator : AbstractValidator<ConvertirClienteRequest>
{
    public ConvertirClienteValidator()
    {
        RuleFor(x => x.OrigenClienteId).Must(v => v is > 0).WithMessage("El origen del cliente es requerido.");
    }
}

public class ComentarioValidator : AbstractValidator<ComentarioRequest>
{
    public ComentarioValidator()
    {
        RuleFor(x => x.Texto).NotEmpty().WithMessage("Escriba un comentario.").MaximumLength(4000);
    }
}

