using ContractWatcher.Common.Contracts;

namespace ContractWatcher.SDK.Scanning.Models;

/// <summary>
/// Содержит отсканированные правила
/// и решение о версии и публикации.
/// </summary>
/// <remarks>
/// Сформированная версия ещё не является
/// подтверждением публикации в Core.
/// </remarks>
public sealed class ContractMappingResult
{
    /// <summary>
    /// Уникальное имя обнаруженного контракта.
    /// </summary>
    public required string Slug { get; init; }

    /// <summary>
    /// Правила приложения.
    /// Сохраняются даже при недоступном Core.
    /// </summary>
    public required IReadOnlyCollection<ContractRule> Rules { get; init; }

    /// <summary>
    /// Выбранный контракт: опубликованный,
    /// подготовленный к публикации либо локальная копия
    /// при недоступном Core.
    /// Null, если выбрать пока нечего.
    /// </summary>
    public PublishedContract? Contract { get; init; }

    /// <summary>
    /// Версия Core, относительно которой принято решение.
    /// Null — неизвестна; 0 — отсутствие подтверждено.
    /// </summary>
    public int? ExpectedVersion { get; init; }

    /// <summary>
    /// Назначенная SDK версия публикации.
    /// Null, если версия не назначена.
    /// </summary>
    public int? TargetVersion { get; init; }

    /// <summary>
    /// Требуется публикация либо повторная сверка
    /// после ошибки обращения к Core.
    /// </summary>
    public bool Pending { get; init; }
}