#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NetPrints.Core;

namespace NetPrints.Translator
{
    /// <summary>
    /// Translates a class into C#, applying the class and member emitters of its
    /// <see cref="TranslationEnvironment"/>.
    /// </summary>
    public sealed class ClassTranslator
    {
        private const string CLASS_TEMPLATE =
            @"%Usings%namespace %Namespace%
            {
                %Attributes%%ClassModifiers%class %ClassName%%GenericArguments%%BaseTypes%
                {
                    %Content%
                }
            }";

        private const string CLASS_TEMPLATE_NO_NAMESPACE =
            @"%Usings%%Attributes%%ClassModifiers%class %ClassName%%GenericArguments%%BaseTypes%
            {
                %Content%
            }";

        private const string VARIABLE_TEMPLATE = "%Attributes%%VariableModifiers%%VariableType% %VariableName%;";

        private const string PROPERTY_TEMPLATE = @"%Attributes%%VariableModifiers%%VariableType% %VariableName%
            {
                %Get%
                %Set%
            }";

        private static readonly HashSet<string> AllowedClassModifiers = new HashSet<string>(StringComparer.Ordinal)
        {
            "partial", "sealed", "abstract", "static", "unsafe",
        };

        private static readonly HashSet<string> AllowedMemberModifiers = new HashSet<string>(StringComparer.Ordinal)
        {
            "abstract", "new", "override", "partial", "readonly", "sealed", "static", "unsafe", "virtual",
        };

        private readonly TranslationEnvironment environment;
        private readonly ExecutionGraphTranslator methodTranslator;

        /// <summary>
        /// Creates a translator that takes node translators and emitters from <paramref name="environment"/>.
        /// </summary>
        /// <param name="environment">Node translators and emitters to use.</param>
        public ClassTranslator(TranslationEnvironment environment)
        {
            this.environment = environment ?? throw new ArgumentNullException(nameof(environment));
            methodTranslator = new ExecutionGraphTranslator(environment);
        }

        /// <summary>
        /// Translates a class into C#.
        /// </summary>
        /// <param name="c">Class to translate.</param>
        /// <returns>C# code for the class.</returns>
        /// <exception cref="TranslationException">
        /// A class or member emitter threw (<c>NPT005</c>) or produced invalid output (<c>NPT007</c>), or a
        /// node has no translator (<c>NPT006</c>).
        /// </exception>
        public string TranslateClass(ClassGraph c)
        {
            ClassEmitContext emitContext = new ClassEmitContext(c, c.AllBaseTypes.Select(t => t.ToString()));

            foreach (IClassEmitter emitter in environment.ClassEmitters)
            {
                RunEmitter(emitter.Id, "class", () => emitter.EmitClass(emitContext), AllowedClassModifiers, emitContext.ExtraModifiers);
            }

            StringBuilder content = new StringBuilder();

            foreach (Variable v in c.Variables)
            {
                content.AppendLine(TranslateVariable(v));
            }

            foreach (ConstructorGraph constructor in c.Constructors)
            {
                content.AppendLine(TranslateConstructor(constructor));
            }

            foreach (MethodGraph m in c.Methods)
            {
                content.AppendLine(TranslateMethod(m));
            }

            List<string> modifiers = new List<string>
            {
                TranslatorUtil.VisibilityTokens[c.Visibility],
            };

            if (c.Modifiers.HasFlag(ClassModifiers.Static))
            {
                modifiers.Add("static");
            }

            if (c.Modifiers.HasFlag(ClassModifiers.Abstract))
            {
                modifiers.Add("abstract");
            }

            if (c.Modifiers.HasFlag(ClassModifiers.Sealed))
            {
                modifiers.Add("sealed");
            }

            if (c.Modifiers.HasFlag(ClassModifiers.Partial))
            {
                modifiers.Add("partial");
            }

            string genericArguments = "";
            if (c.DeclaredGenericArguments.Count > 0)
            {
                genericArguments = "<" + string.Join(", ", c.DeclaredGenericArguments) + ">";
            }

            string baseTypes = emitContext.BaseTypes.Count > 0 ? " : " + string.Join(", ", emitContext.BaseTypes) : "";

            string usings = string.Concat(emitContext.Usings.Select(u => $"using {u};\n"));

            string generatedCode = (string.IsNullOrWhiteSpace(c.Namespace) ? CLASS_TEMPLATE_NO_NAMESPACE : CLASS_TEMPLATE)
                .Replace("%Usings%", usings)
                .Replace("%Namespace%", c.Namespace)
                .Replace("%Attributes%", AttributeLines(emitContext.Attributes))
                .Replace("%ClassModifiers%", ModifierPrefix(modifiers, emitContext.ExtraModifiers))
                .Replace("%ClassName%", c.Name)
                .Replace("%GenericArguments%", genericArguments)
                .Replace("%BaseTypes%", baseTypes)
                .Replace("%Content%", content.ToString());

            return TranslatorUtil.FormatCode(generatedCode);
        }

