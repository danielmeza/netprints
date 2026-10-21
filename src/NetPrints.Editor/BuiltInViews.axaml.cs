using Avalonia.Controls.Templates;
using Avalonia.Markup.Xaml;

namespace NetPrints.Editor;

/// <summary>The editor's own view templates, compiled with the assembly so loading them needs no runtime XAML parser.</summary>
public partial class BuiltInViews : DataTemplates
{
    /// <summary>Loads the templates.</summary>
    public BuiltInViews()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
