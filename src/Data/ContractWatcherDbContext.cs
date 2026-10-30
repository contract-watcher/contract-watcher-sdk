using ContractWatcher.MicroDb;
using ContractWatcher.MicroDb.Options;
using ContractWatcher.MicroDb.Tracking.Enums;
using ContractWatcher.SDK.Data.Domain;

namespace ContractWatcher.SDK.Data;

/// <summary>
/// Объединяет локальные контракты, отчёты и записи повторной отправки SDK.
/// </summary>
/// <remarks>
/// <para>
/// Все наборы используют событийный трекинг.
/// Изменения свойств найденной сущности сохраняются через SaveChangesAsync без вызова Update или Upsert.
/// </para>
/// <para>
/// Каждый набор имеет отдельный JSON-файл. Общей транзакции между файлами нет.
/// </para>
/// </remarks>
internal sealed class ContractWatcherDbContext : MicroDbContext
{
    /// <summary>
    /// Получает контракты.
    /// Ключ — Slug, файл — contracts.json.
    /// </summary>
    public MicroDbSet<ContractEntity, string> Contracts { get; }

    /// <summary>
    /// Получает отчёты.
    /// Ключ — ReportId, файл — reports.json.
    /// </summary>
    public MicroDbSet<ReportEntity, Guid> Reports { get; }

    /// <summary>
    /// Получает ретраи.
    /// Ключ — ReportId, файл — report-retries.json.
    /// </summary>
    public MicroDbSet<ReportRetryEntity, Guid> ReportRetries { get; }
    
    /// <summary>
    /// Результаты сканирования и незавершённые намерения публикации.
    /// </summary>
    public MicroDbSet<ScannedContractEntity, string> ScannedContracts { get; }
    
    /// <summary>
    /// Этапы и расписание повторной публикации контрактов.
    /// Ключ — Slug, файл — contract-retries.json.
    /// </summary>
    public MicroDbSet<ContractRetryEntity, string> ContractRetries { get; }

    /// <summary>
    /// Открывает наборы и включает события изменения сущностей.
    /// </summary>
    /// <param name="options">
    /// Директория базы и настройки JSON-сериализации.
    /// </param>
    public ContractWatcherDbContext(MicroDbOptions options) : base(options)
    {
        Contracts = Set<ContractEntity, string>(
            "contracts", static entity => entity.Slug, ChangeTrackingMode.Events);
        Reports = Set<ReportEntity, Guid>(
            "reports", static entity => entity.ReportId, ChangeTrackingMode.Events);
        ReportRetries = Set<ReportRetryEntity, Guid>(
            "report-retries", static entity => entity.ReportId, ChangeTrackingMode.Events);
        ScannedContracts = Set<ScannedContractEntity, string>(
            "scanned-contracts", static entity => entity.Slug, ChangeTrackingMode.Events);
        ContractRetries = Set<ContractRetryEntity, string>(
            "contract-retries", static entity => entity.Slug, ChangeTrackingMode.Events);
    }
}