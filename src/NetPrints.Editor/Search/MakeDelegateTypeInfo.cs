using NetPrints.Core;

namespace NetPrints.Editor.Search;

/// <summary>
/// Search entry "Make Delegate For A Method Of {Type}" (PAR-54).
/// </summary>
public sealed record MakeDelegateTypeInfo(TypeSpecifier Type, TypeSpecifier FromType);
