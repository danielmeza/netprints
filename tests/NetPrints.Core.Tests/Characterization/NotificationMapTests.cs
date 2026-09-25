using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using NetPrints.Core;
using NetPrints.Graph;
using NetPrints.Tests.Samples;
using Xunit;

namespace NetPrints.Tests.Characterization
{
    /// <summary>
    /// Records which public settable properties of every public, non-abstract, non-generic
    /// <see cref="INotifyPropertyChanged"/> type in <c>NetPrints.Core</c>/<c>NetPrints.Graph</c> raise
    /// change notifications today (Fody's <c>PropertyChanged.Fody</c>), using one instance of each type
    /// taken from the <see cref="AllNodesFixtureFactory"/> fixture. T010 (Fody removal) must reproduce
    /// this map unchanged with CommunityToolkit.Mvvm. Set NETPRINTS_UPDATE_SNAPSHOTS=1 to (re)write it.
    /// </summary>
    public class NotificationMapTests
    {
        public const string UpdateSnapshotsVariable = "NETPRINTS_UPDATE_SNAPSHOTS";
        private const string Throws = "<throws>";

        [Fact]
        public void NotificationsMatchGoldenMap()
        {
            Project project = AllNodesFixtureFactory.CreateAllNodes(Path.Combine(Path.GetTempPath(), "netprints-notification-map.netpp"));

            Dictionary<Type, object> instancesByType = CollectInstancesByType(project);

            Type[] notifyingTypes = typeof(Project).Assembly.GetTypes()
                .Where(t => t.IsPublic && !t.IsAbstract && !t.IsGenericTypeDefinition && typeof(INotifyPropertyChanged).IsAssignableFrom(t))
                .OrderBy(t => t.FullName, StringComparer.Ordinal)
                .ToArray();

            var map = new SortedDictionary<string, SortedDictionary<string, object>>(StringComparer.Ordinal);

            foreach (Type type in notifyingTypes)
            {
                Assert.True(instancesByType.TryGetValue(type, out object instance),
                    $"The AllNodes fixture has no instance of {type.FullName}; extend AllNodesFixtureFactory.");

                var properties = new SortedDictionary<string, object>(StringComparer.Ordinal);

                foreach (PropertyInfo property in SettableProperties(type))
                {
                    object current = property.GetValue(instance);

                    if (!TryMakeDifferentValue(property.PropertyType, current, instancesByType, out object candidate))
                    {
                        continue;
                    }

                    var raised = new List<string>();
                    void Handler(object sender, PropertyChangedEventArgs e) => raised.Add(e.PropertyName);

                    var inpc = (INotifyPropertyChanged)instance;
                    inpc.PropertyChanged += Handler;
                    try
                    {
                        property.SetValue(instance, candidate);
                        properties[property.Name] = raised;
                    }
                    catch (TargetInvocationException)
                    {
                        properties[property.Name] = Throws;
                    }
                    catch (ArgumentException)
                    {
                        properties[property.Name] = Throws;
                    }
                    catch (InvalidOperationException)
                    {
                        properties[property.Name] = Throws;
                    }
                    finally
                    {
                        inpc.PropertyChanged -= Handler;
                    }
                }

                map[type.FullName] = properties;
            }

            string goldenPath = Path.Combine(SampleProjectFactory.FindRepositoryRoot(), "tests", "NetPrints.Core.Tests", "Characterization", "NotificationMap.golden.json");
            var options = new JsonSerializerOptions { WriteIndented = true };
            string actual = JsonSerializer.Serialize(map, options);

            if (Environment.GetEnvironmentVariable(UpdateSnapshotsVariable) == "1")
            {
                File.WriteAllText(goldenPath, actual);
            }

            Assert.True(File.Exists(goldenPath), $"Missing {goldenPath}; regenerate with {UpdateSnapshotsVariable}=1");
            string golden = File.ReadAllText(goldenPath);
            Assert.Equal(golden, actual);
        }

        /// <summary>Public, non-indexer properties with a public getter and a public setter.</summary>
        private static IEnumerable<PropertyInfo> SettableProperties(Type type)
        {
            return type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.CanRead && p.CanWrite && p.GetIndexParameters().Length == 0
                    && p.GetGetMethod(false) != null && p.GetSetMethod(false) != null)
                .OrderBy(p => p.Name, StringComparer.Ordinal);
        }

