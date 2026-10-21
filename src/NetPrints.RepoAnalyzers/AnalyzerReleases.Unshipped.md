; Unshipped analyzer release
; https://github.com/dotnet/roslyn/blob/main/src/RoslynAnalyzers/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md

### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|-------
NPS001 | Style | Disabled | An attribute list shares a line with its member or another attribute list; error once the repository enables it
NPS002 | Style | Disabled | No blank line between adjacent members that need one; error once the repository enables it
NPS003 | Style | Disabled | Grouped non-public fields; a suggestion that never fails a build once enabled
NPI001 | Composition | Error | A singleton takes a session-scoped service
NPI002 | Composition | Error | A service attribute on a type that cannot be registered
NPI003 | Composition | Error | A registered type has no single constructor to call
NPI004 | Composition | Error | Two registrations of one service where a consumer takes a single instance
NPI005 | Composition | Error | A type registered for several services cannot share one instance
NPI010 | SmartEnum | Error | A smart enum is not a top-level abstract partial class
NPI011 | SmartEnum | Error | A smart enum member is not a static readonly field, or a lookup reads All before the members
NPH001 | Hygiene | Disabled | The null-forgiving operator at a site the hygiene ledger does not list; error once the repository enables it
NPH002 | Hygiene | Disabled | A type name that abbreviates ViewModel as VM; error once the repository enables it
NPH003 | Hygiene | Disabled | A diagnostic code literal outside its constants class; error once the repository enables it
NPH004 | Hygiene | Disabled | A pragma, a nullable escape or a SuppressMessage the hygiene ledger does not list; error once the repository enables it
NPH005 | Hygiene | Disabled | ConfigureAwait(false) in UI-bound code; error once the repository enables it
NPH006 | Hygiene | Disabled | A string literal passed to a user-visible text sink; error once the repository enables it
