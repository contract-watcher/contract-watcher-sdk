using ContractWatcher.SDK.Scanning.Enums;
using ContractWatcher.SDK.Scanning.Models;

namespace ContractWatcher.SDK.Scanning.Interfaces;

/// <summary>
/// Представляет самостоятельный этап сканирования.
/// </summary>
public interface IScanningModule
{
    /// <summary>
    /// Зона ответственности модуля.
    /// Используется для диагностики, а не для определения порядка выполнения.
    /// </summary>
    ScanningStage Stage { get; }

    /// <summary>
    /// Выполняет этап, используя данные общего контекста.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Модуль не вызывает другие модули.
    /// Переходами и повторными попытками управляет исполнитель.
    /// </para>
    /// <para>
    /// При повторе передаются тот же контекст и тот же экземпляр модуля.
    /// Полный результат записывается в контекст после успешного завершения.
    /// </para>
    /// </remarks>
    Task ExecuteAsync(ScanningContext context, CancellationToken cancellationToken);
}