
using Domain.Models.Provision;
using FluentValidation;


namespace Application.Validators;

public class ProvisionValidator : AbstractValidator<ProvisionRequestDto>
{
    public ProvisionValidator()
    {
        
        RuleFor(x => x.SshPrivateKey)
            .NotEmpty()
            .WithMessage("Encrypted SSH Private Key content is required.")
            .MaximumLength(4000)
            .WithMessage("SSH Private Key content cannot exceed 4000 characters.");
        
        
        RuleFor(x => x.XrayContainerImage)
            .NotEmpty()
            .WithMessage("Xray container image name is required.")
            .MaximumLength(255)
            .WithMessage("Xray container image name cannot exceed 255 characters.");
        
        RuleFor(x => x.CustomerId)
            .GreaterThan(0)
            .WithMessage("Customer ID must be a positive value.");
        
        
        RuleFor(x => x)
            .Must(x =>
            {
                var usedPorts = new HashSet<int>();
                if (x.InboundPort.HasValue && !usedPorts.Add(x.InboundPort.Value)) return false;
                if (x.XrayPort.HasValue && !usedPorts.Add(x.XrayPort.Value)) return false;
                if (x.ServerPort.HasValue && !usedPorts.Add(x.ServerPort.Value)) return false;
                return true; 
            }).WithMessage("Suggested ports cannot conflict with each other.");
    }
}