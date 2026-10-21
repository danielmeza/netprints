using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Loader;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using NetPrints.Editor.Hosting;
using NetPrints.Editor.UITests.Driving;
using NetPrints.Extensibility.Settings;
using NetPrints.Testing;
using NetPrints.Testing.Ui.Driving;

namespace NetPrints.Editor.UITests.Hosting;

/// <summary>R3-1: a layout that an extension's view triggers itself bypasses the holder, so the error dialog names the extension.</summary>
public sealed class ExtensionErrorAttributionTests
{
    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public async Task AViewThatThrowsOnALaterMeasureReachesTheErrorDialogNamingTheExtension()
    {
        string folder = BuildLateThrowingExtension();
        try
        {
            await using var app = HeadlessApp.Start([folder]);
            Assembly assembly = AssemblyLoadContext.All
                .SelectMany(context => context.Assemblies)
                .First(candidate => !candidate.IsDynamic && string.Equals(candidate.Location, Path.Combine(folder, "Fx.Late.dll"), StringComparison.Ordinal));
            Type viewType = assembly.GetType("Fx.Late.LateThrowingView") ?? throw new InvalidOperationException("LateThrowingView is missing.");
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
                Assert.Contains(folder, message, StringComparison.Ordinal);
            }
            finally
            {
                window.Close();
                HeadlessDriver.DrainFinalizers();
            }
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    // The rule counts every load context in the process that holds a frame's assembly identity, and another UI test
    // leaves a copy of the fx.views fixture loaded. An assembly name of its own keeps this test out of that copy's way.
    private static string BuildLateThrowingExtension()
    {
        string folder = Path.Combine(Path.GetTempPath(), "netprints-ui-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        var compilation = CSharpCompilation.Create(
            "Fx.Late",
            [CSharpSyntaxTree.ParseText("""
                [assembly: System.Reflection.AssemblyVersion("9.9.9.9")]

                namespace Fx.Late
                {
                    public sealed class LateThrowingView : Avalonia.Controls.Control
                    {
                        private bool tripped;

                        public void Trip()
                        {
                            tripped = true;
                            InvalidateMeasure();
                        }

                        protected override Avalonia.Size MeasureOverride(Avalonia.Size availableSize) =>
                            tripped ? throw new System.InvalidOperationException("late measure") : new Avalonia.Size(10, 10);
                    }

                    public sealed class LateExtension : NetPrints.Extensibility.INetPrintsExtension
                    {
                        public void Register(NetPrints.Extensibility.IExtensionBuilder builder)
                        {
                            System.ArgumentNullException.ThrowIfNull(builder);
                        }
                    }
                }
                """)],
            [.. Directory.EnumerateFiles(AppContext.BaseDirectory, "*.dll")
                .Concat(Directory.EnumerateFiles(RuntimeEnvironment.GetRuntimeDirectory(), "*.dll"))
                .Append(typeof(object).Assembly.Location)
                .Distinct(StringComparer.Ordinal)
                .Select(path => MetadataReference.CreateFromFile(path))],
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        using (var stream = File.Create(Path.Combine(folder, "Fx.Late.dll")))
        {
            Assert.True(compilation.Emit(stream).Success, string.Join(Environment.NewLine, compilation.GetDiagnostics().Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)));
        }

        File.WriteAllText(Path.Combine(folder, "netprints-extension.json"), """
            { "id": "fx.views", "name": "NetPrints view resources fixture extension", "version": "1.0.0", "assembly": "Fx.Late.dll", "netprintsApi": "0.4", "dependsOn": [] }
            """);
        return folder;
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public async Task TheHandlerReadsTheOpenProjectsExtensionFolders()
    {
        using var sample = new SampleCopy();
        string extensionFolder = Path.Combine(sample.Directory, "ext");
        CopyExtension(FixtureExtensions.TestExtensionFolder(), extensionFolder);
        string project = sample.ProjectPath;
        File.WriteAllText(project, File.ReadAllText(project).Replace("</Project>", """
              <ItemGroup>
                <NetPrintsExtension Include="ext/" />
              </ItemGroup>
            </Project>
            """, StringComparison.Ordinal));
        await using HeadlessApp app = HeadlessApp.Start();
        await app.Settings.SetAsync(NetPrintsSettings.Descriptor, NetPrintsSettings.Empty with { TrustedProjects = [project] }, TestContext.Current.CancellationToken);

        await app.OpenStartupProjectAsync(project, TestContext.Current.CancellationToken);

        IReadOnlyList<ExtensionOrigin> origins = app.Attribution?.Origins() ?? [];
        Assert.True(origins.Single(origin => origin.Id == "netprints.test").ProjectCopy);
    }

    private static void CopyExtension(string source, string destination)
    {
        Directory.CreateDirectory(destination);
        foreach (string file in Directory.GetFiles(source))
        {
            File.Copy(file, Path.Combine(destination, Path.GetFileName(file)));
        }
    }
}
