using System.Text.Json.Nodes;
using ContractWatcher.Common.Contracts;
using ContractWatcher.SDK.Scanning.Interfaces;

namespace ContractWatcher.SDK.Scanning;

/// <summary>
/// Координирует поиск endpoints, генерацию схем и формирование контрактов.
/// </summary>
/// <remarks>
/// Не выполняет persistence и HTTP-запросы.
/// Однократный вызов после старта приложения обеспечивает startup worker.
/// </remarks>
internal sealed class ContractScanner(
    IEndpointDiscovery discovery,
    IJsonSchemaGenerator schemaGenerator,
    IContractMapper contractMapper) : IContractScanner
{
    public IReadOnlyCollection<PublishedContract> Scan(CancellationToken cancellationToken = default)
    {
        var endpoints = discovery.Discover(cancellationToken);
        var contracts = new List<PublishedContract>(endpoints.Count);

        // Кеш действует только в рамках одного прохода.
        // Один DTO может использоваться несколькими endpoints.
        var schemas = new Dictionary<Type, JsonNode>();

        foreach (var endpoint in endpoints)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!schemas.TryGetValue(endpoint.RequestType, out var schema))
            {
                schema = schemaGenerator.Generate(endpoint.RequestType);
                schemas.Add(endpoint.RequestType, schema);
            }

            cancellationToken.ThrowIfCancellationRequested();

            var contract = contractMapper.Map(endpoint.Slug, version: 1, schema: schema);
            
            // TODO: подключить определение версии через существующее хранилище.
            //
            // 1. Получить сохранённый контракт по endpoint.Slug.
            // 2. Если контракта нет — оставить версию 1.
            // 3. Сравнить Rules по содержимому, не учитывая порядок правил:
            //    FieldName, Type, Required, Nullable.
            // 4. Если правила совпадают — сохранить предыдущую версию.
            // 5. Если отличаются — использовать checked(previousVersion + 1).

            contracts.Add(contract);
        }

        return contracts.ToArray();
    }
}