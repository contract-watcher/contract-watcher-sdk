namespace ContractWatcher.SDK.Scanning.Enums;

/// <summary>
/// Зона ответственности модуля сканирования.
/// </summary>
public enum ScanningStage
{
    /// <summary>
    /// Поиск endpoints и определение request DTO.
    /// </summary>
    Discovery,
    
    /// <summary>
    /// Получение опубликованного состояния до генерации и сравнения схем.
    /// </summary>
    Synchronization,

    /// <summary>
    /// Генерация JSON Schema.
    /// </summary>
    Schema,

    /// <summary>
    /// Формирование PublishedContract.
    /// </summary>
    Mapping,

    /// <summary>
    /// Локальное сохранение контрактов.
    /// </summary>
    Persistence,

    /// <summary>
    /// Публикация контрактов.
    /// </summary>
    Publication,

    /// <summary>
    /// Дополнительный пользовательский этап.
    /// </summary>
    Custom
}