using ContractWatcher.Common.Contracts;

namespace ContractWatcher.SDK.Scanning.Interfaces;

/// <summary>
/// Координирует автоматическое формирование контрактов приложения.
/// </summary>
internal interface IContractScanner
{
    /// <summary>
    /// Обнаруживает endpoints и формирует их контракты.
    /// </summary>
    /// <param name="cancellationToken">Токен отмены сканирования.</param>
    /// <returns>Контракты обнаруженных endpoints.</returns>
    IReadOnlyCollection<PublishedContract> Scan(CancellationToken cancellationToken = default);
}