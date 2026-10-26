using System.Reflection;
using System.Text.Json.Serialization;
using ContractWatcher.SDK.AspNetCore.Attributes;
using ContractWatcher.SDK.Scanning;
using ContractWatcher.SDK.Scanning.Enums;
using ContractWatcher.SDK.Scanning.Interfaces;
using ContractWatcher.SDK.Scanning.Models;
using ContractWatcher.SDK.Scanning.Modules;
using ContractWatcher.SDK.Scanning.Options;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Moq;

namespace ContractWatcher.SDK.Tests.Fixtures;

public static class ScanningFixture
{
    public sealed class Request
    {
        [JsonPropertyName("display_name")]
        public required string Name { get; init; }

        public decimal Price { get; init; }

        public string? Description { get; init; }

        public Details? Address { get; init; }

        public string[]? Tags { get; init; }

        [JsonIgnore]
        public string? Secret { get; init; }
    }

    public sealed class ConflictingRequest
    {
        [JsonPropertyName("same")]
        public string? First { get; init; }

        [JsonPropertyName("same")]
        public string? Second { get; init; }
    }

    public sealed class Details
    {
        public string? City { get; init; }
    }

    [NonController]
    [ApiController]
    [Route("scanner")]
    public sealed class ScanController : ControllerBase
    {
        [HttpPost("explicit")]
        [ContractWatcher("products.explicit")]
        public IActionResult Explicit(
            [FromBody] Request request,
            [FromQuery] int page,
            CancellationToken cancellationToken) => Ok();

        [HttpPost("inferred")]
        [ContractWatcher("products.inferred")]
        public IActionResult Inferred(Request request) => Ok();

        [HttpGet("ordinary")]
        public IActionResult Ordinary() => Ok();

        [NonAction]
        [ContractWatcher("products.explicit")]
        public void Duplicate(Request request) { }
    }

    public static DiscoveredEndpoint Endpoint(string slug = "products", Type? requestType = null) => new()
    {
        Slug = slug,
        Method = typeof(ScanController).GetMethod(nameof(ScanController.Explicit))!,
        RequestType = requestType ?? typeof(Request)
    };

    public static ScanningContext Context(params DiscoveredEndpoint[] endpoints) =>
        new() { Endpoints = endpoints };

    public static ControllerActionDescriptor ActionDescriptor(string methodName, params BindingSource?[] sources)
    {
        var method = typeof(ScanController).GetMethod(methodName)!;

        return new ControllerActionDescriptor
        {
            MethodInfo = method,
            ControllerTypeInfo = typeof(ScanController).GetTypeInfo(),
            DisplayName = methodName,
            Parameters = method.GetParameters()
                .Select((parameter, index) =>
                    (ParameterDescriptor)new ControllerParameterDescriptor
                    {
                        Name = parameter.Name!,
                        ParameterType = parameter.ParameterType,
                        ParameterInfo = parameter,
                        BindingInfo = new BindingInfo
                        {
                            BindingSource = index < sources.Length ? sources[index] : null
                        }
                    })
                .ToList()
        };
    }

    public static EndpointDiscoveryModule Discovery(params ActionDescriptor[] actions)
    {
        var provider = new Mock<IActionDescriptorCollectionProvider>();

        provider.SetupGet(value => value.ActionDescriptors)
            .Returns(new ActionDescriptorCollection(actions, 0));

        return new EndpointDiscoveryModule(provider.Object);
    }

    public static SchemaGenerationModule SchemaModule(bool strictNumbers = true)
    {
        var options = new JsonOptions();

        if (strictNumbers)
            options.JsonSerializerOptions.NumberHandling = JsonNumberHandling.Strict;

        return new SchemaGenerationModule(Options.Create(options));
    }

    internal static ContractScanner Scanner(
        IEnumerable<IScanningModule> modules,
        int attempts = 5,
        TimeSpan? delay = null,
        TimeProvider? clock = null) => new(
        modules,
        Options.Create(new ScanningRetryOptions
        {
            MaxAttempts = attempts,
            Delay = delay ?? TimeSpan.Zero
        }),
        clock ?? TimeProvider.System,
        NullLogger<ContractScanner>.Instance);

    public sealed class TestModule(
        ScanningStage stage, 
        Func<ScanningContext, CancellationToken, Task> execute)
        : IScanningModule
    {
        public ScanningStage Stage => stage;

        public Task ExecuteAsync(ScanningContext context, CancellationToken token) => 
            execute(context, token);
    }

    public sealed class ScanningClock : FakeTimeProvider
    {
        public TaskCompletionSource TimerCreated { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
        {
            var timer = base.CreateTimer(callback, state, dueTime, period);

            TimerCreated.TrySetResult();

            return timer;
        }
    }
}