using System.Collections.ObjectModel;
using System.Collections.Specialized;
using ContractWatcher.MicroDb.Tracking.Abstractions;

namespace ContractWatcher.SDK.Data.Abstractions;

/// <summary>
/// Уведомляет владельца об изменении состава коллекции и свойств её элементов.
/// </summary>
/// <typeparam name="T">Тип вложенного объекта с уведомлениями об изменениях.</typeparam>
/// <remarks>
/// <para>Подписки обновляются после добавления, удаления, замены и очистки элементов.</para>
/// <para>Один экземпляр элемента может встречаться несколько раз, но имеет одну подписку.</para>
/// <para>Коллекция и её элементы требуют внешней синхронизации при конкурентных изменениях.</para>
/// </remarks>
internal sealed class TrackedCollection<T> : ObservableCollection<T>, IEntityChangeSource where T : TrackedEntity
{
    /// <summary>
    /// Элементы, на изменения которых оформлена подписка.
    /// Сравнение выполняется по ссылочной идентичности.
    /// </summary>
    private readonly HashSet<T> _subscriptions = new(ReferenceEqualityComparer.Instance);

    /// <inheritdoc />
    public event EventHandler? Changed;

    /// <summary>
    /// Проверяет добавляемый элемент и выполняет добавление.
    /// </summary>
    protected override void InsertItem(int index, T item)
    {
        if (item is null)
            throw new ArgumentNullException(nameof(item));

        base.InsertItem(index, item);
    }

    /// <summary>
    /// Проверяет новый элемент и выполняет замену.
    /// </summary>
    protected override void SetItem(int index, T item)
    {
        if (item is null)
            throw new ArgumentNullException(nameof(item));

        base.SetItem(index, item);
    }

    /// <summary>
    /// Согласует подписки с текущим составом
    /// и уведомляет владельца коллекции.
    /// </summary>
    /// <param name="args">Описание изменения состава или порядка элементов.</param>
    protected override void OnCollectionChanged(NotifyCollectionChangedEventArgs args)
    {
        var current = new HashSet<T>(this, ReferenceEqualityComparer.Instance);

        var removed = _subscriptions
            .Where(item => !current.Contains(item))
            .ToArray();

        foreach (var item in removed)
        {
            item.Changed -= OnItemChanged;
            _subscriptions.Remove(item);
        }

        foreach (var item in current)
            if (_subscriptions.Add(item))
                item.Changed += OnItemChanged;

        Changed?.Invoke(this, EventArgs.Empty);

        base.OnCollectionChanged(args);
    }

    /// <summary>
    /// Передаёт уведомление вложенного элемента владельцу коллекции.
    /// </summary>
    private void OnItemChanged(object? sender, EventArgs args) => Changed?.Invoke(this, EventArgs.Empty);
}