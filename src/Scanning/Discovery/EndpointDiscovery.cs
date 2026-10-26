using System.Reflection;
using ContractWatcher.SDK.AspNetCore.Attributes;
using ContractWatcher.SDK.Scanning.Interfaces;
using ContractWatcher.SDK.Scanning.Models;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace ContractWatcher.SDK.Scanning.Discovery;

/// <summary>
/// Находит отмеченные MVC actions по подготовленным метаданным ASP.NET Core.
/// </summary>
/// <remarks>
/// Поддерживает входные модели из request body.
/// Minimal API, form-data и модели из query в этот механизм не входят.
/// </remarks>
internal sealed class EndpointDiscovery(IActionDescriptorCollectionProvider actionProvider) : IEndpointDiscovery
{
    public IReadOnlyCollection<DiscoveredEndpoint> Discover(CancellationToken cancellationToken = default)
    {
        var endpoints = new Dictionary<string, DiscoveredEndpoint>(
            StringComparer.Ordinal);

        var actions = actionProvider.ActionDescriptors.Items
            .OfType<ControllerActionDescriptor>();

        foreach (var action in actions)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var attribute = action.MethodInfo
                .GetCustomAttribute<ContractWatcherAttribute>(inherit: true);

            if (attribute is null)
                continue;

            var slug = attribute.ContractSlug;

            if (string.IsNullOrWhiteSpace(slug))
                throw new InvalidOperationException($"Action '{action.DisplayName}' has an empty contract slug.");

            var requestType = GetRequestType(action);

            if (endpoints.TryGetValue(slug, out var existing))
            {
                // Один метод может иметь несколько маршрутов.
                // Повторное описание того же контракта не требуется.
                if (existing.Method.Equals(action.MethodInfo) && existing.RequestType == requestType)
                    continue;

                throw new InvalidOperationException(
                    $"Contract slug '{slug}' is used by multiple actions or incompatible request models.");
            }

            endpoints.Add(slug, new DiscoveredEndpoint
            {
                Slug = slug,
                Method = action.MethodInfo,
                RequestType = requestType
            });
        }

        return endpoints.Values.ToArray();
    }

    /// <summary>
    /// Определяет единственный параметр, получаемый из тела запроса.
    /// </summary>
    /// <remarks>
    /// Использует итоговый BindingSource MVC:
    /// как явный FromBody, так и автоматически определённый источник.
    /// </remarks>
    private static Type GetRequestType(ControllerActionDescriptor action)
    {
        var parameters = action.Parameters
            .Where(parameter => 
                parameter.BindingInfo?.BindingSource?.CanAcceptDataFrom(BindingSource.Body) == true)
            .ToArray();

        if (parameters.Length != 1)
            throw new InvalidOperationException(
                $"Action '{action.DisplayName}' must have exactly one body parameter to generate a contract.");

        return parameters[0].ParameterType;
    }
}