        /// <summary>
        /// Picks a value different from <paramref name="current"/> that is plausible for
        /// <paramref name="propertyType"/>. Returns false when no safe candidate can be produced (the
        /// property is then left untouched and not recorded). A candidate that a property's own setter
        /// rejects (e.g. an incompatible <c>object</c> value, or <c>IsPure</c> when the node cannot be
        /// made pure) is expected and recorded as "&lt;throws&gt;" by the caller.
        /// </summary>
        private static bool TryMakeDifferentValue(Type propertyType, object current, IReadOnlyDictionary<Type, object> pool, out object candidate)
        {
            if (propertyType == typeof(bool))
            {
                candidate = !(bool)current;
                return true;
            }

            if (propertyType == typeof(string))
            {
                candidate = current is string s ? s + "_x" : "x";
                return true;
            }

            if (propertyType.IsEnum)
            {
                object different = Enum.GetValues(propertyType).Cast<object>().FirstOrDefault(v => !v.Equals(current));
                candidate = different;
                return different != null;
            }

            if (IsNumeric(propertyType))
            {
                candidate = AddOne(propertyType, current);
                return true;
            }

            if (propertyType == typeof(object))
            {
                candidate = current is int i ? (object)(i + 1) : 1;
                return true;
            }

            if (!propertyType.IsValueType)
            {
                if (current != null)
                {
                    candidate = null;
                    return true;
                }

                if (pool.TryGetValue(propertyType, out object alternate) && alternate != null)
                {
                    candidate = alternate;
                    return true;
                }

                if (propertyType.GetConstructor(Type.EmptyTypes) != null)
                {
                    candidate = Activator.CreateInstance(propertyType);
                    return true;
                }
            }

            candidate = null;
            return false;
        }

        private static bool IsNumeric(Type t) =>
            t == typeof(byte) || t == typeof(sbyte) || t == typeof(short) || t == typeof(ushort)
            || t == typeof(int) || t == typeof(uint) || t == typeof(long) || t == typeof(ulong)
            || t == typeof(float) || t == typeof(double) || t == typeof(decimal);

        private static object AddOne(Type t, object current)
        {
            if (t == typeof(double))
            {
                double d = (double)current;
                return double.IsNaN(d) || double.IsInfinity(d) ? 0.0 : d + 1;
            }

            if (t == typeof(float))
            {
                float f = (float)current;
                return float.IsNaN(f) || float.IsInfinity(f) ? 0f : f + 1;
            }

            if (t == typeof(decimal))
            {
                return (decimal)current + 1;
            }

            return Convert.ChangeType(Convert.ToInt64(current) + 1, t);
        }

        /// <summary>Walks the whole fixture, keeping the first instance found of every runtime type.</summary>
        private static Dictionary<Type, object> CollectInstancesByType(Project project)
        {
            var result = new Dictionary<Type, object>();

            void Register(object obj)
            {
                if (obj != null)
                {
                    result.TryAdd(obj.GetType(), obj);
                }
            }

            void RegisterNode(Node node)
            {
                Register(node);
                foreach (var pin in node.InputDataPins)
                    Register(pin);
                foreach (var pin in node.OutputDataPins)
                    Register(pin);
                foreach (var pin in node.InputExecPins)
                    Register(pin);
                foreach (var pin in node.OutputExecPins)
                    Register(pin);
                foreach (var pin in node.InputTypePins)
                    Register(pin);
                foreach (var pin in node.OutputTypePins)
                    Register(pin);
            }

            void RegisterGraph(NodeGraph graph)
            {
                if (graph == null)
                {
                    return;
                }

                foreach (var node in graph.Nodes)
                {
                    RegisterNode(node);
                }
            }

            Register(project);

            foreach (ClassGraph cls in project.Classes)
            {
                RegisterNode(cls.ReturnNode);

                foreach (Variable variable in cls.Variables)
                {
                    Register(variable);
                    RegisterGraph(variable.TypeGraph);
                    RegisterGraph(variable.GetterMethod);
                    RegisterGraph(variable.SetterMethod);
                }

                foreach (MethodGraph method in cls.Methods)
                {
                    RegisterGraph(method);
                }

                foreach (ConstructorGraph constructor in cls.Constructors)
                {
                    RegisterGraph(constructor);
                }
            }

            return result;
        }
    }
}
