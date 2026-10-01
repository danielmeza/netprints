#nullable enable
using System;

namespace NetPrints.Translator;

/// <summary>
/// Base for a <c>renderGenerated</c> callback exception that must abort the whole operation instead of
/// being isolated per class: <c>NetPrints.Serialization.ProjectPersistence.SaveAsync</c>'s per-class
/// isolation (F-07) catches any other exception and reports it as a diagnostic for that one class, but a
/// subclass of this one is left to propagate (e.g. <c>MainEditorViewModel.CompileAsync</c>'s
/// <c>ClassTranslationFailure</c>, which fails the whole build rather than silently skipping a broken
/// class's generated code).
/// </summary>
/// <param name="message">Human-readable description.</param>
/// <param name="inner">The exception that caused this one.</param>
public abstract class ClassTranslationAbortException(string message, Exception inner) : Exception(message, inner);
