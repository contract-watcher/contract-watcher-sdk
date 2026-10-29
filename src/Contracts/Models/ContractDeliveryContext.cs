using ContractWatcher.Common.Contracts;
using ContractWatcher.SDK.Scanning.Enums;

namespace ContractWatcher.SDK.Contracts.Models;

/// <summary>
/// Содержит данные и текущий этап одной попытки доставки.
/// </summary>
/// <remarks>
/// Создаётся из сохранённого состояния.
/// Не содержит ссылок на изменяемые сущности MicroDb.
/// </remarks>
internal sealed class ContractDeliveryContext
{
    /// <summary>
    /// Slug обрабатываемого контракта.
    /// </summary>
    public required string Slug { get; init; }

    /// <summary>
    /// Независимый снимок правил текущей редакции.
    /// </summary>
    public required IReadOnlyCollection<ContractRule> Rules { get; init; }

    /// <summary>
    /// Этап, с которого продолжается доставка.
    /// </summary>
    public ScanningStage Stage { get; set; }

    /// <summary>
    /// Версия, назначенная SDK.
    /// Null означает, что версия ещё не определена.
    /// </summary>
    public int? Version { get; set; }

    /// <summary>
    /// Формирует контракт для отправки или локального подтверждения.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Версия ещё не определена либо имеет недопустимое значение.
    /// </exception>
    public PublishedContract ToContract() => new()
    {
        Slug = Slug,
        Rules = Rules,
        ContractVersion = Version is > 0 ? Version.Value 
            : throw new InvalidOperationException("Publication version is unknown.")
    };
}