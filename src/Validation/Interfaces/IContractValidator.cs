using System.Text.Json;
using ContractWatcher.Common.Contracts;
using ContractWatcher.SDK.Validation.Models;

namespace ContractWatcher.SDK.Validation.Interfaces;

/// <summary>
/// Представляет сервис локальной проверки входящего JSON согласно опубликованному контракту
/// </summary>
public interface IContractValidator
{
    /// <summary>
    /// Проверяет входящий JSON согласно указанному контракту
    /// </summary>
    /// <param name="payload">Входящий JSON</param>
    /// <param name="contract">Опубликованный контракт</param>
    /// <returns>Результат проверки</returns>
    ContractValidationResult Validate(JsonElement payload, PublishedContract contract);
}