#nullable enable
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using NetPrints.Compilation;
using NetPrints.Core;
using NetPrints.Serialization;
using NetPrints.Translator;

namespace NetPrints.Core
{
    [Flags]
    public enum ProjectCompilationOutput
    {
        Nothing = 0,
        SourceCode = 1,
        Binaries = 2,
        Errors = 4,
        All = SourceCode | Binaries | Errors,
    }

    public enum BinaryType
    {
        SharedLibrary,
        Executable,
    }

    /// <summary>
    /// Project model.
    /// </summary>
    [DataContract]
    public partial class Project : ModelObject
    {
        private static readonly IEnumerable<FrameworkAssemblyReference> DefaultReferences = new FrameworkAssemblyReference[]
        {
            new FrameworkAssemblyReference(".NETFramework/v4.5/System.dll"),
            new FrameworkAssemblyReference(".NETFramework/v4.5/System.Core.dll"),
            new FrameworkAssemblyReference(".NETFramework/v4.5/mscorlib.dll"),
        };

        private static readonly DataContractSerializer ProjectSerializer = new DataContractSerializer(typeof(Project));

        /// <summary>
        /// Classes contained in this project.
        /// </summary>
        public ObservableRangeCollection<ClassGraph> Classes
        {
            get;
            private set;
        } = new ObservableRangeCollection<ClassGraph>();

        /// <summary>
        /// Name of the project.
        /// </summary>
        [ObservableProperty]
        [DataMember]
        public partial string Name { get; set; }

        /// <summary>
        /// Version of the editor that the project was saved in.
        /// </summary>
        [ObservableProperty]
        [DataMember]
        public partial Version SaveVersion { get; set; }

        /// <summary>
        /// Path to the last successfully compiled assembly. Null before the first successful
        /// compilation, or when the last compilation failed.
        /// </summary>
        [ObservableProperty]
        [DataMember]
        public partial string? LastCompiledAssemblyPath { get; set; }

        /// <summary>
        /// Path to the project file.
        /// </summary>
        [ObservableProperty]
        public partial string Path { get; set; }

        /// <summary>
        /// Default namespace of newly created classes.
        /// </summary>
        [ObservableProperty]
        [DataMember]
        public partial string DefaultNamespace { get; set; }

        /// <summary>
        /// Paths to files for the class models within this project.
        /// </summary>
        [ObservableProperty]
        [DataMember]
        public partial ObservableRangeCollection<string> ClassPaths { get; set; } = new ObservableRangeCollection<string>();

        /// <summary>
        /// References of this project.
        /// </summary>
        [ObservableProperty]
        [DataMember]
        public partial ObservableRangeCollection<CompilationReference> References { get; set; } = new ObservableRangeCollection<CompilationReference>();

