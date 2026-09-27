#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using NetPrints.Core;
using NetPrints.Graph;

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

        /// <summary>Kind of a <see cref="SyntaxAnnotation"/> tagging the token where a node's own
        /// statements start (research.md R3): <see cref="SyntaxAnnotation.Data"/> is the node's id.</summary>
        private const string NodeIdAnnotationKind = "NetPrints.NodeId";

        /// <summary>Kind of a <see cref="SyntaxAnnotation"/> paired with <see cref="NodeIdAnnotationKind"/>
        /// on the same token: <see cref="SyntaxAnnotation.Data"/> is the index, in translation order, of
        /// the member (method, constructor, event or accessor) the node belongs to.</summary>
        private const string MemberAnnotationKind = "NetPrints.Member";

        /// <summary>Kind of a <see cref="SyntaxAnnotation"/> tagging the token right after a member's own
        /// generated code, bounding that member's last node's mapped span: <see cref="SyntaxAnnotation.Data"/>
        /// is the member's index, matching <see cref="MemberAnnotationKind"/>.</summary>
        private const string MemberEndAnnotationKind = "NetPrints.MemberEnd";

        private const string PROPERTY_TEMPLATE = @"%Attributes%%VariableModifiers%%VariableType% %VariableName%
            {
                %Get%
                %Set%
            }";

        private static readonly HashSet<string> AllowedClassModifiers = new HashSet<string>(StringComparer.Ordinal)
        {
            CSharpKeywords.Partial, CSharpKeywords.Sealed, CSharpKeywords.Abstract, CSharpKeywords.Static, CSharpKeywords.Unsafe,
        };

        private static readonly HashSet<string> AllowedMemberModifiers = new HashSet<string>(StringComparer.Ordinal)
        {
            CSharpKeywords.Abstract, CSharpKeywords.New, CSharpKeywords.Override, CSharpKeywords.Partial,
            CSharpKeywords.ReadOnly, CSharpKeywords.Sealed, CSharpKeywords.Static, CSharpKeywords.Unsafe, CSharpKeywords.Virtual,
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
        /// <returns>C# code for the class; identical to <see cref="Translate(ClassGraph)"/>'s <see cref="TranslatedClass.Code"/>.</returns>
        /// <exception cref="TranslationException">
        /// A class or member emitter threw (<c>NPT005</c>) or produced invalid output (<c>NPT007</c>), or a
        /// node has no translator (<c>NPT006</c>).
        /// </exception>
        public string TranslateClass(ClassGraph c) => Translate(c).Code;

        /// <summary>
        /// Translates a class into C#, together with the map from its generated code back to the nodes
        /// that produced it (compilation-and-diagnostics.md §2, research.md R3). Building the map never
        /// changes <see cref="TranslatedClass.Code"/> (RC-T06): it is exactly what <see cref="TranslateClass"/>
        /// returns.
        /// </summary>
        /// <param name="c">Class to translate.</param>
        /// <returns>The class's generated C# and its source map.</returns>
        /// <exception cref="TranslationException">
        /// A class or member emitter threw (<c>NPT005</c>) or produced invalid output (<c>NPT007</c>), or a
        /// node has no translator (<c>NPT006</c>).
        /// </exception>
        public TranslatedClass Translate(ClassGraph c)
        {
            ArgumentNullException.ThrowIfNull(c);

            ClassEmitContext emitContext = new ClassEmitContext(c, c.AllBaseTypes.Select(t => t.ToString()));

            foreach (IClassEmitter emitter in environment.ClassEmitters)
            {
                RunEmitter(emitter.Id, "class", () => emitter.EmitClass(emitContext), AllowedClassModifiers, emitContext.ExtraModifiers);
            }

            StringBuilder content = new StringBuilder();
            List<NodeOffsetGroup> members = new List<NodeOffsetGroup>();

            void AppendMember(TranslatedMember member)
            {
                int baseOffset = content.Length;
                content.AppendLine(member.Code);

                foreach (NodeOffsetGroup group in member.Groups)
                {
                    members.Add(new NodeOffsetGroup(group.GraphKey, baseOffset + group.End,
                        group.Nodes.Select(n => new NodeOffset(baseOffset + n.Offset, n.NodeId)).ToList()));
                }
            }

            foreach (Variable v in c.Variables)
            {
                AppendMember(TranslateVariableCore(v));
            }

            foreach (ConstructorGraph constructor in c.Constructors)
            {
                AppendMember(TranslateExecutionGraphCore(constructor, EmittedMemberKind.Constructor, constructor.ToString()));
            }

            foreach (MethodGraph m in c.Methods)
            {
                AppendMember(TranslateExecutionGraphCore(m, EmittedMemberKind.Method, m.Name));
            }

            // Events are emitted after methods (data-model.md §4); event and method names share one
            // namespace on the generated class (NPT002, research.md K13).
            var usedMemberNames = new HashSet<string>(c.Methods.Select(m => m.Name), StringComparer.Ordinal);
            foreach (EventGraph eventGraph in c.EventGraphs)
            {
                foreach (EventEntryNode entry in eventGraph.Entries)
                {
                    if (!usedMemberNames.Add(entry.EventName))
                    {
                        throw new TranslationException(TranslationDiagnosticCodes.DuplicateMemberName, $"Duplicate event or method name '{entry.EventName}'.",
                            TranslatorUtil.TryGetGraphKey(eventGraph), entry.Id);
                    }

                    AppendMember(TranslateEventCore(eventGraph, entry));
                }
            }

            List<string> modifiers = new List<string>
            {
                TranslatorUtil.VisibilityTokens[c.Visibility],
            };

            if (c.Modifiers.HasFlag(ClassModifiers.Static))
            {
                modifiers.Add(CSharpKeywords.Static);
            }

            if (c.Modifiers.HasFlag(ClassModifiers.Abstract))
            {
                modifiers.Add(CSharpKeywords.Abstract);
            }

            if (c.Modifiers.HasFlag(ClassModifiers.Sealed))
            {
                modifiers.Add(CSharpKeywords.Sealed);
            }

            if (c.Modifiers.HasFlag(ClassModifiers.Partial))
            {
                modifiers.Add(CSharpKeywords.Partial);
            }

            string genericArguments = "";
            if (c.DeclaredGenericArguments.Count > 0)
            {
                genericArguments = "<" + string.Join(", ", c.DeclaredGenericArguments) + ">";
            }

            // Deduplicate: AllBaseTypes never repeats a type, but a class emitter (extension-points.md
            // §3) is free to add its own entries to BaseTypes and could repeat one already there.
            List<string> distinctBaseTypes = emitContext.BaseTypes.Distinct(StringComparer.Ordinal).ToList();
            string baseTypes = distinctBaseTypes.Count > 0 ? " : " + string.Join(", ", distinctBaseTypes) : "";

            string usings = string.Concat(emitContext.Usings.Select(u => $"using {u};\n"));

            string beforeContent = (string.IsNullOrWhiteSpace(c.Namespace) ? CLASS_TEMPLATE_NO_NAMESPACE : CLASS_TEMPLATE)
                .Replace("%Usings%", usings, StringComparison.Ordinal)
                .Replace("%Namespace%", c.Namespace, StringComparison.Ordinal)
                .Replace("%Attributes%", AttributeLines(emitContext.Attributes), StringComparison.Ordinal)
                .Replace("%ClassModifiers%", ModifierPrefix(modifiers, emitContext.ExtraModifiers), StringComparison.Ordinal)
                .Replace("%ClassName%", c.Name, StringComparison.Ordinal)
                .Replace("%GenericArguments%", genericArguments, StringComparison.Ordinal)
                .Replace("%BaseTypes%", baseTypes, StringComparison.Ordinal);

            int contentOffset = beforeContent.IndexOf("%Content%", StringComparison.Ordinal);
            string generatedCode = beforeContent.Replace("%Content%", content.ToString(), StringComparison.Ordinal);

            List<NodeOffsetGroup> absoluteMembers = members.Select(member => new NodeOffsetGroup(member.GraphKey, contentOffset + member.End,
                member.Nodes.Select(n => new NodeOffset(contentOffset + n.Offset, n.NodeId)).ToList())).ToList();

            (string formattedCode, SourceMap map) = BuildSourceMap(generatedCode, absoluteMembers);
            return new TranslatedClass(c.FullName, formattedCode, map);
        }

        /// <summary>
        /// Translates a variable into C#.
        /// </summary>
        /// <param name="variable">Variable to translate.</param>
        /// <returns>C# code for the variable.</returns>
        /// <exception cref="TranslationException">
        /// A member emitter threw (<c>NPT005</c>) or produced invalid output (<c>NPT007</c>).
        /// </exception>
        public string TranslateVariable(Variable variable) => TranslateVariableCore(variable).Code;

        private TranslatedMember TranslateVariableCore(Variable variable)
        {
            List<string> modifiers = new List<string>
            {
                TranslatorUtil.VisibilityTokens[variable.Visibility],
            };

            if (variable.Modifiers.HasFlag(VariableModifiers.Static))
            {
                modifiers.Add(CSharpKeywords.Static);
            }

            if (variable.Modifiers.HasFlag(VariableModifiers.ReadOnly))
            {
                modifiers.Add(CSharpKeywords.ReadOnly);
            }

            if (variable.Modifiers.HasFlag(VariableModifiers.New))
            {
                modifiers.Add(CSharpKeywords.New);
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
                // A partial property declaration has neither bodies nor a backing field: no node produces it.
                List<string> accessors = new List<string>();

                if (variable.GetterMethod != null)
                {
                    accessors.Add($"{AccessorVisibilityPrefix(variable, variable.GetterMethod)}get;");
                }

                if (variable.SetterMethod != null)
                {
                    accessors.Add($"{AccessorVisibilityPrefix(variable, variable.SetterMethod)}set;");
                }

                string declaration = $"{attributes}{ModifierPrefix(modifiers, emitContext.ExtraModifiers, forcePartial: true)}{variable.Type.FullCodeName} {variable.Name} {{ {string.Join(" ", accessors)} }}";
                return new TranslatedMember(declaration, []);
            }

            string modifierText = ModifierPrefix(modifiers, emitContext.ExtraModifiers);

            if (variable.HasAccessors)
            {
                // Translate get / set methods

                string output = PROPERTY_TEMPLATE
                    .Replace("%Attributes%", attributes, StringComparison.Ordinal)
                    .Replace("%VariableModifiers%", modifierText, StringComparison.Ordinal)
                    .Replace("%VariableType%", variable.Type.FullCodeName, StringComparison.Ordinal)
                    .Replace("%VariableName%", variable.Name, StringComparison.Ordinal);

                List<NodeOffsetGroup> groups = new List<NodeOffsetGroup>(2);

                if (variable.GetterMethod != null)
                {
                    string getterMethodCode = methodTranslator.Translate(variable.GetterMethod, false);
                    List<NodeOffset> getterOffsets = new List<NodeOffset>(methodTranslator.LastNodeOffsets);
                    string visibilityPrefix = AccessorVisibilityPrefix(variable, variable.GetterMethod);
                    string getBlock = $"{visibilityPrefix}get\n{getterMethodCode}";
                    int placeholderStart = output.IndexOf("%Get%", StringComparison.Ordinal);
                    output = output.Replace("%Get%", getBlock, StringComparison.Ordinal);

                    if (getterOffsets.Count > 0 && TranslatorUtil.TryGetGraphKey(variable.GetterMethod) is { } getterGraphKey)
                    {
                        int codeStart = placeholderStart + getBlock.Length - getterMethodCode.Length;
                        groups.Add(new NodeOffsetGroup(getterGraphKey, codeStart + getterMethodCode.Length,
                            getterOffsets.Select(n => new NodeOffset(codeStart + n.Offset, n.NodeId)).ToList()));
                    }
                }
                else
                {
                    output = output.Replace("%Get%", "", StringComparison.Ordinal);
                }

                if (variable.SetterMethod != null)
                {
                    string setterMethodCode = methodTranslator.Translate(variable.SetterMethod, false);
                    List<NodeOffset> setterOffsets = new List<NodeOffset>(methodTranslator.LastNodeOffsets);
                    string visibilityPrefix = AccessorVisibilityPrefix(variable, variable.SetterMethod);
                    string setBlock = $"{visibilityPrefix}set\n{setterMethodCode}";
                    int placeholderStart = output.IndexOf("%Set%", StringComparison.Ordinal);
                    output = output.Replace("%Set%", setBlock, StringComparison.Ordinal);

                    if (setterOffsets.Count > 0 && TranslatorUtil.TryGetGraphKey(variable.SetterMethod) is { } setterGraphKey)
                    {
                        int codeStart = placeholderStart + setBlock.Length - setterMethodCode.Length;
                        groups.Add(new NodeOffsetGroup(setterGraphKey, codeStart + setterMethodCode.Length,
                            setterOffsets.Select(n => new NodeOffset(codeStart + n.Offset, n.NodeId)).ToList()));
                    }
                }
                else
                {
                    output = output.Replace("%Set%", "", StringComparison.Ordinal);
                }

                return new TranslatedMember(output, groups);
            }
            else
            {
                string field = VARIABLE_TEMPLATE
                    .Replace("%Attributes%", attributes, StringComparison.Ordinal)
                    .Replace("%VariableModifiers%", modifierText, StringComparison.Ordinal)
                    .Replace("%VariableType%", variable.Type.FullCodeName, StringComparison.Ordinal)
                    .Replace("%VariableName%", variable.Name, StringComparison.Ordinal);
                return new TranslatedMember(field, []);
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

        /// <summary>
        /// Translates one event graph entry to C#.
        /// </summary>
        /// <param name="eventGraph">Event graph <paramref name="entry"/> belongs to.</param>
        /// <param name="entry">Entry to translate.</param>
        /// <returns>C# code for the generated method.</returns>
        /// <exception cref="TranslationException">
        /// A member emitter threw (<c>NPT005</c>) or produced invalid output (<c>NPT007</c>), a node has no
        /// translator (<c>NPT006</c>), or <paramref name="entry"/> depends on another entry's node (<c>NPT001</c>).
        /// </exception>
        private TranslatedMember TranslateEventCore(EventGraph eventGraph, EventEntryNode entry)
        {
            if (eventGraph.Class is null)
            {
                return new TranslatedMember(methodTranslator.TranslateEventEntry(eventGraph, entry), []);
            }

            MemberEmitContext emitContext = EmitMember(eventGraph.Class, EmittedMemberKind.EventMethod, entry.EventName, entry);
            string code = methodTranslator.TranslateEventEntry(eventGraph, entry, emitContext.ExtraModifiers);
            List<NodeOffset> offsets = new List<NodeOffset>(methodTranslator.LastNodeOffsets);
            string attributes = AttributeLines(emitContext.Attributes);
            string full = attributes + code;

            if (offsets.Count == 0 || TranslatorUtil.TryGetGraphKey(eventGraph) is not { } graphKey)
            {
                return new TranslatedMember(full, []);
            }

            List<NodeOffset> nodes = offsets.Select(n => new NodeOffset(attributes.Length + n.Offset, n.NodeId)).ToList();
            return new TranslatedMember(full, [new NodeOffsetGroup(graphKey, full.Length, nodes)]);
        }

        private string TranslateExecutionGraph(ExecutionGraph graph, EmittedMemberKind kind, string name) =>
            TranslateExecutionGraphCore(graph, kind, name).Code;

        private TranslatedMember TranslateExecutionGraphCore(ExecutionGraph graph, EmittedMemberKind kind, string name)
        {
            if (graph.Class is null)
            {
                return new TranslatedMember(methodTranslator.Translate(graph, true), []);
            }

            MemberEmitContext emitContext = EmitMember(graph.Class, kind, name, graph);
            string code = methodTranslator.Translate(graph, true, emitContext.ExtraModifiers);
            List<NodeOffset> offsets = new List<NodeOffset>(methodTranslator.LastNodeOffsets);
            string attributes = AttributeLines(emitContext.Attributes);
            string full = attributes + code;

            if (offsets.Count == 0 || TranslatorUtil.TryGetGraphKey(graph) is not { } graphKey)
            {
                return new TranslatedMember(full, []);
            }

            List<NodeOffset> nodes = offsets.Select(n => new NodeOffset(attributes.Length + n.Offset, n.NodeId)).ToList();
            return new TranslatedMember(full, [new NodeOffsetGroup(graphKey, full.Length, nodes)]);
        }

        private MemberEmitContext EmitMember(ClassGraph cls, EmittedMemberKind kind, string name, object model)
        {
            MemberEmitContext context = new MemberEmitContext(cls, kind, name, model);

            foreach (IMemberEmitter emitter in environment.MemberEmitters)
            {
                RunEmitter(emitter.Id, "member", () => emitter.EmitMember(context), AllowedMemberModifiers, context.ExtraModifiers);

                if (context.DeclarePartial && kind != EmittedMemberKind.Property)
                {
                    throw new TranslationException(TranslationDiagnosticCodes.InvalidEmitterOutput, $"{emitter.Id}: DeclarePartial is only valid on a property, not on {kind} '{name}'.",
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
                throw new TranslationException(TranslationDiagnosticCodes.EmitterFailed, $"{id}: {ex.Message}", inner: ex);
            }

            string? invalid = extraModifiers.FirstOrDefault(modifier => !allowedModifiers.Contains(modifier));
            if (invalid != null)
            {
                throw new TranslationException(TranslationDiagnosticCodes.InvalidEmitterOutput, $"{id}: '{invalid}' is not an allowed {target} modifier.");
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
            List<string> result = modifiers.Where(modifier => modifier != CSharpKeywords.Partial).ToList();
            result.AddRange(extraModifiers.Where(modifier => modifier != CSharpKeywords.Partial && !result.Contains(modifier)));

            if (forcePartial || modifiers.Contains(CSharpKeywords.Partial) || extraModifiers.Contains(CSharpKeywords.Partial))
            {
                result.Add(CSharpKeywords.Partial);
            }

            return string.Concat(result.Select(modifier => modifier + " "));
        }

        /// <summary>
        /// Ranks each <see cref="MemberVisibility"/> single-flag value <see cref="TranslatorUtil.VisibilityTokens"/>
        /// emits a keyword for, from most restrictive (lowest) to least restrictive (highest).
        /// </summary>
        private static readonly Dictionary<MemberVisibility, int> VisibilityRestrictiveness = new Dictionary<MemberVisibility, int>
        {
            [MemberVisibility.Private] = 0,
            [MemberVisibility.Protected] = 1,
            [MemberVisibility.Internal] = 1,
            [MemberVisibility.Public] = 2,
        };

        /// <summary>
        /// The accessor's visibility keyword followed by a space, or an empty string when it must be
        /// omitted: C# only allows an accessor modifier that is strictly more restrictive than the
        /// property's own visibility (equal is redundant, less restrictive is invalid, so both are
        /// dropped here rather than emitted).
        /// </summary>
        private static string AccessorVisibilityPrefix(Variable variable, MethodGraph accessor)
        {
            bool isMoreRestrictive = VisibilityRestrictiveness.TryGetValue(accessor.Visibility, out int accessorRank)
                && VisibilityRestrictiveness.TryGetValue(variable.Visibility, out int propertyRank)
                && accessorRank < propertyRank;

            return isMoreRestrictive ? $"{TranslatorUtil.VisibilityTokens[accessor.Visibility]} " : "";
        }

        /// <summary>
        /// Builds the <see cref="SourceMap"/> of <paramref name="generatedCode"/> from every member's
        /// node offsets, then formats the code (research.md R3): the offsets are recorded before
        /// formatting as <see cref="SyntaxAnnotation"/>s on the token where each node's statements
        /// start, so they survive the whitespace changes formatting makes, and are read back from the
        /// formatted tree. <paramref name="generatedCode"/> is formatted identically whether or not
        /// <paramref name="members"/> has any entries (RC-T06).
        /// </summary>
        /// <param name="generatedCode">Unformatted class code, with every entry of
        /// <paramref name="members"/> already made absolute within it.</param>
        /// <param name="members">Every member's own node offsets and end-of-member offset, absolute
        /// within <paramref name="generatedCode"/>.</param>
        /// <returns>The formatted code and its source map.</returns>
        private static (string Code, SourceMap Map) BuildSourceMap(string generatedCode, IReadOnlyList<NodeOffsetGroup> members)
        {
            if (members.Count == 0)
            {
                return (TranslatorUtil.FormatCode(generatedCode), SourceMap.Empty);
            }

            SyntaxNode root = CSharpSyntaxTree.ParseText(generatedCode).GetCompilationUnitRoot();

            var annotationsByToken = new Dictionary<SyntaxToken, List<SyntaxAnnotation>>();

            void Annotate(int position, SyntaxAnnotation annotation)
            {
                SyntaxToken token = root.FindToken(position);
                if (!annotationsByToken.TryGetValue(token, out List<SyntaxAnnotation>? list))
                {
                    list = new List<SyntaxAnnotation>();
                    annotationsByToken[token] = list;
                }

                list.Add(annotation);
            }

            for (int memberIndex = 0; memberIndex < members.Count; memberIndex++)
            {
                NodeOffsetGroup member = members[memberIndex];
                string memberTag = memberIndex.ToString(CultureInfo.InvariantCulture);

                foreach (NodeOffset node in member.Nodes)
                {
                    Annotate(node.Offset, new SyntaxAnnotation(NodeIdAnnotationKind, node.NodeId));
                    Annotate(node.Offset, new SyntaxAnnotation(MemberAnnotationKind, memberTag));
                }

                Annotate(member.End, new SyntaxAnnotation(MemberEndAnnotationKind, memberTag));
            }

            SyntaxNode annotatedRoot = root.ReplaceTokens(annotationsByToken.Keys,
                (original, _) => original.WithAdditionalAnnotations(annotationsByToken[original].ToArray()));

            SyntaxNode formattedRoot = TranslatorUtil.FormatCode(annotatedRoot);
            string formattedCode = formattedRoot.ToFullString();

            var memberEndPositions = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (SyntaxNodeOrToken item in formattedRoot.GetAnnotatedNodesAndTokens(MemberEndAnnotationKind))
            {
                string tag = item.GetAnnotations(MemberEndAnnotationKind).First().Data!;
                memberEndPositions[tag] = item.SpanStart;
            }

            var nodesByMember = new Dictionary<string, List<(int Start, string NodeId)>>(StringComparer.Ordinal);
            foreach (SyntaxNodeOrToken item in formattedRoot.GetAnnotatedNodesAndTokens(NodeIdAnnotationKind).OrderBy(item => item.SpanStart))
            {
                string nodeId = item.GetAnnotations(NodeIdAnnotationKind).First().Data!;
                string memberTag = item.GetAnnotations(MemberAnnotationKind).First().Data!;

                if (!nodesByMember.TryGetValue(memberTag, out List<(int, string)>? list))
                {
                    list = new List<(int, string)>();
                    nodesByMember[memberTag] = list;
                }

                list.Add((item.SpanStart, nodeId));
            }

            var entries = new List<SourceMapEntry>();
            for (int memberIndex = 0; memberIndex < members.Count; memberIndex++)
            {
                string memberTag = memberIndex.ToString(CultureInfo.InvariantCulture);
                if (!nodesByMember.TryGetValue(memberTag, out List<(int Start, string NodeId)>? nodes))
                {
                    continue;
                }

                int memberEnd = memberEndPositions.TryGetValue(memberTag, out int end) ? end : formattedCode.Length;
                string graphKey = members[memberIndex].GraphKey;

                for (int i = 0; i < nodes.Count; i++)
                {
                    int spanEnd = i + 1 < nodes.Count ? nodes[i + 1].Start : memberEnd;
                    spanEnd = Math.Max(spanEnd, nodes[i].Start + 1);
                    entries.Add(new SourceMapEntry(TextSpan.FromBounds(nodes[i].Start, spanEnd), graphKey, nodes[i].NodeId));
                }
            }

            entries.Sort((a, b) => a.Span.Start.CompareTo(b.Span.Start));
            return (formattedCode, new SourceMap(entries));
        }

        /// <summary>
        /// One member's (method, constructor, event or property accessor) own generated code, and the
        /// node offset groups within it (research.md R3): 0 groups for a member with no exec nodes
        /// (a field, an abstract/partial member), 1 for a method/constructor/event, up to 2 for a
        /// property (its getter and setter each translate as their own group).
        /// </summary>
        private readonly record struct TranslatedMember(string Code, IReadOnlyList<NodeOffsetGroup> Groups);

        /// <summary>
        /// One graph's node offsets within a <see cref="TranslatedMember"/>'s code (research.md R3), or,
        /// once shifted by <see cref="ClassTranslator.Translate(ClassGraph)"/>, within the class's full
        /// generated code.
        /// </summary>
        /// <param name="GraphKey">Graph key (<c>NetPrints.Core.GraphKeys.For</c>) of the graph the nodes
        /// belong to.</param>
        /// <param name="End">Offset right after the graph's own generated code: bounds the last node's
        /// mapped span.</param>
        /// <param name="Nodes">Every node's own offset, in translation order.</param>
        private readonly record struct NodeOffsetGroup(string GraphKey, int End, IReadOnlyList<NodeOffset> Nodes);
    }
}
