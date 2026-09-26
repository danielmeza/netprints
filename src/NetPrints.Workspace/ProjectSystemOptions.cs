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
/// Not sourced from MinVer yet — that isn't wired up until sub-phase L (T109), so a caller supplies the
/// version explicitly until then, the same documented gap as sub-phase D's pack-and-consume tests.</param>
public sealed record ProjectSystemOptions(IReadOnlyList<string> ExtraProperties, string NetPrintsSdkVersion);
