using System.Runtime.Serialization;
using CommunityToolkit.Mvvm.ComponentModel;

namespace NetPrints.Core
{
    /// <summary>
    /// Base class for model types that raise <see cref="System.ComponentModel.INotifyPropertyChanged"/>
    /// notifications through CommunityToolkit.Mvvm's source generator instead of IL weaving
    /// (data-model.md §1). <c>[INotifyPropertyChanged]</c> (source-generated) is used instead of
    /// inheriting <c>ObservableObject</c> so DataContract deserialization, which never runs
    /// constructors, keeps working unchanged; MVVMTK0032 (which suggests <c>ObservableObject</c>
    /// instead) is suppressed here only.
    /// </summary>
    [DataContract]
    [INotifyPropertyChanged]
#pragma warning disable MVVMTK0032
    public abstract partial class ModelObject
    {
    }
#pragma warning restore MVVMTK0032
}
