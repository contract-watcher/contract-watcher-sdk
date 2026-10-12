using System.Text.Json;
using ContractWatcher.Common.Contracts;
using ContractWatcher.SDK.Execution.Models;
using ContractWatcher.SDK.Validation.Models;

namespace ContractWatcher.SDK.Tests.Fixtures;

public static class ExecutionFixture
{
    public static WatcherContext CreateContext(string contractSlug = "products", JsonElement payload = default) =>
        new() { ContractSlug = contractSlug, Payload = payload };

    public static WatcherContext CreateResolvedContext(
        PublishedContract? contract = null,
        string contractSlug = "products",
        JsonElement payload = default) =>
        new()
        {
            ContractSlug = contractSlug,
            Payload = payload,
            Contract = contract ?? ContractFixture.CreateContract()
        };

    public static WatcherContext CreateValidatedContext(
        PublishedContract? contract = null,
        ContractValidationResult? validationResult = null,
        string contractSlug = "products",
        JsonElement payload = default) =>
        new()
        {
            ContractSlug = contractSlug,
            Payload = payload,
            Contract = contract ?? ContractFixture.CreateContract(),
            ValidationResult = validationResult ?? ValidationFixture.CreateValidationResult()
        };
}