#nullable enable
using System;

namespace NetPrints.Projects;

/// <summary>
/// An <see cref="IProjectSystem"/> operation could not proceed at all (project-system.md §4): no
/// .NET SDK could be registered, or in-process MSBuild evaluation failed. Problems that still let the
/// caller continue (a failed restore, a workspace diagnostic) are reported as an <c>Error</c>/
/// <c>Warning</c> <see cref="ProjectMessage"/> instead of this exception.
/// </summary>
public sealed class ProjectSystemException : Exception
{
    /// <summary>
    /// No .NET SDK could be registered by <c>MsBuildRegistration.EnsureRegistered</c>; every
    /// <see cref="IProjectSystem"/> member throws this before doing anything else.
    /// </summary>
    public const string NoSdkRegistered = "NPW001";

    /// <summary>
    /// In-process MSBuild evaluation of the project failed; the message carries MSBuild's own error
    /// text.
    /// </summary>
    public const string EvaluationFailed = "NPW003";

    /// <summary>
    /// Creates a project system exception.
    /// </summary>
    /// <param name="code">Stable machine-readable code: <see cref="NoSdkRegistered"/> or
    /// <see cref="EvaluationFailed"/>.</param>
    /// <param name="message">Human-readable description of the problem.</param>
    /// <param name="inner">Underlying exception, if any.</param>
    public ProjectSystemException(string code, string message, Exception? inner = null)
        : base(message, inner)
    {
        Code = code;
    }

    /// <summary>Stable machine-readable code identifying the kind of failure.</summary>
    public string Code { get; }
}
