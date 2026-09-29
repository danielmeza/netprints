using CommunityToolkit.Mvvm.ComponentModel;

namespace NetPrints.Core
{
    /// <summary>
    /// Base class for model types that raise <see cref="System.ComponentModel.INotifyPropertyChanged"/>
    /// notifications through CommunityToolkit.Mvvm's <see cref="ObservableObject"/> (data-model.md §1).
    /// </summary>
    public abstract class ModelObject : ObservableObject;
}
