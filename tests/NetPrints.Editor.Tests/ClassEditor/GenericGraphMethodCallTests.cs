using System.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using NetPrints.Core;
using NetPrints.Editor.Graph;
using NetPrints.Editor.Tests.Graph;
using NetPrints.Editor.Tests.Hosting;
using NetPrints.Graph;
using NetPrints.Serialization.Documents;
using NetPrints.Serialization.Json;
using NetPrints.Serialization.Mapping;
using NetPrints.Serialization.Migrations;
using NetPrints.Testing;

namespace NetPrints.Editor.Tests.ClassEditor;

/// <summary>A call dragged from a generic graph method gets its type pins, and the project builds and runs (G-F2 note).</summary>
public sealed class GenericGraphMethodCallTests : GraphTestBase
{
    private readonly MethodGraph caller;

    public GenericGraphMethodCallTests(TestEditor editor)
        : base(editor)
    {
        caller = ClassContext.CreateMethod();
        caller.Name = "Main";
        caller.Modifiers = MethodModifiers.Static;
        caller.Visibility = MemberVisibility.Public;
        Method.Name = "Identity";
        Method.Modifiers = MethodModifiers.Static;
        Method.Visibility = MemberVisibility.Public;
        ClassContext.UndoRedo.Clear();
    }

    private void MakeGeneric(bool parameter, bool returnValue)
    {
        VmOf(Method.MethodEntryNode).RightPinsPlusCommand.Execute(null);
        NodeOutputTypePin generic = Method.MethodEntryNode.OutputTypePins[0];

        if (parameter)
        {
            Method.MethodEntryNode.AddArgument();
            GraphUtil.ConnectTypePins(generic, Method.MethodEntryNode.InputTypePins[0]);
        }

        if (returnValue)
        {
            Method.MainReturnNode.AddReturnType();
            GraphUtil.ConnectTypePins(generic, Method.MainReturnNode.InputTypePins[0]);
            if (parameter)
            {
                GraphUtil.ConnectDataPins(Method.MethodEntryNode.OutputDataPins[0], Method.MainReturnNode.InputDataPins[0]);
            }
        }

        GraphUtil.ConnectExecPins(Method.MethodEntryNode.InitialExecutionPin, Method.MainReturnNode.ReturnPin);
    }

    private CallMethodNode DropTheMethod() =>
        Assert.IsType<CallMethodNode>(new NodeGraphViewModel(caller, ClassContext.Services)
            .Drop(ClassContext.Methods.Single(m => m.Graph == Method), new GraphPoint(0, 0)));

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public void ACallDroppedFromAGenericGraphMethodHasItsTypePins(bool parameter, bool returnValue)
    {
        MakeGeneric(parameter, returnValue);

        CallMethodNode call = DropTheMethod();

        Assert.Single(call.InputTypePins);
        Assert.Equal(parameter ? 1 : 0, call.ArgumentPins.Count);
        Assert.Equal(returnValue ? 1 : 0, call.ReturnValuePins.Count);
        Assert.Equal(Method.GenericArgumentTypes.Count(), call.MethodSpecifier.GenericArguments.Count);
    }

    [Fact(Timeout = 600_000)]
    public async Task AProjectCallingAGenericGraphMethodDroppedFromTheListBuildsAndRuns()
    {
        MakeGeneric(parameter: true, returnValue: true);
        CallMethodNode call = DropTheMethod();
        var write = new CallMethodNode(caller, ConsoleWriteLine(TypeSpecifier.FromType<object>()));
        var intType = new TypeNode(caller, IntType);
        GraphUtil.ConnectTypePins(intType.OutputTypePins[0], call.InputTypePins[0]);
        GraphUtil.ConnectDataPins(LiteralNode.WithValue(caller, 42).ValuePin, call.ArgumentPins[0]);
        GraphUtil.ConnectDataPins(call.ReturnValuePins[0], write.ArgumentPins[0]);
        GraphUtil.ConnectExecPins(caller.EntryNode.InitialExecutionPin, call.InputExecPins[0]);
        GraphUtil.ConnectExecPins(call.OutputExecPins[0], write.InputExecPins[0]);
        GraphUtil.ConnectExecPins(write.OutputExecPins[0], caller.MainReturnNode.ReturnPin);

        string directory = TestPaths.CreateTempDirectory();
        try
        {
            var registry = new NodeDocumentConverterRegistry(NodeDocumentConverterRegistry.BuiltIn, []);
            var mapper = new DocumentMapper(registry, NullLogger<DocumentMapper>.Instance);
            var format = new JsonDocumentFormat(new NetPrintsJsonOptions(registry), new DocumentMigrator([], NullLogger<DocumentMigrator>.Instance));
            using (var buffer = new MemoryStream())
            {
                await format.WriteClassAsync(mapper.ToDocument(Class), buffer, TestContext.Current.CancellationToken);
                await File.WriteAllBytesAsync(Path.Combine(directory, "C.netpc.json"), buffer.ToArray(), TestContext.Current.CancellationToken);
            }

            string csproj = Path.Combine(directory, "Generic.csproj");
            await File.WriteAllTextAsync(csproj, """
                <Project Sdk="Microsoft.NET.Sdk">

                  <PropertyGroup>
                    <OutputType>Exe</OutputType>
                    <TargetFramework>net10.0</TargetFramework>
                    <RootNamespace>N</RootNamespace>
                    <NetPrintsProfile>netprints.default</NetPrintsProfile>
                  </PropertyGroup>

                  <ItemGroup>
                    <PackageReference Include="NetPrints.Sdk" Version="1.0.0" Condition="'$(NetPrintsUseLocalSdk)' != 'true'" PrivateAssets="all" />
                  </ItemGroup>

                </Project>

                """, TestContext.Current.CancellationToken);
            LocalSdkLayout.Write(directory);

            (int buildExit, string buildOutput) = await RunDotnetAsync(directory, "build", csproj, "-nodeReuse:false", "-v:n", "-tl:off", "--nologo");
            Assert.True(buildExit == 0, buildOutput);

            (int runExit, string runOutput) = await RunDotnetAsync(directory, "run", "--project", csproj, "--no-build");
            Assert.True(runExit == 0, runOutput);
            Assert.Equal("42", runOutput.Trim());
        }
        finally
        {
            TestPaths.TryDelete(directory);
        }
    }

    private static async Task<(int ExitCode, string Output)> RunDotnetAsync(string workingDirectory, params string[] args)
    {
        var startInfo = new ProcessStartInfo("dotnet")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            WorkingDirectory = workingDirectory,
        };
        foreach (string arg in args)
        {
            startInfo.ArgumentList.Add(arg);
        }

        using Process process = Process.Start(startInfo) ?? throw new InvalidOperationException("dotnet did not start.");
        Task<string> output = process.StandardOutput.ReadToEndAsync(TestContext.Current.CancellationToken);
        Task<string> errors = process.StandardError.ReadToEndAsync(TestContext.Current.CancellationToken);
        await Task.WhenAll(output, errors, process.WaitForExitAsync(TestContext.Current.CancellationToken));
        return (process.ExitCode, await output + await errors);
    }
}
