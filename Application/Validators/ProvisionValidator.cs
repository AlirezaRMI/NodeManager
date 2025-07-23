
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
        
        RuleFor(x => x.InboundPort)
            .InclusiveBetween(1024, 65535)
            .WithMessage("Suggested Inbound Port must be between 1024 and 65535."); 
        
        RuleFor(x => x.XrayPort)
            .InclusiveBetween(1024, 65535)
            .WithMessage("Suggested Xray Port must be between 1024 and 65535.");
        
        RuleFor(x => x.ServerPort)
            .InclusiveBetween(1024, 65535)
            .WithMessage("Suggested Server Port must be between 1024 and 65535.");
        
    }
}