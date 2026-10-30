using ContractWatcher.MicroDb.Options;

namespace ContractWatcher.SDK.Configuration.Options;

/// <summary>
/// Настройки ContractWatcher
/// </summary>
public sealed class ContractWatcherOptions
{
    /// <summary>
    /// Адрес ContractWatcher Core
    /// </summary>
    public string CoreUrl { get; set; } = string.Empty;

    /// <summary>
    /// Ключ интеграции
    /// </summary>
    public string IntegrationKey { get; set; } = string.Empty;
    
    /// <summary>
    /// Интервал фонового обновления опубликованных контрактов
    /// </summary>
    public TimeSpan ContractRefreshInterval { get; set; } = TimeSpan.FromMinutes(5);
    
    /// <summary>
    /// Настройки локальной базы контрактов, отчётов
    /// и записей повторной отправки.
    /// </summary>
    /// <remarks>
    /// Директория должна принадлежать одной интеграции и одному процессу-писателю.
    /// </remarks>
    public MicroDbOptions Database { get; set; } = new()
    {
        DirectoryPath = Path.Combine(AppContext.BaseDirectory, "data", "contract-watcher")
    };
    
    /// <summary>
    /// Таймаут одного HTTP-запроса к Core.
    /// </summary>
    public TimeSpan CoreRequestTimeout { get; set; } = TimeSpan.FromSeconds(10);

    /// <summary>
    /// Максимальный размер буферизуемого ответа Core в байтах.
    /// </summary>
    public long MaxCoreResponseBytes { get; set; } = 1024 * 1024;
}