using System.Collections.Concurrent;
using System.Runtime.ExceptionServices;
using System.Text.Json;
using ContractWatcher.Common.Contracts;
using ContractWatcher.SDK.Core.Enums;
using ContractWatcher.SDK.Core.Exceptions;
using ContractWatcher.SDK.Core.Interfaces;
using ContractWatcher.SDK.Data.Domain;
using ContractWatcher.SDK.Data.Enums;
using ContractWatcher.SDK.Data.Mappings;
using ContractWatcher.SDK.Data.Services;
using ContractWatcher.SDK.Reporting.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ContractWatcher.SDK.Reporting.Services;

/// <summary>
/// Сохраняет отчёты и координирует первоначальную
/// и повторную отправку в Core.
/// </summary>
/// <remarks>
/// Singleton разделяет отметки выполняющихся отправок
/// между publisher и воркером.
/// Сохраняемые сущности изменяются через свойства
/// и существующий событийный трекер MicroDb.
/// </remarks>
internal sealed class ReportDeliveryService(
    DatabaseAccess database,
    IServiceScopeFactory scopeFactory,
    IOptions<ReportRetryOptions> options,
    TimeProvider clock,
    ILogger<ReportDeliveryService> logger)
{
    /// <summary>
    /// Отчёты, отправка которых уже выполняется в текущем процессе.
    /// </summary>
    private readonly ConcurrentDictionary<Guid, byte> _sending = new();

    /// <summary>
    /// Сохраняет отчёт до HTTP-запроса
    /// и выполняет первоначальную попытку отправки.
    /// </summary>
    /// <param name="report">
    /// Отчёт с постоянным ReportId для всех попыток.
    /// </param>
    /// <param name="cancellationToken">
    /// Токен отмены. Уже сохранённый Pending-отчёт остаётся в очереди.
    /// </param>
    internal async Task PublishAsync(ValidationReport report, CancellationToken cancellationToken)
    {
        if (report is null)
            throw new ArgumentNullException(nameof(report));

        if (report.ReportId == Guid.Empty)
            throw new ArgumentException("ReportId is required.", nameof(report));

        var entity = report.ToEntity(clock.GetUtcNow());

        await database.WriteAsync(db =>
        {
            var existing = db.Reports.Find(entity.ReportId);

            if (existing is null)
                db.Reports.Add(entity);
            else if (JsonSerializer.Serialize(existing.ToDto()) != JsonSerializer.Serialize(entity.ToDto()))
                throw new InvalidOperationException("ReportId is already associated with another payload.");

            return true;
        }, cancellationToken).ConfigureAwait(false);

        await TrySendAsync(entity.ReportId, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Получает партию ожидающих отчётов
    /// с наступившим сроком отправки.
    /// </summary>
    /// <remarks>
    /// Отсутствие retry-записи у Pending-отчёта означает готовность
    /// к отправке после прерывания первоначальной попытки.
    /// </remarks>
    internal Task<Guid[]> GetDueAsync(CancellationToken cancellationToken) =>
        database.WriteAsync(db =>
        {
            var now = clock.GetUtcNow();

            return db.Reports
                .Where(entity => entity.DeliveryState == ReportDeliveryState.Pending)
                .Where(entity => !_sending.ContainsKey(entity.ReportId))
                .Where(entity =>
                    db.ReportRetries.Find(entity.ReportId) is not { } retry ||
                    retry.NextAttemptAt is { } due && due <= now)
                .OrderBy(entity => entity.StoredAt)
                .ThenBy(entity => entity.ReportId)
                .Take(options.Value.BatchSize)
                .Select(entity => entity.ReportId)
                .ToArray();
        }, cancellationToken);

    /// <summary>
    /// Отправляет отчёт, если он ожидает доставки
    /// и не занят другим отправителем.
    /// </summary>
    /// <param name="reportId">Идентификатор сохранённого отчёта.</param>
    /// <param name="cancellationToken">Токен отмены текущей попытки.</param>
    /// <remarks>
    /// <para>
    /// Временные ошибки сохраняют расписание и не выбрасываются
    /// в вызывающую проверку.
    /// </para>
    /// <para>
    /// Постоянные ошибки сохраняют Failed и затем передаются
    /// вызывающему коду. Ошибки диска также передаются вызывающему коду.
    /// </para>
    /// </remarks>
    internal async Task TrySendAsync(Guid reportId, CancellationToken cancellationToken)
    {
        if (!_sending.TryAdd(reportId, 0))
            return;

        try
        {
            var payload = await database.WriteAsync(db =>
            {
                var entity = db.Reports.Find(reportId);

                if (entity is null || entity.DeliveryState != ReportDeliveryState.Pending)
                    return null;

                var retry = db.ReportRetries.Find(reportId);

                if (retry is not null &&
                    (retry.NextAttemptAt is not { } nextAttemptAt ||
                     nextAttemptAt > clock.GetUtcNow()))
                    return null;

                return entity.ToDto();
            }, cancellationToken).ConfigureAwait(false);

            if (payload is null)
                return;

            // Typed HttpClient не удерживается singleton-сервисом на всё время жизни приложения.
            await using var scope = scopeFactory.CreateAsyncScope();

            var client = scope.ServiceProvider.GetRequiredService<IContractWatcherCoreClient>();

            Exception? failure = null;

            try
            {
                await client.PublishReportAsync(payload, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (Exception exception)when (!cancellationToken.IsCancellationRequested)
            {
                failure = exception;
            }

            if (failure is null)
            {
                await database.WriteAsync(db =>
                {
                    var entity = db.Reports.Find(reportId)!;

                    entity.DeliveryState = ReportDeliveryState.Delivered;
                    entity.DeliveredAt = clock.GetUtcNow();

                    return true;
                }, cancellationToken).ConfigureAwait(false);

                // Подтверждение в reports.json сохраняется раньше удаления retry-записи.
                await database.WriteAsync(db => db.ReportRetries.Remove(reportId), cancellationToken)
                    .ConfigureAwait(false);

                return;
            }

            var retryable = await database.WriteAsync(db =>
            {
                var retry = db.ReportRetries.Find(reportId);

                if (retry is null)
                {
                    retry = new ReportRetryEntity { ReportId = reportId };

                    db.ReportRetries.Add(retry);
                }

                retry.FailedAttempts = checked(retry.FailedAttempts + 1);
                retry.LastAttemptAt = clock.GetUtcNow();
                retry.LastError = failure.Message;

                var repeat = IsTransient(failure) && 
                             (options.Value.MaxAttempts == 0 || 
                              retry.FailedAttempts < options.Value.MaxAttempts);

                retry.NextAttemptAt = repeat ? retry.LastAttemptAt + GetDelay(retry.FailedAttempts) : null;

                if (!repeat)
                    db.Reports.Find(reportId)!.DeliveryState = ReportDeliveryState.Failed;

                return repeat;
            }, cancellationToken).ConfigureAwait(false);

            logger.LogWarning(
                failure,
                "Report {ReportId} delivery failed; retry scheduled: {Retry}.",
                reportId,
                retryable);

            if (!retryable)
                ExceptionDispatchInfo.Capture(failure).Throw();
        }
        finally
        {
            _sending.TryRemove(reportId, out _);
        }
    }

    /// <summary>
    /// Удаляет остаточные retry-записи после подтверждения доставки
    /// или удаления соответствующего отчёта.
    /// </summary>
    internal Task<int> CleanupAsync(
        CancellationToken cancellationToken) =>
        database.WriteAsync(db =>
        {
            var ids = db.ReportRetries
                .Where(retry =>
                    db.Reports.Find(retry.ReportId) is null or
                    { DeliveryState: ReportDeliveryState.Delivered })
                .Select(retry => retry.ReportId)
                .ToArray();

            foreach (var id in ids)
                db.ReportRetries.Remove(id);

            return ids.Length;
        }, cancellationToken);

    /// <summary>
    /// Распознаёт сетевые ошибки, таймауты и HTTP 408, 429, 5xx стандартного клиента Core.
    /// </summary>
    private static bool IsTransient(Exception exception) =>
        exception switch
        {
            CoreRequestException core =>
                core.Error is
                    CoreRequestError.NetworkError or
                    CoreRequestError.Timeout or
                    CoreRequestError.ServerError ||
                (int?)core.StatusCode is 408 or 429,

            HttpRequestException => true,
            OperationCanceledException => true,
            TimeoutException => true,
            _ => false
        };

    /// <summary>
    /// Вычисляет экспоненциальную задержку с добавкой до 20% и установленным пределом.
    /// </summary>
    private TimeSpan GetDelay(int attempts)
    {
        var delay = 
            options.Value.InitialDelay.TotalMilliseconds * Math.Pow(2, Math.Min(attempts - 1, 30));

        delay *= 1 + Random.Shared.NextDouble() * 0.2;

        return TimeSpan.FromMilliseconds(
            Math.Min(delay, options.Value.MaxDelay.TotalMilliseconds));
    }
}