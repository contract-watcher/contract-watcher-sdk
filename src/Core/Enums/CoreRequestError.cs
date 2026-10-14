namespace ContractWatcher.SDK.Core.Enums;

/// <summary>
/// Тип ошибки при обращении к Core
/// </summary>
public enum CoreRequestError
{
    Unauthorized,
    Forbidden,
    ServerError,
    Timeout,
    NetworkError,
    UnexpectedResponse
}