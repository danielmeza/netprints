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
NPA010 | Authoring | Warning | A declared pin key is in neither NodePins.Shipped.txt nor NodePins.Unshipped.txt
NPA011 | Authoring | Error | A shipped pin key is no longer declared; the old and the new key are named
NPA012 | Authoring | Info | A node kind declares pins, but the project has neither NodePins.Shipped.txt nor NodePins.Unshipped.txt
NPA040 | Authoring | Warning | A convention resource key of a node kind is missing from the neutral resx
NPA041 | Authoring | Error | A satellite resx has a key the neutral one lacks, or a placeholder count differs
NPA042 | Authoring | Info | A Node resx key of a node kind in the project that no declaration uses
