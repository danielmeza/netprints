; Unshipped analyzer release
; https://github.com/dotnet/roslyn/blob/main/src/RoslynAnalyzers/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md

### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|-------
NPC001 | NetPrints.Catalog | Error | A catalog request names an assembly that is not referenced
NPC002 | NetPrints.Catalog | Error | Unknown catalog profile
NPC003 | NetPrints.Catalog | Error | Catalog profile file or catalog id is invalid
NPC004 | NetPrints.Catalog | Warning | NetPrints annotation on a symbol that is not public
NPC005 | NetPrints.Catalog | Warning | Catalog member skipped because an assembly is not available
NPC006 | NetPrints.Catalog | Error | Two catalogs have the same id
