using ContractWatcher.SDK.Scanning.Enums;

namespace ContractWatcher.SDK.Scanning.Exceptions;

/// <summary>
/// Описывает исчерпание попыток конкретного модуля.
/// </summary>
public sealed class ScanningException : Exception
{
    /// <summary>
    /// Этап, на котором остановился пайплайн.
    /// </summary>
    public ScanningStage Stage { get; }

    /// <summary>
    /// Тип упавшего модуля.
    /// </summary>
    public Type ModuleType { get; }

    /// <summary>
    /// Количество выполненных попыток.
    /// </summary>
    public int Attempts { get; }

    /// <summary>
    /// Обрабатываемый объект, если модуль указал его.
    /// </summary>
    public string? Item { get; }

    /// <summary>
    /// Создаёт ошибку этапа, сохраняя исходную причину.
    /// </summary>
    public ScanningException(ScanningStage stage, Type moduleType, int attempts, string? item, Exception innerException)
        : base($"Scanning stage '{stage}' ({moduleType.Name}) failed after {attempts} attempts. Item: '{item ?? "-"}'.", innerException)
    {
        Stage = stage;
        ModuleType = moduleType;
        Attempts = attempts;
        Item = item;
    }
}