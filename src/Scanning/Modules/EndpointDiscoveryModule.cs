using System.Reflection;
using ContractWatcher.SDK.AspNetCore.Attributes;
using ContractWatcher.SDK.Scanning.Enums;
using ContractWatcher.SDK.Scanning.Interfaces;
using ContractWatcher.SDK.Scanning.Models;
using Microsoft.AspNetCore.Mvc.ActionConstraints;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace ContractWatcher.SDK.Scanning.Modules;

/// <summary>
/// Находит отмеченные MVC actions и определяет типы входных моделей.
/// </summary>
/// <remarks>
/// Использует подготовленные метаданные ASP.NET Core.
/// Поддерживает модели, получаемые из request body.
/// </remarks>
public sealed class EndpointDiscoveryModule(IActionDescriptorCollectionProvider actionProvider) : IScanningModule
{
    public ScanningStage Stage => ScanningStage.Discovery;
    
    public Task ExecuteAsync(ScanningContext context, CancellationToken cancellationToken)
    {
        if (context is null)
            throw new ArgumentNullException(nameof(context));
        
        if (cancellationToken.IsCancellationRequested)
            throw new OperationCanceledException();

        var endpoints = new Dictionary<string, DiscoveredEndpoint>(StringComparer.Ordinal);

        var actions = actionProvider.ActionDescriptors.Items.OfType<ControllerActionDescriptor>();

        foreach (var action in actions)
        {
            if (cancellationToken.IsCancellationRequested)
                throw new OperationCanceledException();

            var attribute = action.MethodInfo
                .GetCustomAttribute<ContractWatcherAttribute>(inherit: true);

            if (attribute is null)
                continue;

            context.CurrentItem = action.MethodInfo.Name;

            var slug = attribute.ContractSlug;
            var requestType = GetRequestType(action);
            
            var route = new DiscoveredRoute
            {
                Template = action.AttributeRouteInfo?.Template,
                HttpMethods = (action.ActionConstraints ?? [])
                    .OfType<HttpMethodActionConstraint>()
                    .SelectMany(constraint => constraint.HttpMethods)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(method => method, StringComparer.OrdinalIgnoreCase)
                    .ToArray()
            };

            if (endpoints.TryGetValue(slug, out var existing))
            {
                if (existing.Method.Equals(action.MethodInfo) && existing.RequestType == requestType)
                {
                    endpoints[slug] = new DiscoveredEndpoint
                    {
                        Slug = existing.Slug,
                        Method = existing.Method,
                        RequestType = existing.RequestType,
                        Routes = existing.Routes.Append(route).ToArray()
                    };

                    continue;
                }

                throw new InvalidOperationException(
                    $"Contract slug '{slug}' is used by multiple actions or incompatible request models.");
            }

            endpoints.Add(slug, new DiscoveredEndpoint
            {
                Slug = slug,
                Method = action.MethodInfo,
                RequestType = requestType,
                Routes = [route]
            });
        }

        if (cancellationToken.IsCancellationRequested)
            throw new OperationCanceledException();

        context.Endpoints = endpoints.Values.ToArray();

        return Task.CompletedTask;
    }

    /// <summary>
    /// Определяет единственный параметр, получаемый из тела запроса.
    /// </summary>
    /// <remarks>
    /// Учитывает явный FromBody и автоматически определённый MVC источник.
    /// </remarks>
    private static Type GetRequestType(ControllerActionDescriptor action)
    {
        var parameters = action.Parameters
            .Where(parameter => parameter.BindingInfo?.BindingSource?
                .CanAcceptDataFrom(BindingSource.Body) == true)
            .ToArray();

        if (parameters.Length != 1)
            throw new InvalidOperationException(
                $"Action '{action.DisplayName}' must have exactly one body parameter to generate a contract.");

        return parameters[0].ParameterType;
    }
}