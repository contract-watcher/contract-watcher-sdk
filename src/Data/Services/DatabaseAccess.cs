namespace ContractWatcher.SDK.Data.Services;

/// <summary>
/// Согласует чтение DTO, изменение tracked-сущностей
/// и сохранение единого контекста SDK.
/// </summary>
/// <remarks>
/// Потокобезопасность словаря MicroDb не делает свойства
/// и вложенные коллекции сущностей потокобезопасными.
/// Все сервисы хранения используют этот singleton.
/// </remarks>
internal sealed class DatabaseAccess(ContractWatcherDbContext context) : IDisposable
{
    /// <summary>
    /// Ограничивает доступ к изменяемому состоянию контекста
    /// одной операцией.
    /// </summary>
    private readonly SemaphoreSlim _gate = new(1, 1);

    /// <summary>
    /// Читает согласованный снимок данных.
    /// </summary>
    /// <typeparam name="T">Тип результата чтения.</typeparam>
    /// <param name="read">
    /// Преобразование данных в DTO или независимый массив.
    /// </param>
    /// <remarks>
    /// Синхронное чтение может ожидать завершения текущего сохранения.
    /// Изменяемые сущности не должны выходить за пределы операции.
    /// </remarks>
    internal T Read<T>(Func<ContractWatcherDbContext, T> read)
    {
        _gate.Wait();

        try
        {
            return read(context);
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>
    /// Выполняет изменение и подтверждает его
    /// после успешного сохранения.
    /// </summary>
    /// <typeparam name="T">Тип результата операции.</typeparam>
    /// <param name="change">
    /// Операция над сущностями контекста.
    /// </param>
    /// <param name="cancellationToken">
    /// Токен отмены ожидания и записи.
    /// </param>
    /// <remarks>
    /// <para>
    /// Перед новой операцией завершается запись предыдущих изменений,
    /// если она ранее завершилась ошибкой.
    /// MicroDb не откатывает сущности в памяти при ошибках IO.
    /// </para>
    /// <para>
    /// ConfigureAwait(false) позволяет синхронным читателям ждать
    /// без захвата SynchronizationContext.
    /// </para>
    /// </remarks>
    internal async Task<T> WriteAsync<T>(
        Func<ContractWatcherDbContext, T> change,
        CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            await context.SaveChangesAsync(cancellationToken)
                .ConfigureAwait(false);

            var result = change(context);

            await context.SaveChangesAsync(cancellationToken)
                .ConfigureAwait(false);

            return result;
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>
    /// Освобождает средство синхронизации после остановки сервисов SDK.
    /// </summary>
    public void Dispose() => _gate.Dispose();
}