        /// <summary>
        /// Translates a variable into C#.
        /// </summary>
        /// <param name="variable">Variable to translate.</param>
        /// <returns>C# code for the variable.</returns>
        /// <exception cref="TranslationException">
        /// A member emitter threw (<c>NPT005</c>) or produced invalid output (<c>NPT007</c>).
        /// </exception>
        public string TranslateVariable(Variable variable)
        {
            List<string> modifiers = new List<string>
            {
                TranslatorUtil.VisibilityTokens[variable.Visibility],
            };

            if (variable.Modifiers.HasFlag(VariableModifiers.Static))
            {
                modifiers.Add("static");
            }

            if (variable.Modifiers.HasFlag(VariableModifiers.ReadOnly))
            {
                modifiers.Add("readonly");
            }

            if (variable.Modifiers.HasFlag(VariableModifiers.New))
            {
                modifiers.Add("new");
            }

            if (variable.Modifiers.HasFlag(VariableModifiers.Const))
            {
                modifiers.Add("const");
            }

            MemberEmitContext emitContext = EmitMember(variable.Class,
                variable.HasAccessors ? EmittedMemberKind.Property : EmittedMemberKind.Field, variable.Name, variable);
            string attributes = AttributeLines(emitContext.Attributes);

            if (emitContext.DeclarePartial)
            {
                // A partial property declaration has neither bodies nor a backing field.
                List<string> accessors = new List<string>();

                if (variable.GetterMethod != null)
                {
                    accessors.Add($"{AccessorVisibilityPrefix(variable, variable.GetterMethod)}get;");
                }

                if (variable.SetterMethod != null)
                {
                    accessors.Add($"{AccessorVisibilityPrefix(variable, variable.SetterMethod)}set;");
                }

                return $"{attributes}{ModifierPrefix(modifiers, emitContext.ExtraModifiers, forcePartial: true)}{variable.Type.FullCodeName} {variable.Name} {{ {string.Join(" ", accessors)} }}";
            }

            string modifierText = ModifierPrefix(modifiers, emitContext.ExtraModifiers);

            if (variable.HasAccessors)
            {
                // Translate get / set methods

                string output = PROPERTY_TEMPLATE
                    .Replace("%Attributes%", attributes)
                    .Replace("%VariableModifiers%", modifierText)
                    .Replace("%VariableType%", variable.Type.FullCodeName)
                    .Replace("%VariableName%", variable.Name);

                if (variable.GetterMethod != null)
                {
                    string getterMethodCode = methodTranslator.Translate(variable.GetterMethod, false);
                    string visibilityPrefix = AccessorVisibilityPrefix(variable, variable.GetterMethod);

                    output = output.Replace("%Get%", $"{visibilityPrefix}get\n{getterMethodCode}");
                }
                else
                {
                    output = output.Replace("%Get%", "");
                }

                if (variable.SetterMethod != null)
                {
                    string setterMethodCode = methodTranslator.Translate(variable.SetterMethod, false);
                    string visibilityPrefix = AccessorVisibilityPrefix(variable, variable.SetterMethod);

                    output = output.Replace("%Set%", $"{visibilityPrefix}set\n{setterMethodCode}");
                }
                else
                {
                    output = output.Replace("%Set%", "");
                }

                return output;
            }
            else
            {
                return VARIABLE_TEMPLATE
                    .Replace("%Attributes%", attributes)
                    .Replace("%VariableModifiers%", modifierText)
                    .Replace("%VariableType%", variable.Type.FullCodeName)
                    .Replace("%VariableName%", variable.Name);
            }
        }

