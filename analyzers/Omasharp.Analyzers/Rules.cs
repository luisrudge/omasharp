using Microsoft.CodeAnalysis;

namespace Omasharp.Analyzers;

static class Rules
{
    const string Category = "Omasharp";
    const string Help = "https://github.com/luisrudge/omasharp/blob/main/analyzers/README.md#";

    static DiagnosticDescriptor Rule(string id, string title, string message, DiagnosticSeverity severity) =>
        new(id, title, message, Category, severity, isEnabledByDefault: true, helpLinkUri: Help + id.ToLowerInvariant());

    public static readonly DiagnosticDescriptor ReservedName = Rule("OMASHARP001",
        "A name QML can't take",
        "{0}",
        DiagnosticSeverity.Error);

    public static readonly DiagnosticDescriptor SameQmlName = Rule("OMASHARP002",
        "Two members with the same QML name",
        "{0} and {1} both become '{2}' in QML, so QML can only reach one of them; rename one",
        DiagnosticSeverity.Error);

    public static readonly DiagnosticDescriptor Invisible = Rule("OMASHARP003",
        "A public member QML can't see",
        "'{0}' is public but QML can't see it: {1}",
        DiagnosticSeverity.Warning);

    public static readonly DiagnosticDescriptor SilentSetter = Rule("OMASHARP004",
        "A setter that never tells QML",
        "the backend changes '{0}' after startup, but its setter never tells QML, so QML keeps showing the old value; store it with Set(ref _field, value) in the setter (or call Notify(nameof({0})) after changing it)",
        DiagnosticSeverity.Warning);

    public static readonly DiagnosticDescriptor StaleComputed = Rule("OMASHARP005",
        "A computed property QML is never told about",
        "'{0}' is computed from {1}, which the backend changes, but nothing calls Notify(nameof({0})), so QML keeps showing its old value; call Notify(nameof({0})) where {1} changes",
        DiagnosticSeverity.Warning);

    public static readonly DiagnosticDescriptor UnknownNotify = Rule("OMASHARP006",
        "Notify names no public property",
        "{0}(\"{1}\") names no public property of {2}, so nothing updates in QML; pass nameof(...) of the public property that changed",
        DiagnosticSeverity.Warning);

    public static readonly DiagnosticDescriptor CantCall = Rule("OMASHARP007",
        "QML can't call or set this",
        "{0}",
        DiagnosticSeverity.Error);

    public static readonly DiagnosticDescriptor CantReturn = Rule("OMASHARP008",
        "QML can't use this value",
        "QML can't use what '{0}' gives it ({1}): {2}",
        DiagnosticSeverity.Error);

    public static readonly DiagnosticDescriptor AsyncVoid = Rule("OMASHARP009",
        "async void in the backend",
        "'{0}' is async void: its exceptions end the app and QML can't wait for it; return Task instead (QML gets a Promise)",
        DiagnosticSeverity.Warning);

    public static readonly DiagnosticDescriptor ConfigureAwaitFalse = Rule("OMASHARP010",
        "ConfigureAwait(false) in the backend",
        "ConfigureAwait(false) moves the rest of '{0}' off the UI thread, where it races QML for the backend's state; remove it (awaits in the backend come back to the UI thread)",
        DiagnosticSeverity.Warning);

    public static readonly SuppressionDescriptor StaticSuppression = new("OMASHARPS001", "CA1822",
        "QML only sees the backend's instance members: making this static would hide it from QML.");
}
