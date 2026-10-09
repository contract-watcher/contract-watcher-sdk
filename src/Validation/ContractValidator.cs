using System.Text.Json;
using ContractWatcher.Common.Contracts;
using ContractWatcher.SDK.Validation.Interfaces;
using ContractWatcher.SDK.Validation.Models;

namespace ContractWatcher.SDK.Validation;

/// <summary>
/// Выполняет локальную проверку входящего JSON согласно правилам опубликованного контракта
/// </summary>
public sealed class ContractValidator : IContractValidator
{
    private readonly IValidationPipeline _pipeline;

    /// <summary>
    /// Инициализирует новый экземпляр валидатора контракта
    /// </summary>
    /// <param name="pipeline"> Конвейер правил проверки отдельных полей </param>
    public ContractValidator(IValidationPipeline pipeline) =>
        _pipeline = pipeline ?? throw new ArgumentNullException(nameof(pipeline));
    
    public ContractValidationResult Validate(JsonElement payload, PublishedContract contract)
    {
        if (contract is null)
            throw new ArgumentNullException(nameof(contract));

        if (payload.ValueKind != JsonValueKind.Object)
            throw new ArgumentException("Корневой элемент JSON должен быть объектом", nameof(payload));

        var violations = new List<ValidationViolation>();

        foreach (var rule in contract.Rules)
        {
            var value = payload.TryGetProperty(rule.FieldName, out var property) ? property : (JsonElement?)null;
            var context = new ValidationContext { Rule = rule, Value = value };

            violations.AddRange(_pipeline.Validate(context));
        }

        return new ContractValidationResult { Violations = violations };
    }
}