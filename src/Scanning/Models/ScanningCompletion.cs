namespace ContractWatcher.SDK.Scanning.Models;

/// <summary>
/// Сообщает фоновому воркеру о завершении startup scan.
/// </summary>
/// <remarks>
/// Сохраняет первый результат завершения.
/// Очередь не запускается до проверки актуальных endpoints приложения.
/// </remarks>
internal sealed class ScanningCompletion
{
    /// <summary>
    /// Результат первого запуска сканирования.
    /// </summary>
    private readonly TaskCompletionSource<bool> _source = new(
        TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>
    /// Ожидает завершения startup scan.
    /// </summary>
    internal Task<bool> WaitAsync(CancellationToken cancellationToken) => 
        _source.Task.WaitAsync(cancellationToken);

    /// <summary>
    /// Фиксирует результат сканирования, если он ещё не установлен.
    /// </summary>
    internal void Complete(bool succeeded) => _source.TrySetResult(succeeded);
}