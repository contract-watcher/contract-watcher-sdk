using System.Net;
using ContractWatcher.SDK.Core.Enums;

namespace ContractWatcher.SDK.Core.Exceptions;

/// <summary>
/// Ошибка взаимодействия с Core
/// </summary>
public sealed class CoreRequestException : Exception
{
    public CoreRequestException(
        CoreRequestError error,
        string message,
        HttpStatusCode? statusCode = null,
        Exception? innerException = null)
        : base(message, innerException)
    {
        Error = error;
        StatusCode = statusCode;
    }

    public CoreRequestError Error { get; }

    public HttpStatusCode? StatusCode { get; }
}