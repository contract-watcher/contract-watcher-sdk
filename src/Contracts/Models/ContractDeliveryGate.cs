namespace ContractWatcher.SDK.Contracts.Models;

/// <summary>
/// Согласует доставку контрактов с заменой отсканированных правил.
/// </summary>
/// <remarks>
/// Не позволяет одновременно отправлять одну редакцию
/// и заменять её содержимое новым результатом сканирования.
/// Блокировка действует внутри одного процесса.
/// </remarks>
internal sealed class ContractDeliveryGate : IDisposable
{
    /// <summary>
    /// Общая блокировка публикации и обновления результатов сканирования.
    /// </summary>
    internal SemaphoreSlim Semaphore { get; } = new(1, 1);

    /// <summary>
    /// Освобождает блокировку после остановки сервисов SDK.
    /// </summary>
    public void Dispose() => Semaphore.Dispose();
}