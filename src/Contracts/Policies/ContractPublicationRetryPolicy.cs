using ContractWatcher.MicroDb.Exceptions;
using ContractWatcher.SDK.Contracts.Options;
using ContractWatcher.SDK.Core.Enums;
using ContractWatcher.SDK.Core.Exceptions;
using Microsoft.Extensions.Options;

namespace ContractWatcher.SDK.Contracts.Policies;

/// <summary>
/// Определяет возможность и время повторной доставки контракта.
/// </summary>
/// <remarks>
/// Не изменяет состояние операции.
/// Null означает остановку автоматических повторов.
/// </remarks>
internal sealed class ContractPublicationRetryPolicy(IOptions<ContractPublicationOptions> options)
{
    /// <summary>
    /// Вычисляет время следующей попытки.
    /// </summary>
    /// <param name="exception">Ошибка текущей попытки.</param>
    /// <param name="failedAttempts">
    /// Количество неудач этапа, включая текущую.
    /// </param>
    /// <param name="now">Текущее время.</param>
    /// <returns>
    /// Время следующей попытки либо null,
    /// если ошибка постоянная или исчерпан лимит.
    /// </returns>
    internal DateTimeOffset? GetNextAttemptAt(Exception exception, int failedAttempts, DateTimeOffset now)
    {
        var repeat = IsTransient(exception) &&
            (options.Value.MaxAttempts == 0 || failedAttempts < options.Value.MaxAttempts);

        return repeat ? now + GetDelay(failedAttempts) : null;
    }

    /// <summary>
    /// Вычисляет экспоненциальную задержку с верхним ограничением.
    /// </summary>
    private TimeSpan GetDelay(int attempt) => TimeSpan.FromMilliseconds(Math.Min(
        options.Value.MaxDelay.TotalMilliseconds, 
        options.Value.InitialDelay.TotalMilliseconds *
        Math.Pow(2, Math.Min(attempt - 1, 30))));

    /// <summary>
    /// Определяет ошибки, допускающие повторную попытку.
    /// </summary>
    /// <remarks>
    /// Отмена приложения отсекается исполнителем до обращения к политике.
    /// OperationCanceledException здесь означает внутреннюю отмену операции,
    /// когда токен приложения не отменён.
    /// </remarks>
    private static bool IsTransient(Exception exception) =>
        exception switch
        {
            CoreRequestException core =>
                core.Error is
                    CoreRequestError.NetworkError or
                    CoreRequestError.Timeout or
                    CoreRequestError.ServerError ||
                (int?)core.StatusCode is 408 or 409 or 429,

            HttpRequestException or TimeoutException or OperationCanceledException => true,

            IOException => true,

            MicroDbException { InnerException: IOException } => true,

            _ => false
        };
}