        /// <summary>
        /// Translates a method to C#.
        /// </summary>
        /// <param name="m">Method to translate.</param>
        /// <returns>C# code for the method.</returns>
        /// <exception cref="TranslationException">
        /// A member emitter threw (<c>NPT005</c>) or produced invalid output (<c>NPT007</c>), or a node has no
        /// translator (<c>NPT006</c>).
        /// </exception>
        public string TranslateMethod(MethodGraph m)
        {
            return TranslateExecutionGraph(m, EmittedMemberKind.Method, m.Name);
        }

        /// <summary>
        /// Translates a constructor to C#.
        /// </summary>
        /// <param name="m">Constructor to translate.</param>
        /// <returns>C# code for the constructor.</returns>
        /// <exception cref="TranslationException">
        /// A member emitter threw (<c>NPT005</c>) or produced invalid output (<c>NPT007</c>), or a node has no
        /// translator (<c>NPT006</c>).
        /// </exception>
        public string TranslateConstructor(ConstructorGraph m)
        {
            return TranslateExecutionGraph(m, EmittedMemberKind.Constructor, m.ToString());
        }

        private string TranslateExecutionGraph(ExecutionGraph graph, EmittedMemberKind kind, string name)
        {
            if (graph.Class is null)
            {
                return methodTranslator.Translate(graph, true);
            }

            MemberEmitContext emitContext = EmitMember(graph.Class, kind, name, graph);
            string code = methodTranslator.Translate(graph, true, emitContext.ExtraModifiers);
            return AttributeLines(emitContext.Attributes) + code;
        }

        private MemberEmitContext EmitMember(ClassGraph cls, EmittedMemberKind kind, string name, object model)
        {
            MemberEmitContext context = new MemberEmitContext(cls, kind, name, model);

            foreach (IMemberEmitter emitter in environment.MemberEmitters)
            {
                RunEmitter(emitter.Id, "member", () => emitter.EmitMember(context), AllowedMemberModifiers, context.ExtraModifiers);

                if (context.DeclarePartial && kind != EmittedMemberKind.Property)
                {
                    throw new TranslationException("NPT007", $"{emitter.Id}: DeclarePartial is only valid on a property, not on {kind} '{name}'.",
                        (model as NodeGraph) is { } graph ? TranslatorUtil.TryGetGraphKey(graph) : null);
                }
            }

            return context;
        }

        private static void RunEmitter(string id, string target, Action emit, HashSet<string> allowedModifiers, ISet<string> extraModifiers)
        {
            try
            {
                emit();
            }
            catch (Exception ex)
            {
                throw new TranslationException("NPT005", $"{id}: {ex.Message}", inner: ex);
            }

            string? invalid = extraModifiers.FirstOrDefault(modifier => !allowedModifiers.Contains(modifier));
            if (invalid != null)
            {
                throw new TranslationException("NPT007", $"{id}: '{invalid}' is not an allowed {target} modifier.");
            }
        }

        private static string AttributeLines(IEnumerable<string> attributes) =>
            string.Concat(attributes.Select(attribute => $"[{attribute}]\n"));

        /// <summary>
        /// The modifier tokens followed by a space each: <paramref name="modifiers"/> as they are, then the
        /// extra modifiers they do not already contain in ordinal order, with <c>partial</c> last (C# requires
        /// it directly before the declaration keyword).
        /// </summary>
        private static string ModifierPrefix(List<string> modifiers, IEnumerable<string> extraModifiers, bool forcePartial = false)
        {
            List<string> result = modifiers.Where(modifier => modifier != "partial").ToList();
            result.AddRange(extraModifiers.Where(modifier => modifier != "partial" && !result.Contains(modifier)));

            if (forcePartial || modifiers.Contains("partial") || extraModifiers.Contains("partial"))
            {
                result.Add("partial");
            }

            return string.Concat(result.Select(modifier => modifier + " "));
        }

        private static string AccessorVisibilityPrefix(Variable variable, MethodGraph accessor) =>
            accessor.Visibility != variable.Visibility ? $"{TranslatorUtil.VisibilityTokens[accessor.Visibility]} " : "";
    }
}
