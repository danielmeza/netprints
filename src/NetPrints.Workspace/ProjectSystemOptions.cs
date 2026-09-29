#nullable enable
using System.Collections.Generic;
using NetPrints.Projects;

namespace NetPrints.Workspace;

/// <summary>
/// Options configuring <see cref="MsBuildProjectSystem"/> (project-system.md §4).
/// </summary>
/// <param name="ExtraProperties">Names of additional evaluated MSBuild properties to capture into
/// <see cref="ProjectSnapshot.Properties"/> — profile- or extension-specific settings a caller wants
/// back, read with <see cref="ProjectSnapshot.GetProperty"/>.</param>
/// <param name="NetPrintsSdkVersion">The <c>NetPrints.Sdk</c> package version substituted for a new
/// project's <c>{NetPrintsSdkVersion}</c> template placeholder (<see cref="IProjectSystem.CreateAsync"/>).
/// The caller supplies this explicitly; the editor derives its own from its MinVer-stamped assembly
/// version (release-and-docs.md, "Editor version").</param>
public sealed record ProjectSystemOptions(IReadOnlyList<string> ExtraProperties, string NetPrintsSdkVersion);
