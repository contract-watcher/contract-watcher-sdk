using ContractWatcher.Common.Contracts;
using ContractWatcher.SDK.Validation.Interfaces;
using ContractWatcher.SDK.Validation.Models;

namespace ContractWatcher.SDK.Validation;

/// <summary>
/// Выполняет зарегистрированные правила проверки в порядке их добавления в контейнер зависимостей
/// </summary>
public class ValidationPipeline : IValidationPipeline
{
    private readonly IReadOnlyCollection<IValidationRule> _rules;

    /// <summary>
    /// Инициализирует новый экземпляр конвейера проверки
    /// </summary>
    /// <param name="rules">Зарегистрированные правила проверки</param>
    public ValidationPipeline(IEnumerable<IValidationRule> rules)
    {
        if (rules is null)
            throw new ArgumentNullException(nameof(rules));
        
        _rules = rules.ToArray();
    }
    
    public IReadOnlyCollection<ValidationViolation> Validate(ValidationContext context)
    {
        if  (context is null)
            throw new ArgumentNullException(nameof(context));

        var violations = new List<ValidationViolation>();

        foreach (var rule in _rules)
        {
            if (!rule.CanValidate(context))
                continue;

            var violation = rule.Validate(context);

            if (violation is not null)
                violations.Add(violation);
        }

        return violations;
    }
}