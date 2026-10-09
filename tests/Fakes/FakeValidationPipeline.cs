using ContractWatcher.Common.Contracts;
using ContractWatcher.SDK.Validation.Interfaces;
using ContractWatcher.SDK.Validation.Models;

namespace ContractWatcher.SDK.Tests.Fakes;

public sealed class FakeValidationPipeline: IValidationPipeline
{
    private readonly IReadOnlyCollection<ValidationViolation> _result;
    public List<ValidationContext> Contexts { get; } = [];

    public FakeValidationPipeline(IReadOnlyCollection<ValidationViolation>? result = null) => _result = result ?? [];

    public IReadOnlyCollection<ValidationViolation> Validate(ValidationContext context)
    {
        Contexts.Add(context);

        return _result;
    }
}