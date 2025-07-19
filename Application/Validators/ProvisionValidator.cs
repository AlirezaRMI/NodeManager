
using Domain.Models.Provision;
using FluentValidation;


namespace Application.Validators;

public class ProvisionValidator : AbstractValidator<ProvisionRequestDto>
{
    public ProvisionValidator()
    {
        
        
        RuleFor(x => x.XrayContainerImage)
            .NotEmpty()
            .WithMessage("Xray container image name is required.")
            .MaximumLength(255)
            .WithMessage("Xray container image name cannot exceed 255 characters.");
        
        RuleFor(x => x.CustomerId)
            .GreaterThan(0)
            .WithMessage("Customer ID must be a positive value.");
        
    }
}