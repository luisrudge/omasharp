; Unshipped analyzer release
; https://github.com/dotnet/roslyn-analyzers/blob/main/src/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md

### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|-------
OMASHARP001 | Omasharp | Error | A name QML can't take
OMASHARP002 | Omasharp | Error | Two members with the same QML name
OMASHARP003 | Omasharp | Warning | A public member QML can't see
OMASHARP004 | Omasharp | Warning | A setter that never tells QML
OMASHARP005 | Omasharp | Warning | A computed property QML is never told about
OMASHARP006 | Omasharp | Warning | Notify names no public property
OMASHARP007 | Omasharp | Error | QML can't call or set this
OMASHARP008 | Omasharp | Error | QML can't use this value
OMASHARP009 | Omasharp | Warning | async void in the backend
OMASHARP010 | Omasharp | Warning | ConfigureAwait(false) in the backend
