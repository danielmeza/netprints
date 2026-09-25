using System.Collections;
using System.Collections.ObjectModel;
using System.Collections.Specialized;

namespace NetPrints.Editor.ViewModels;

/// <summary>
/// An observable collection of view models that mirrors an observable collection of models.
/// </summary>
/// <remarks>
/// Unlike the WPF editor's version, a <see cref="NotifyCollectionChangedAction.Reset"/> of the
/// source (raised by <c>ObservableRangeCollection.AddRange/ReplaceRange/RemoveRange</c>)
/// rebuilds the view models instead of leaving the collection empty.
/// </remarks>
public sealed class ObservableViewModelCollection<TViewModel, TModel> : ObservableCollection<TViewModel>, IDisposable
    where TModel : class
{
    private readonly IList source;
    private readonly INotifyCollectionChanged notifier;
    private readonly Func<TModel, TViewModel> factory;
    private readonly Action<TViewModel>? onRemoved;

    public ObservableViewModelCollection(ObservableCollection<TModel> source, Func<TModel, TViewModel> factory,
        Action<TViewModel>? onRemoved = null)
        : base(source.Select(factory))
    {
        this.source = source;
        notifier = source;
        this.factory = factory;
        this.onRemoved = onRemoved;
        notifier.CollectionChanged += OnSourceCollectionChanged;
    }

    /// <summary>Finds the view model of a model, or default.</summary>
    public TViewModel? Find(TModel model)
    {
        int index = source.IndexOf(model);
        return index >= 0 && index < Count ? this[index] : default;
    }

    private void OnSourceCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        switch (e.Action)
        {
            case NotifyCollectionChangedAction.Add when e.NewItems is not null:
                for (int i = 0; i < e.NewItems.Count; i++)
                {
                    Insert(e.NewStartingIndex + i, factory((TModel)e.NewItems[i]!));
                }
                break;

            case NotifyCollectionChangedAction.Remove when e.OldItems is not null:
                for (int i = 0; i < e.OldItems.Count; i++)
                {
                    var removed = this[e.OldStartingIndex];
                    RemoveAt(e.OldStartingIndex);
                    onRemoved?.Invoke(removed);
                }
                break;

            case NotifyCollectionChangedAction.Move when e.OldItems is not null:
                if (e.OldItems.Count == 1)
                {
                    Move(e.OldStartingIndex, e.NewStartingIndex);
                }
                else
                {
                    Rebuild();
                }
                break;

            case NotifyCollectionChangedAction.Replace when e.OldItems is not null && e.NewItems is not null:
                for (int i = 0; i < e.OldItems.Count; i++)
                {
                    var removed = this[e.OldStartingIndex + i];
                    this[e.OldStartingIndex + i] = factory((TModel)e.NewItems[i]!);
                    onRemoved?.Invoke(removed);
                }
                break;

            default:
                Rebuild();
                break;
        }
    }

    private void Rebuild()
    {
        var old = Items.ToList();
        var models = source.Cast<TModel>().ToList();

        Items.Clear();
        foreach (var model in models)
        {
            Items.Add(factory(model));
        }

        foreach (var vm in old)
        {
            onRemoved?.Invoke(vm);
        }

        OnPropertyChanged(new System.ComponentModel.PropertyChangedEventArgs(nameof(Count)));
        OnPropertyChanged(new System.ComponentModel.PropertyChangedEventArgs("Item[]"));
        OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
    }

    public void Dispose()
    {
        notifier.CollectionChanged -= OnSourceCollectionChanged;
    }
}
