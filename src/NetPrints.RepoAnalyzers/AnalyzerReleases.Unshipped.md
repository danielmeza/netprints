; Unshipped analyzer release
; https://github.com/dotnet/roslyn/blob/main/src/RoslynAnalyzers/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md

### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|-------
NPS001 | Style | Disabled | An attribute list shares a line with its member or another attribute list; error once the repository enables it
NPS002 | Style | Disabled | No blank line between adjacent members that need one; error once the repository enables it
NPS003 | Style | Disabled | Grouped non-public fields; a suggestion that never fails a build once enabled
