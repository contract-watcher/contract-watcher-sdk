namespace ContractWatcher.SDK.Data.Enums;

/// <summary>
/// Определяет состояние доставки отчёта
/// независимо от результата проверки.
/// </summary>
internal enum ReportDeliveryState
{
    /// <summary>
    /// Ожидает первоначальной или повторной отправки.
    /// </summary>
    Pending,

    /// <summary>
    /// Core подтвердил приём отчёта.
    /// </summary>
    Delivered,

    /// <summary>
    /// Автоматические попытки прекращены.
    /// Отчёт сохранён для разбора.
    /// </summary>
    Failed
}