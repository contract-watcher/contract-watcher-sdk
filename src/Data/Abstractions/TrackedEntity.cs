using ContractWatcher.MicroDb.Tracking.Abstractions;

namespace ContractWatcher.SDK.Data.Abstractions;

/// <summary>
/// Предоставляет уведомление MicroDb об изменении
/// сущности или вложенного объекта.
/// </summary>
/// <remarks>
/// Не хранит состояние трекера и fingerprints:
/// ими управляет MicroDb.
/// </remarks>
internal abstract class TrackedEntity : IEntityChangeSource
{
    public event EventHandler? Changed;

    /// <summary>
    /// Изменяет значение свойства и уведомляет подписчиков,
    /// если новое значение отличается от текущего.
    /// </summary>
    /// <typeparam name="T">Тип значения свойства.</typeparam>
    /// <param name="storage">Поле, хранящее текущее значение.</param>
    /// <param name="value">Новое значение свойства.</param>
    protected void SetValue<T>(ref T storage, T value)
    {
        if (EqualityComparer<T>.Default.Equals(storage, value))
            return;

        storage = value;
        OnChanged();
    }

    /// <summary>
    /// Сообщает об изменении сохраняемого состояния объекта.
    /// </summary>
    protected void OnChanged() => Changed?.Invoke(this, EventArgs.Empty);
}