        /// <summary>
        /// Determines what gets output during compilation.
        /// </summary>
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(CanCompileAndRun))]
        [DataMember]
        public partial ProjectCompilationOutput CompilationOutput { get; set; }

        /// <summary>
        /// Type of the binary that we want to output.
        /// </summary>
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(CanCompileAndRun))]
        [DataMember]
        public partial BinaryType OutputBinaryType { get; set; }

        private Project()
        {
        }

        public string GetClassStoragePath(ClassGraph cls)
        {
            return $"{cls.FullName}.netpc";
        }

        /// <summary>
        /// Directory of a project file. Throws if <paramref name="projectPath"/> has no directory
        /// (a root or relative path) instead of returning a value from a null-directory silently.
        /// </summary>
        private static string GetProjectDirectory(string projectPath) =>
            System.IO.Path.GetDirectoryName(projectPath)
                ?? throw new InvalidOperationException($"Project path '{projectPath}' has no directory.");

        /// <summary>
        /// Saves the project to its path.
        /// </summary>
        public void Save()
        {
            // Save all classes
            foreach (ClassGraph cls in Classes)
            {
                SaveClassInProjectDirectory(cls);
            }

            // Set class paths from class storage paths
            ClassPaths = new ObservableRangeCollection<string>(Classes.Select(c => GetClassStoragePath(c)));

            SaveVersion = Assembly.GetExecutingAssembly().GetName().Version ?? new Version(0, 0);

            using FileStream fileStream = File.Open(Path, FileMode.Create);
            ProjectSerializer.WriteObject(fileStream, this);
        }

        /// <summary>
        /// Creates a new project.
        /// </summary>
        /// <param name="name">Name of the project.</param>
        /// <param name="defaultNamespace">Default namespace of the project.</param>
        /// <param name="addDefaultReferences">Whether to add default references to the project.</param>
        /// <returns>The created project.</returns>
        public static Project CreateNew(string name, string defaultNamespace, bool addDefaultReferences = true,
            ProjectCompilationOutput compilationOutput = ProjectCompilationOutput.All)
        {
            Project project = new Project()
            {
                Name = name,
                DefaultNamespace = defaultNamespace,
                CompilationOutput = compilationOutput
            };

            if (addDefaultReferences)
            {
                project.References.AddRange(DefaultReferences);
            }

            return project;
        }

        /// <summary>
        /// Loads a project from a path.
        /// </summary>
        /// <param name="path">Path to the project file.</param>
        /// <returns>Loaded project or null if unsuccessful</returns>
        public static Project? LoadFromPath(string path)
        {
            using FileStream fileStream = File.OpenRead(path);

            if (ProjectSerializer.ReadObject(fileStream) is Project project)
            {
                project.Path = path;

                // Load classes
                ConcurrentBag<ClassGraph> classes = new ConcurrentBag<ClassGraph>();

                Parallel.ForEach(project.ClassPaths, classPath =>
                {
                    ClassGraph cls = SerializationHelper.LoadClass(System.IO.Path.Combine(GetProjectDirectory(project.Path), classPath));
                    cls.Project = project;
                    classes.Add(cls);
                });

                project.Classes.ReplaceRange(classes.OrderBy(c => c.Name));

                return project;
            }

            return null;
        }

        public bool CanCompileAndRun
        {
            get => CanCompile && OutputBinaryType == BinaryType.Executable
                && CompilationOutput.HasFlag(ProjectCompilationOutput.Binaries);
        }

        public bool CanCompile
        {
            get => !IsCompiling;
        }

        [ObservableProperty]
        public partial string CompilationMessage { get; set; } = "Ready";

        [ObservableProperty]
        public partial bool IsCompiling { get; set; }

        [ObservableProperty]
        public partial bool LastCompilationSucceeded { get; set; }

        [ObservableProperty]
        public partial ObservableRangeCollection<string> LastCompileErrors { get; set; }

        public async void CompileProject()
        {
            // Check if we are already compiling
            if (!CanCompile || CompilationOutput == ProjectCompilationOutput.Nothing)
            {
                return;
            }

            IsCompiling = true;
            CompilationMessage = "Compiling...";

            var references = References.ToArray();

            // Compile in another thread
            var compileTask = Task.Run(() =>
            {
                string projectDir = GetProjectDirectory(Path);
                string compiledDir = System.IO.Path.Combine(projectDir, $"Compiled_{Name}");

                DirectoryInfo compiledDirInfo = new DirectoryInfo(compiledDir);
                if (compiledDirInfo.Exists)
                {
                    // Delete existing compiled output
                    foreach (FileInfo file in compiledDirInfo.EnumerateFiles())
                    {
                        file.Delete();
                    }

                    foreach (DirectoryInfo dir in compiledDirInfo.EnumerateDirectories())
                    {
                        dir.Delete(true);
                    }
                }
                else
                {
                    Directory.CreateDirectory(compiledDir);
                }

                var translatedClasses = new ConcurrentBag<(string FullName, string Code)>();
                var translationErrors = new ConcurrentBag<string>();

                // Translate classes in parallel
                Parallel.ForEach(Classes, cls =>
                {
                    // Translate the class to C#
                    ClassTranslator classTranslator = new ClassTranslator();

                    string code;
                    try
                    {
                        code = classTranslator.TranslateClass(cls);
                    }
                    catch (Exception ex)
                    {
                        // Report the reason instead of compiling the exception text.
                        translationErrors.Add($"{cls.FullName}: {ex.Message}");
                        code = $"// {cls.FullName} could not be translated: {ex.Message}";
                    }

                    string[] directories = cls.FullName.Split('.');
                    directories = directories
                        .Take(directories.Length - 1)
                        .Prepend(compiledDir)
                        .ToArray();

                    // Write source to file
                    string outputDirectory = System.IO.Path.Combine(directories);

                    System.IO.Directory.CreateDirectory(outputDirectory);

                    if (CompilationOutput.HasFlag(ProjectCompilationOutput.SourceCode))
                    {
                        File.WriteAllText(System.IO.Path.Combine(outputDirectory, $"{cls.Name}.cs"), code);
                    }

                    translatedClasses.Add((cls.FullName, code));
                });

                if (!translationErrors.IsEmpty)
                {
                    return new CodeCompileResults(false, translationErrors.OrderBy(e => e, StringComparer.Ordinal).ToArray(), null);
                }

                // Deterministic output (constitution VI): the compiler sees the sources in a stable order.
                var classSources = translatedClasses
                    .OrderBy(c => c.FullName, StringComparer.Ordinal)
                    .Select(c => c.Code);

                bool generateExecutable = OutputBinaryType == BinaryType.Executable;
                string ext = generateExecutable ? "exe" : "dll";

                string outputPath = System.IO.Path.Combine(compiledDir, $"{Name}.{ext}");

                // Create compiler on other app domain, compile, unload the app domain

                var codeCompiler = new Compilation.CodeCompiler();

                bool deleteBinaries = !CompilationOutput.HasFlag(ProjectCompilationOutput.Binaries) && !File.Exists(outputPath);

                // Missing framework reference assemblies fall back to the runtime's (FR-008, FR-009).
                var resolver = new ReferenceAssemblyResolver();
                var referenceWarnings = new List<string>();
                var assemblyPaths = resolver.ResolveAssemblyPaths(references.OfType<AssemblyReference>(), referenceWarnings);

                var sources = classSources
                    .Concat(references
                        .OfType<SourceDirectoryReference>()
                        .Where(sourceRef => sourceRef.IncludeInCompilation)
                        .SelectMany(sourceRef => sourceRef.SourceFilePaths.OrderBy(p => p, StringComparer.Ordinal))
                        .Select(sourcePath => File.ReadAllText(sourcePath)))
                    .Distinct()
                    .ToArray();

                CodeCompileResults compilationResults = codeCompiler.CompileSources(
                    outputPath, assemblyPaths, sources, generateExecutable);

                if (referenceWarnings.Count > 0)
                {
                    compilationResults = new CodeCompileResults(compilationResults.Success,
                        referenceWarnings.Concat(compilationResults.Errors).ToArray(),
                        compilationResults.PathToAssembly);
                }

                // Started through the dotnet host, which needs a runtime config (FR-010).
                if (compilationResults.Success && generateExecutable && resolver.UsesRuntimeAssemblies
                    && CompilationOutput.HasFlag(ProjectCompilationOutput.Binaries))
                {
                    File.WriteAllText(GetRuntimeConfigPath(compiledDir), CreateRuntimeConfigJson());
                }

                // Delete the output binary if we don't want it.
                // TODO: Don't generate it in the first place.
                if (compilationResults.PathToAssembly != null && deleteBinaries)
                {
                    if (File.Exists(compilationResults.PathToAssembly))
                    {
                        File.Delete(compilationResults.PathToAssembly);
                    }
                }

                // Write errors to file
                if (CompilationOutput.HasFlag(ProjectCompilationOutput.Errors))
                {
                    File.WriteAllText(System.IO.Path.Combine(compiledDir, $"{Name}_errors.txt"),
                        string.Join(Environment.NewLine, compilationResults.Errors));
                }

                return compilationResults;
            });

            CodeCompileResults results;
            try
            {
                results = await compileTask;
            }
            catch (Exception ex)
            {
                // Report unexpected failures as a failed build instead of crashing the host.
                results = new CodeCompileResults(false, new[] { ex.ToString() }, null);
            }

            LastCompilationSucceeded = results.Success;
            LastCompileErrors = new ObservableRangeCollection<string>(results.Errors);

            if (LastCompilationSucceeded)
            {
                LastCompiledAssemblyPath = results.PathToAssembly;
                CompilationMessage = "Build succeeded";
            }
            else
            {
                CompilationMessage = $"Build failed with {LastCompileErrors.Count} error(s)";
            }

            IsCompiling = false;
        }

        /// <summary>
        /// Translates every class to C#, for the reflection host. A class that fails to translate
        /// (e.g. an unconnected node) is skipped instead of compiling its exception text as source;
        /// the reason is reported through <paramref name="warnings"/> instead.
        /// </summary>
        public IEnumerable<string> GenerateClassSources(out IReadOnlyList<string> warnings)
        {
            if (Classes is null)
            {
                warnings = Array.Empty<string>();
                return new string[0];
            }

            ConcurrentBag<string> classSources = new ConcurrentBag<string>();
            ConcurrentBag<string> translationWarnings = new ConcurrentBag<string>();

            // Translate classes in parallel
            Parallel.ForEach(Classes, cls =>
            {
                // Translate the class to C#
                ClassTranslator classTranslator = new ClassTranslator();

                try
                {
                    classSources.Add(classTranslator.TranslateClass(cls));
                }
                catch (Exception ex)
                {
                    translationWarnings.Add($"{cls.FullName}: {ex.Message}");
                }
            });

            warnings = translationWarnings.OrderBy(w => w, StringComparer.Ordinal).ToArray();

            return classSources;
        }

        /// <summary>
        /// Gets the command that runs the compiled executable. Executables compiled against the
        /// running .NET runtime's assemblies (see <see cref="ReferenceAssemblyResolver"/>) have a
        /// runtime configuration file and are started through the <c>dotnet</c> host.
        /// </summary>
        /// <returns>File name and arguments of the process to start.</returns>
        public (string FileName, string Arguments) GetRunCommand()
        {
            if (OutputBinaryType != BinaryType.Executable || !CompilationOutput.HasFlag(ProjectCompilationOutput.Binaries))
            {
                throw new InvalidOperationException("Can only run executable projects which output their binaries.");
            }

            string compiledDir = GetCompiledDirectory();
            string exePath = System.IO.Path.GetFullPath(System.IO.Path.Combine(compiledDir, $"{Name}.exe"));

            if (!File.Exists(exePath))
            {
                throw new Exception($"The executable does not exist at {exePath}");
            }

            if (File.Exists(GetRuntimeConfigPath(compiledDir)))
            {
                return (ReferenceAssemblyResolver.GetDotNetHostPath(), $"\"{exePath}\"");
            }

            return (exePath, "");
        }

        public void RunProject()
        {
            var (fileName, arguments) = GetRunCommand();
            Process.Start(new ProcessStartInfo(fileName, arguments) { UseShellExecute = false });
        }

        private string GetCompiledDirectory() =>
            System.IO.Path.Combine(GetProjectDirectory(Path), $"Compiled_{Name}");

        private string GetRuntimeConfigPath(string compiledDir) =>
            System.IO.Path.Combine(compiledDir, $"{Name}.runtimeconfig.json");

        private static string CreateRuntimeConfigJson()
        {
            Version version = Environment.Version;
            return "{\n" +
                "  \"runtimeOptions\": {\n" +
                $"    \"tfm\": \"net{version.Major}.{version.Minor}\",\n" +
                "    \"framework\": {\n" +
                "      \"name\": \"Microsoft.NETCore.App\",\n" +
                $"      \"version\": \"{version.Major}.{version.Minor}.0\"\n" +
                "    }\n" +
                "  }\n" +
                "}\n";
        }

        private void FixReferencePaths()
        {
            var referencesToRemove = new List<CompilationReference>();

            // Fix references
            foreach (var reference in References)
            {
                if (reference is AssemblyReference assemblyReference)
                {
                    // Check if the assembly exists at the path and
                    // give the user a chance to select another one.
                    if (!File.Exists(assemblyReference.AssemblyPath))
                    {
                        throw new NotImplementedException();

                        // TODO: Fix
                        /*var openFileDialog = new OpenFileDialog()
                        {
                            Title = $"Open replacement for {assemblyReference}",
                            CheckFileExists = true,
                        };

                        if (openFileDialog.ShowDialog() == true)
                        {
                            assemblyReference.AssemblyPath = openFileDialog.FileName;
                        }
                        else
                        {
                            referencesToRemove.Add(reference);
                        }*/
                    }
                }
            }

            // Remove references which couldn't be fixed
            if (referencesToRemove.Count > 0)
            {
                References.RemoveRange(referencesToRemove);

                // TODO
                /*MessageBox.Show("The following assemblies could not be found and have been removed from the project:\n\n" +
                    string.Join(Environment.NewLine, referencesToRemove.Select(n => n.ToString())),
                    "Could not load some assemblies", MessageBoxButton.OK, MessageBoxImage.Warning);*/
            }
        }

        #region Create / Load / Save Project
        /// <summary>
        /// Saves the given class in the project directory.
        /// </summary>
        /// <param name="cls">Class to save.</param>
        public void SaveClassInProjectDirectory(ClassGraph cls)
        {
            string outputPath = System.IO.Path.Combine(GetProjectDirectory(Path), GetClassStoragePath(cls));

            // Save in same directory as project
            SerializationHelper.SaveClass(cls, outputPath);
        }

        #endregion

        #region Creating and loading classes
        public ClassGraph CreateNewClass()
        {
            // Make a class name that isn't already a file and isn't
            // already a class in the project.

            IList<string> existingFiles = System.IO.Directory.GetFiles(GetProjectDirectory(Path))
                .Select(f => System.IO.Path.GetFileNameWithoutExtension(f))
                .Concat(Classes.Select(c => System.IO.Path.GetFileNameWithoutExtension(GetClassStoragePath(c))))
                .ToList();

            string storageName = $"{DefaultNamespace}.MyClass";
            storageName = NetPrintsUtil.GetUniqueName(storageName, existingFiles);

            // TODO: Might break if GetUniqueName adds a dot
            // (which it doesn't at the time of writing, it just adds
            // numbers, but this is not guaranteed forever).
            string name = storageName.Split('.').Last();

            ClassGraph cls = new ClassGraph()
            {
                Name = name,
                Namespace = DefaultNamespace,
                Project = this,
            };

            // TODO: SaveClassInProjectDirectory(clsVM);
            Classes.Add(cls);

            return cls;
        }

        public ClassGraph AddExistingClass(string path)
        {
            // Check if a class with the same storage name is already loaded
            string fileName = System.IO.Path.GetFileName(path);
            ClassGraph? cls = Classes.FirstOrDefault(c => string.Equals(GetClassStoragePath(c), fileName, StringComparison.OrdinalIgnoreCase));

            bool loadAndSave = false;

            if (cls != null)
            {
                // Ask if we should overwrite if it already exists
                // TODO: Probably want to move this into a view instead of here in
                // the viewmodel.
                /*MessageBoxResult result = MessageBox.Show($"File with name {fileName} already exists in this project. Overwrite it?", "File already exists",
                    MessageBoxButton.YesNo, MessageBoxImage.Question);

                // Overwrite the class if chosen
                if (result == MessageBoxResult.Yes)
                {
                    // Remove the old class and load the new one
                    Project.Classes.Remove(cls);
                    loadAndSave = true;
                }*/
            }
            else
            {
                // Load the new class
                loadAndSave = true;
            }

            if (loadAndSave)
            {
                // Load the class and save it relative to the project
                cls = SerializationHelper.LoadClass(path);
                cls.Project = this;
                SaveClassInProjectDirectory(cls);
                Classes.Add(cls);
            }

            // cls is set here: either found above, or just loaded and assigned in the loadAndSave branch.
            return cls ?? throw new InvalidOperationException($"AddExistingClass: no class was loaded or found for '{path}'.");
        }
        #endregion

        [OnDeserialized]
        private void FixDefaults(StreamingContext context)
        {
            Classes = new ObservableRangeCollection<ClassGraph>();
        }
    }
}
