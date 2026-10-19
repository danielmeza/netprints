using System.Reflection;
using System.Runtime.Loader;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using NetPrints.Editor.Hosting;
using NetPrints.Editor.UITests.Driving;
using NetPrints.Testing;
using NetPrints.Testing.Ui.Driving;

namespace NetPrints.Editor.UITests.Hosting;

/// <summary>R3-1: a layout that an extension's view triggers itself bypasses the holder, so the error dialog names the extension.</summary>
public sealed class ExtensionErrorAttributionTests
{
    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public async Task AViewThatThrowsOnALaterMeasureReachesTheErrorDialogNamingTheExtension()
    {
        string folder = FixtureExtensions.ViewsFolder();
        await using var app = HeadlessApp.Start([folder]);
        Assembly assembly = AssemblyLoadContext.All
            .SelectMany(context => context.Assemblies)
            .First(candidate => !candidate.IsDynamic && string.Equals(candidate.Location, Path.Combine(folder, "Fx.Views.dll"), StringComparison.Ordinal));
        Type viewType = assembly.GetType("Fx.Views.LateThrowingView") ?? throw new InvalidOperationException("LateThrowingView is missing.");
        var view = (Control)(Activator.CreateInstance(viewType) ?? throw new InvalidOperationException("The view could not be created."));
        var window = new Window { Width = 200, Height = 100, Content = new ContainedView(view, _ => new Border()) };
        try
        {
            window.Show();
            window.UpdateLayout();
            Assert.Empty(app.Dialogs.Errors);

            viewType.GetMethod("Trip")?.Invoke(view, null);
            Dispatcher.UIThread.Post(window.UpdateLayout);
            await UiWait.UntilAsync(app.Driver, () => Task.FromResult(app.Dialogs.Errors.Count == 1), "error reported", TestContext.Current.CancellationToken);

            string message = app.Dialogs.Errors[0].Message;
            Assert.Contains("NetPrints view resources fixture extension", message, StringComparison.Ordinal);
            Assert.Contains("fx.views", message, StringComparison.Ordinal);
            Assert.Contains("late measure", message, StringComparison.Ordinal);
        }
        finally
        {
            window.Close();
            HeadlessDriver.DrainFinalizers();
        }
    }
}
