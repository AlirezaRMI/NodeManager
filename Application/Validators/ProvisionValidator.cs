
using Domain.Models.Provision;
using FluentValidation;


namespace Application.Validators;

public class ProvisionValidator : AbstractValidator<ProvisionRequestDto>
{
    public ProvisionValidator()
    {
        
        
        RuleFor(x => x.SshHost)
            .NotEmpty()
            .WithMessage("SSH host address is required.")
            .MaximumLength(255)
            .WithMessage("SSH host address cannot exceed 255 characters.");
        
        RuleFor(x => x.SshPort)
            .InclusiveBetween(1, 65535)
            .WithMessage("SSH port must be between 1 and 65535.");
        
        RuleFor(x => x.SshUsername)
            .NotEmpty()
            .WithMessage("SSH username is required.")
            .MaximumLength(50)
            .WithMessage("SSH username cannot exceed 50 characters.");
        
        RuleFor(x => x.SshPrivateKey)
            .NotEmpty()
            .WithMessage("Encrypted SSH Private Key content is required.")
            .MaximumLength(4000)
            .WithMessage("SSH Private Key content cannot exceed 4000 characters.");
        
        RuleFor(x => x.SshPassword) 
            .MaximumLength(255)
            .WithMessage("SSH Passphrase cannot exceed 255 characters.")
            .When(x => !string.IsNullOrEmpty(x.SshPassword)); 
        
        RuleFor(x => x.XrayContainerImage)
            .NotEmpty()
            .WithMessage("Xray container image name is required.")
            .MaximumLength(255)
            .WithMessage("Xray container image name cannot exceed 255 characters.");
        
        RuleFor(x => x.CustomerId)
            .GreaterThan(0)
            .WithMessage("Customer ID must be a positive value.");
        
        RuleFor(x => x.InboundPort)
            .InclusiveBetween(1024, 65535)
            .WithMessage("Suggested Inbound Port must be between 1024 and 65535.")
            .When(x => x.InboundPort.HasValue); 
        
        RuleFor(x => x.XrayPort)
            .InclusiveBetween(1024, 65535)
            .WithMessage("Suggested Xray Port must be between 1024 and 65535.")
            .When(x => x.XrayPort.HasValue);
        
        RuleFor(x => x.ServerPort)
            .InclusiveBetween(1024, 65535)
            .WithMessage("Suggested Server Port must be between 1024 and 65535.")
            .When(x => x.ServerPort.HasValue);
        
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