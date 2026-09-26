#nullable enable
using System;

namespace NetPrints.Core;

/// <summary>
/// One "New Class" choice offered by an <see cref="IProjectProfile"/> (extension-points.md §5): a
/// display name and a factory that builds the starting <see cref="ClassGraph"/> for a newly created
/// class of this template.
/// </summary>
/// <param name="Id">Stable identifier of the template, unique within its owning profile.</param>
/// <param name="DisplayName">Name shown in the New Class picker.</param>
/// <param name="Create">Builds the new class: given the owning <see cref="Project"/> and the class
/// name the user chose, returns the starting <see cref="ClassGraph"/> (not yet added to the
/// project).</param>
public sealed record ClassTemplate(string Id, string DisplayName, Func<Project, string, ClassGraph> Create);
