; Unshipped analyzer release
; https://github.com/dotnet/roslyn/blob/main/src/RoslynAnalyzers/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md

### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|-------
NPA001 | Authoring | Error | A pin property or its class is not partial, the property is not get-only, or its pin type does not match the attribute
NPA002 | Authoring | Error | Duplicate pin key in one collection, including base-class pins
NPA003 | Authoring | Error | The constructor does not call DeclarePins() exactly once, or adds pins to a declared collection before it
NPA004 | Authoring | Error | Invalid pin key: empty, or containing a dot, a slash or a tilde
NPA005 | Authoring | Error | A base class adds pins imperatively to a collection in which the convention declares pins
NPA006 | Authoring | Error | A DefaultValue does not convert to the pin's type
NPA007 | Authoring | Error | The PinType member is missing or has the wrong type
NPA008 | Authoring | Error | A pin property name does not end in Pin and has no Pin attribute
NPA009 | Authoring | Error | Pin properties in a class without NodeKind or NodeBase
