namespace ContractWatcher.SDK.Core.Enums;

/// <summary>
/// Тип ошибки при обращении к Core
/// </summary>
internal enum CoreRequestError
{
    Unauthorized,
    Forbidden,
    ServerError,
    Timeout,
    NetworkError,
    UnexpectedResponse
}