using ContractWatcher.Common.Contracts;
using ContractWatcher.SDK.Validation.Interfaces;
using ContractWatcher.SDK.Validation.Models;

namespace ContractWatcher.SDK.Tests.Fakes;

public sealed class FakeValidationRule: IValidationRule
{
    private readonly bool _canValidate;
    private readonly ValidationViolation? _violation;
    private readonly Action? _onValidate;

    public FakeValidationRule(bool canValidate = true, ValidationViolation? violation = null, Action? onValidate = null)
    {
        _canValidate = canValidate;
        _violation = violation;
        _onValidate = onValidate;
    }

    public bool CanValidate(ValidationContext context) => _canValidate;

    public ValidationViolation? Validate(ValidationContext context)
    {
        _onValidate?.Invoke();

        return _violation;
    }
}