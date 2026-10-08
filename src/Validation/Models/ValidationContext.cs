using System.Text.Json;
using ContractWatcher.Common.Contracts;

namespace ContractWatcher.SDK.Validation.Models;

/// <summary>
/// Содержит данные, необходимые для проверки одного поля входящего JSON согласно правилу контракта
/// </summary>
public sealed class ValidationContext
{
    /// <summary>
    /// Получает правило контракта для проверяемого поля
    /// </summary>
    public required ContractRule Rule { get; init; }

    /// <summary>
    /// Получает значение поля из входящего JSON
    /// </summary>
    public JsonElement? Value { get; init; }
}