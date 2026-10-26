using System.Text.Json;
using ContractWatcher.MicroDb.Options;
using ContractWatcher.SDK.Configuration.Builders;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ContractWatcher.SDK.Data.Extensions;

/// <summary>
/// Содержит методы подключения локального хранилища SDK.
/// </summary>
internal static class PersistenceBuilderExtensions
{
    /// <summary>
    /// Регистрирует контекст MicroDb с событийным
    /// отслеживанием изменений сущностей.
    /// </summary>
    /// <param name="builder">Builder конфигурации SDK.</param>
    /// <param name="options">Директория базы и параметры JSON-сериализации.</param>
    /// <returns>Исходный builder для продолжения настройки SDK.</returns>
    /// <remarks>
    /// <para>
    /// Контекст регистрируется как singleton: сервисы SDK используют один экземпляр базы.
    /// </para>
    /// <para>
    /// Настройки копируются при регистрации.
    /// Последующие изменения исходных настроек сериализации не влияют на создаваемый контекст.
    /// </para>
    /// <para>
    /// Метод регистрирует контекст.
    /// Существующие реализации хранилища контрактов и publisher автоматически не заменяются.
    /// </para>
    /// </remarks>
    /// <exception cref="ArgumentNullException">
    /// Builder, настройки или параметры сериализации равны null.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// Путь к директории пуст или содержит только пробельные символы.
    /// </exception>
    internal static ContractWatcherBuilder AddMicroDb(this ContractWatcherBuilder builder, MicroDbOptions options)
    {
        if (builder is null)
            throw new ArgumentNullException(nameof(builder));
        
        if (options is null)
            throw new ArgumentNullException(nameof(options));
        
        if (string.IsNullOrWhiteSpace(options.DirectoryPath))
            throw new ArgumentNullException(nameof(options.DirectoryPath));
        
        if (options.JsonSerializerOptions is null)
            throw new ArgumentNullException(nameof(options.JsonSerializerOptions));

        var snapshot = new MicroDbOptions
        {
            DirectoryPath = Path.GetFullPath(options.DirectoryPath),
            JsonSerializerOptions = new JsonSerializerOptions(options.JsonSerializerOptions)
        };

        builder.Services.TryAddSingleton<ContractWatcherDbContext>(
            _ => new ContractWatcherDbContext(snapshot));

        return builder;
    }
}