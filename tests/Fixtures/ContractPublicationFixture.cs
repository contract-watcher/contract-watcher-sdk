using ContractWatcher.SDK.Contracts.Interfaces;
using ContractWatcher.SDK.Contracts.Models;
using ContractWatcher.SDK.Contracts.Options;
using ContractWatcher.SDK.Contracts.Policies;
using ContractWatcher.SDK.Contracts.Services;
using ContractWatcher.SDK.Contracts.Stores;
using ContractWatcher.SDK.Data.Domain;
using ContractWatcher.SDK.Scanning.Enums;
using ContractWatcher.SDK.Scanning.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace ContractWatcher.SDK.Tests.Fixtures;

/// <summary>
/// Создаёт сеанс доставки поверх существующей MicroDbFixture.
/// </summary>
/// <remarks>
/// Файлы принадлежат MicroDbFixture и сохраняются
/// между последовательными сеансами публикации.
/// </remarks>
internal sealed class ContractPublicationFixture : IDisposable
{
    private readonly ServiceProvider _provider;

    internal MicroDbSession Session { get; }

    internal ContractDeliveryGate Gate { get; } = new();

    internal ContractDeliveryService Delivery { get; }

    internal ContractPublicationFixture(
        MicroDbFixture fixture,
        IContractStore? store = null,
        int attempts = 5,
        ContractSourcePreference preference = ContractSourcePreference.Application,
        ILogger<ContractDeliveryService>? logger = null)
    {
        Session = fixture.Open();

        var publication = Options.Create(new ContractPublicationOptions
        {
            MaxAttempts = attempts,
            InitialDelay = TimeSpan.FromSeconds(1),
            MaxDelay = TimeSpan.FromSeconds(1)
        });

        var deliveryStore = new ContractDeliveryStore(
            Session.Access,
            new ContractPublicationRetryPolicy(publication),
            publication,
            fixture.Clock);

        _provider = new ServiceCollection()
            .AddSingleton(fixture.Client.Object)
            .AddSingleton(store ?? Session.Contracts)
            .AddSingleton(deliveryStore)
            .AddScoped<ContractDeliveryProcessor>()
            .BuildServiceProvider();

        Delivery = new ContractDeliveryService(
            deliveryStore,
            _provider.GetRequiredService<IServiceScopeFactory>(),
            Gate,
            Options.Create(new ContractSynchronizationOptions
            {
                SourcePreference = preference
            }),
            logger ?? NullLogger<ContractDeliveryService>.Instance);
    }

    /// <summary>
    /// Сохраняет неподтверждённый результат сканирования.
    /// Null в version означает необходимость синхронизации с Core.
    /// </summary>
    internal async Task SeedAsync(string slug = "products", int? version = 1)
    {
        Session.Context.ScannedContracts.Add(new ScannedContractEntity
        {
            Slug = slug,
            Revision = Guid.NewGuid(),
            Pending = true,
            TargetVersion = version
        });

        await Session.Context.SaveChangesAsync();
    }

    public void Dispose()
    {
        _provider.Dispose();
        Gate.Dispose();
        Session.Dispose();
    }
}