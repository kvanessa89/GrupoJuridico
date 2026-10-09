using FluentValidation;
using GrupoJuridico.Gestion.Application.Common;
using GrupoJuridico.Gestion.Domain.Entities;

namespace GrupoJuridico.Gestion.Application.Personas;

/// <summary>Reglas al registrar un prospecto (solo aplican al crear, no al editar).</summary>
public class CrearProspectoValidator : AbstractValidator<CrearProspectoRequest>
{
    public const string MensajeContacto = "Ingresá al menos un teléfono o un WhatsApp.";

    public CrearProspectoValidator()
    {
        RuleFor(x => x.Nombres).NotEmpty().WithMessage("Campo requerido.");
        RuleFor(x => x.Apellidos).NotEmpty().WithMessage("Campo requerido.");
        RuleFor(x => x.Cedula).NotEmpty().WithMessage("Campo requerido.");
        RuleFor(x => x.Finca).NotEmpty().WithMessage("Campo requerido.");
        RuleFor(x => x.FechaIngreso).NotNull().WithMessage("Campo requerido.");
        RuleFor(x => x.VendedorId).Must(v => v is > 0).WithMessage("Campo requerido.");
        RuleFor(x => x.ProcedenciaVentaId).Must(v => v is > 0).WithMessage("Campo requerido.");
        RuleFor(x => x.MetodoVentaId).Must(v => v is > 0).WithMessage("Campo requerido.");
        RuleFor(x => x.MontoVenta).GreaterThan(0).WithMessage("Campo requerido.");
        RuleFor(x => x.MontoPrima).GreaterThan(0).WithMessage("Campo requerido.");
        RuleFor(x => x.MontoCancelado).GreaterThanOrEqualTo(0);
        RuleFor(x => x.TelefonoPrincipal)
            .Must((req, _) => !Validacion.Vacio(req.TelefonoPrincipal) || !Validacion.Vacio(req.WhatsappPrincipal))
            .WithMessage(MensajeContacto);
        RuleFor(x => x.WhatsappPrincipal)
            .Must((req, _) => !Validacion.Vacio(req.TelefonoPrincipal) || !Validacion.Vacio(req.WhatsappPrincipal))
            .WithMessage(MensajeContacto);
        RuleFor(x => x.FechaPago)
            .NotNull().When(x => Prima.EstadoPara(x.MontoPrima, x.MontoCancelado) == EstadoPrima.Pagada)
            .WithMessage("Requerida cuando la prima está pagada.");
    }
}

public class ActualizarVentaValidator : AbstractValidator<ActualizarVentaRequest>
{
    public ActualizarVentaValidator()
    {
        RuleFor(x => x.Monto).GreaterThanOrEqualTo(0);
    }
}

public class ActualizarPrimaValidator : AbstractValidator<ActualizarPrimaRequest>
{
    public ActualizarPrimaValidator()
    {
        RuleFor(x => x.Monto).GreaterThanOrEqualTo(0);
        RuleFor(x => x.MontoCancelado).GreaterThanOrEqualTo(0);
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
        RuleFor(x => x.Texto).NotEmpty().WithMessage("Escribí un comentario.").MaximumLength(4000);
    }
}
