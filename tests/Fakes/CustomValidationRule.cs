using ContractWatcher.Common.Contracts;
using ContractWatcher.SDK.Validation.Interfaces;
using ContractWatcher.SDK.Validation.Models;

namespace ContractWatcher.SDK.Tests.Fakes;

public sealed class CustomValidationRule : IValidationRule
{
    public bool CanValidate(ValidationContext context) => true;

    public ValidationViolation? Validate(ValidationContext context) => null;
}