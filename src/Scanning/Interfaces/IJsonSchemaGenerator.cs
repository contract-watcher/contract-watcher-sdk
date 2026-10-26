using System.Text.Json.Nodes;

namespace ContractWatcher.SDK.Scanning.Interfaces;

/// <summary>
/// Генерирует JSON Schema для CLR-типа входной модели.
/// </summary>
internal interface IJsonSchemaGenerator
{
    /// <summary>
    /// Формирует схему с учётом настроек JSON пользовательского приложения.
    /// </summary>
    /// <param name="requestType">Тип входной модели.</param>
    JsonNode Generate(Type requestType);
}