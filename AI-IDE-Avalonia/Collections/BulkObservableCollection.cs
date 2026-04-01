using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;

namespace AI_IDE_Avalonia.Collections;

/// <summary>
/// An <see cref="ObservableCollection{T}"/> that adds a <see cref="Reset"/> method which
/// replaces all items in a single pass and fires exactly one <see cref="CollectionChanged"/>
/// notification with <see cref="NotifyCollectionChangedAction.Reset"/>.
/// Use this instead of <c>Clear()</c> + many <c>Add()</c> calls to avoid triggering a
/// separate layout pass for each item.
/// </summary>
public sealed class BulkObservableCollection<T> : ObservableCollection<T>
{
    private bool _suppressNotifications;

    /// <summary>
    /// Replaces the entire contents with <paramref name="newItems"/> and raises a single
    /// <see cref="NotifyCollectionChangedAction.Reset"/> notification.
    /// </summary>
    public void Reset(IEnumerable<T> newItems)
    {
        _suppressNotifications = true;
        try
        {
            Items.Clear();
            foreach (var item in newItems)
                Items.Add(item);
        }
        finally
        {
            _suppressNotifications = false;
        }

        OnPropertyChanged(new PropertyChangedEventArgs(nameof(Count)));
        OnPropertyChanged(new PropertyChangedEventArgs("Item[]"));
        OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
    }

    protected override void OnCollectionChanged(NotifyCollectionChangedEventArgs e)
    {
        if (!_suppressNotifications)
            base.OnCollectionChanged(e);
    }
}
