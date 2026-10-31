using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Omasharp.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class BackendAnalyzer : DiagnosticAnalyzer
{
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = ImmutableArray.Create(
        Rules.ReservedName, Rules.SameQmlName, Rules.Invisible, Rules.SilentSetter, Rules.StaleComputed,
        Rules.UnknownNotify, Rules.CantCall, Rules.CantReturn, Rules.AsyncVoid, Rules.ConfigureAwaitFalse);

    static readonly HashSet<string> s_notifiers = new()
    {
        "Set", "Notify", "OnPropertyChanged", "RaisePropertyChanged", "NotifyPropertyChanged", "SetProperty", "SetField",
    };

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(start =>
        {
            var backends = Backends.For(start.Compilation, start.Options);
            if (!backends.Any) return;
            start.RegisterSymbolAction(c => AnalyzeType(c, backends), SymbolKind.NamedType);
            start.RegisterOperationAction(c => AnalyzeInvocation(c, backends), OperationKind.Invocation);
        });
    }

    static void AnalyzeType(SymbolAnalysisContext c, Backends backends)
    {
        var type = (INamedTypeSymbol)c.Symbol;
        if (!backends.IsBackend(type)) return;
        var mentions = Mentions.Of(type, c.CancellationToken);
        var writes = Writes.Of(type, c.Compilation, c.CancellationToken);
        var visible = new List<ISymbol>();

        foreach (var member in type.GetMembers())
        {
            if (member.IsImplicitlyDeclared || !member.Locations.Any(l => l.IsInSource)) continue;
            var at = member.Locations.First(l => l.IsInSource);

            if (member is IMethodSymbol { MethodKind: MethodKind.Ordinary, IsAsync: true, ReturnsVoid: true } asyncVoid)
                c.ReportDiagnostic(Diagnostic.Create(Rules.AsyncVoid, at, asyncVoid.Name));

            if (member.DeclaredAccessibility != Accessibility.Public) continue;
            switch (member)
            {
                case IFieldSymbol f:
                    c.ReportDiagnostic(Diagnostic.Create(Rules.Invisible, at, f.Name, f.IsConst || f.IsStatic
                        ? "static members and constants aren't on `backend`; expose it as an instance property (public int Max => MaxValue;), or make it non-public if it's only for C#"
                        : "fields aren't on `backend`; make it a property (int _x; public int X { get => _x; set => Set(ref _x, value); }), or make it non-public if it's only for C#"));
                    break;
                case IEventSymbol e when e.Name != "PropertyChanged":
                    c.ReportDiagnostic(Diagnostic.Create(Rules.Invisible, at, e.Name,
                        "C# events don't reach QML; call Raise(\"name\", value) instead and handle it in QML with backend.raised.connect((name, value) => ...)"));
                    break;
                case IPropertySymbol p when p.IsStatic:
                    c.ReportDiagnostic(Diagnostic.Create(Rules.Invisible, at, p.Name,
                        "static members aren't on `backend`; make it an instance property, or make it non-public if it's only for C#"));
                    break;
                case IPropertySymbol p when p.IsIndexer:
                    c.ReportDiagnostic(Diagnostic.Create(Rules.Invisible, at, "this[]",
                        "QML can't index `backend`; expose a list property, or a method that takes the key"));
                    break;
                case IPropertySymbol p when p.GetMethod?.DeclaredAccessibility != Accessibility.Public:
                    c.ReportDiagnostic(Diagnostic.Create(Rules.Invisible, at, p.Name,
                        "it has no public getter; give it a public get, or make it non-public"));
                    break;
                case IPropertySymbol p:
                    visible.Add(p);
                    AnalyzeProperty(c, type, p, at, mentions, writes);
                    break;
                case IMethodSymbol { MethodKind: MethodKind.Ordinary } m when m.IsStatic:
                    c.ReportDiagnostic(Diagnostic.Create(Rules.Invisible, at, m.Name,
                        "static methods aren't on `backend`; make it an instance method, or make it internal if it's only for C# and tests"));
                    break;
                case IMethodSymbol { MethodKind: MethodKind.Ordinary } m when m.IsGenericMethod:
                    c.ReportDiagnostic(Diagnostic.Create(Rules.Invisible, at, m.Name,
                        "QML can't call generic methods; add a non-generic method for QML, or make it non-public"));
                    break;
                case IMethodSymbol { MethodKind: MethodKind.Ordinary } m when !Backends.IsDispose(m):
                    visible.Add(m);
                    AnalyzeMethod(c, m, at);
                    break;
            }
        }

        foreach (var group in visible.OfType<IMethodSymbol>().GroupBy(m => m.Name).Where(g => g.Count() > 1))
            foreach (var m in group)
                c.ReportDiagnostic(Diagnostic.Create(Rules.CantCall, Where(m),
                    $"QML can't reliably call '{m.Name}': it's overloaded, and QML picks an overload by argument count only; give each overload its own name"));

        foreach (var group in visible.GroupBy(m => QmlNames.Of(m.Name)))
        {
            var names = group.Select(m => m.Name).Distinct().ToList();
            if (names.Count < 2) continue;
            foreach (var m in group.Where(m => m.Name != names[0]))
                c.ReportDiagnostic(Diagnostic.Create(Rules.SameQmlName, Where(m), names[0], m.Name, group.Key));
        }
    }

    static Location Where(ISymbol s) => s.Locations.First(l => l.IsInSource);

    static void AnalyzeProperty(SymbolAnalysisContext c, INamedTypeSymbol type, IPropertySymbol p, Location at, Mentions mentions, Writes writes)
    {
        var qml = QmlNames.Of(p.Name);
        if (QmlNames.IsReserved(qml))
            c.ReportDiagnostic(Diagnostic.Create(Rules.ReservedName, at, QmlNames.ReservedMessage(type.Name, p.Name, qml)));

        var setter = p.SetMethod;
        if (setter?.DeclaredAccessibility == Accessibility.Public && !Crossing.CanComeFromQml(p.Type))
            c.ReportDiagnostic(Diagnostic.Create(Rules.CantCall, at,
                $"QML can't set '{p.Name}': a {Show(p.Type)} can't come from QML; make the setter private (QML then only reads it), or use {Crossing.Supported}"));

        if (Crossing.CantGoToQml(p.Type) is { } problem)
            c.ReportDiagnostic(Diagnostic.Create(Rules.CantReturn, at, p.Name, Show(p.Type), problem));

        if (mentions.NotifiesAll || mentions.Names.Contains(p.Name)) return;
        var syntax = p.DeclaringSyntaxReferences.Select(r => r.GetSyntax(c.CancellationToken)).OfType<PropertyDeclarationSyntax>().FirstOrDefault();
        if (syntax == null) return;

        if (setter != null && !setter.IsInitOnly)
        {
            var set = syntax.AccessorList?.Accessors.FirstOrDefault(a => a.IsKind(SyntaxKind.SetAccessorDeclaration));
            SyntaxNode? body = (SyntaxNode?)set?.Body ?? set?.ExpressionBody;
            if ((body == null || !CallsNotifier(body)) && writes.Changes(p))
                c.ReportDiagnostic(Diagnostic.Create(Rules.SilentSetter, at, p.Name));
            return;
        }

        if (setter == null && !IsAutoProperty(syntax))
        {
            var inputs = new SortedSet<string>();
            MutableInputs(c, type, p, writes, inputs, new HashSet<IPropertySymbol>(SymbolEqualityComparer.Default), 0);
            if (inputs.Count > 0)
                c.ReportDiagnostic(Diagnostic.Create(Rules.StaleComputed, at, p.Name, string.Join(", ", inputs)));
        }
    }

    static void AnalyzeMethod(SymbolAnalysisContext c, IMethodSymbol m, Location at)
    {
        var qml = QmlNames.Of(m.Name);
        if (QmlNames.IsReserved(qml))
            c.ReportDiagnostic(Diagnostic.Create(Rules.ReservedName, at, QmlNames.ReservedMessage(m.ContainingType.Name, m.Name, qml)));

        foreach (var p in m.Parameters)
        {
            var where = p.Locations.FirstOrDefault(l => l.IsInSource) ?? at;
            if (p.RefKind != RefKind.None)
                c.ReportDiagnostic(Diagnostic.Create(Rules.CantCall, where,
                    $"QML can't call '{m.Name}': parameter '{p.Name}' is {p.RefKind.ToString().ToLowerInvariant()}, and QML only passes values; return the result instead"));
            else if (p.IsParams)
                c.ReportDiagnostic(Diagnostic.Create(Rules.CantCall, where,
                    $"QML can't call '{m.Name}' with separate arguments: it doesn't expand params; drop `params` and let QML pass an array ([1, 2, 3])"));
            else if (!p.IsOptional && !Crossing.CanComeFromQml(p.Type))
                c.ReportDiagnostic(Diagnostic.Create(Rules.CantCall, where,
                    $"QML can't call '{m.Name}': parameter '{p.Name}' is a {Show(p.Type)}, which can't come from QML; take {Crossing.Supported} (an id or index instead of an object)"));
        }

        if (Crossing.CantGoToQml(m.ReturnType) is { } problem)
            c.ReportDiagnostic(Diagnostic.Create(Rules.CantReturn, at, m.Name, Show(m.ReturnType), problem));
    }

    static string Show(ITypeSymbol t) => t.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);

    static bool CallsNotifier(SyntaxNode body) =>
        body.DescendantNodesAndSelf().Any(n => n switch
        {
            InvocationExpressionSyntax i => s_notifiers.Contains(CalledName(i)),
            IdentifierNameSyntax id => id.Identifier.ValueText == "PropertyChanged",
            _ => false,
        });

    static string CalledName(InvocationExpressionSyntax i) => i.Expression switch
    {
        IdentifierNameSyntax id => id.Identifier.ValueText,
        GenericNameSyntax g => g.Identifier.ValueText,
        MemberAccessExpressionSyntax ma => ma.Name.Identifier.ValueText,
        MemberBindingExpressionSyntax mb => mb.Name.Identifier.ValueText,
        _ => "",
    };

    static bool IsAutoProperty(PropertyDeclarationSyntax p) =>
        p.ExpressionBody == null && p.AccessorList != null && p.AccessorList.Accessors.All(a => a.Body == null && a.ExpressionBody == null);

    static void MutableInputs(SymbolAnalysisContext c, INamedTypeSymbol type, IPropertySymbol p, Writes writes,
        SortedSet<string> inputs, HashSet<IPropertySymbol> seen, int depth)
    {
        if (depth > 4 || !seen.Add(p)) return;
        var syntax = p.DeclaringSyntaxReferences.Select(r => r.GetSyntax(c.CancellationToken)).OfType<PropertyDeclarationSyntax>().FirstOrDefault();
        if (syntax == null) return;
        var get = syntax.AccessorList?.Accessors.FirstOrDefault(a => a.IsKind(SyntaxKind.GetAccessorDeclaration));
        SyntaxNode? body = (SyntaxNode?)syntax.ExpressionBody ?? (SyntaxNode?)get?.Body ?? get?.ExpressionBody;
        if (body == null) return;
        var model = c.Compilation.GetSemanticModel(syntax.SyntaxTree);
        foreach (var id in body.DescendantNodes().OfType<IdentifierNameSyntax>())
        {
            var s = model.GetSymbolInfo(id, c.CancellationToken).Symbol;
            if (s == null || !SymbolEqualityComparer.Default.Equals(s.ContainingType?.OriginalDefinition, type.OriginalDefinition)) continue;
            switch (s)
            {
                case IFieldSymbol f when writes.Changes(f):
                    inputs.Add(f.Name);
                    break;
                case IPropertySymbol q when q.SetMethod is { IsInitOnly: false } set
                    && (writes.Changes(q) || (set.DeclaredAccessibility == Accessibility.Public && Backends.IsVisibleToQml(q))):
                    inputs.Add(q.Name);
                    break;
                case IPropertySymbol q when q.SetMethod == null:
                    MutableInputs(c, type, q, writes, inputs, seen, depth + 1);
                    break;
            }
        }
    }

    static void AnalyzeInvocation(OperationAnalysisContext c, Backends backends)
    {
        var call = (IInvocationOperation)c.Operation;
        var type = c.ContainingSymbol.ContainingType;
        if (!backends.IsBackend(type)) return;
        var target = call.TargetMethod;

        if (target.Name == "ConfigureAwait" && call.Arguments.Length == 1)
        {
            var v = call.Arguments[0].Value.ConstantValue;
            var leaves = v.HasValue && (v.Value is false || (v.Value is int options && (options & 1) == 0));
            if (leaves) c.ReportDiagnostic(Diagnostic.Create(Rules.ConfigureAwaitFalse, call.Syntax.GetLocation(), c.ContainingSymbol.Name));
            return;
        }

        if (!s_notifiers.Contains(target.Name)) return;
        var owner = target.ContainingType;
        if (!backends.IsBackendBase(owner) && !SymbolEqualityComparer.Default.Equals(owner?.OriginalDefinition, type.OriginalDefinition)) return;
        foreach (var arg in call.Arguments)
        {
            if (arg.ArgumentKind == ArgumentKind.DefaultValue || arg.Parameter is not { } param || !IsPropertyNameParameter(param)) continue;
            var values = arg.Value is IArrayCreationOperation { Initializer: { } init } ? init.ElementValues : ImmutableArray.Create(arg.Value);
            foreach (var value in values)
            {
                if (value.ConstantValue is not { HasValue: true, Value: string name } || name.Length == 0) continue;
                if (!IsQmlProperty(type, name))
                    c.ReportDiagnostic(Diagnostic.Create(Rules.UnknownNotify, value.Syntax.GetLocation(), target.Name, name, type.Name));
            }
        }
    }

    static bool IsPropertyNameParameter(IParameterSymbol p) =>
        (p.Type.SpecialType == SpecialType.System_String || p.Type is IArrayTypeSymbol { ElementType.SpecialType: SpecialType.System_String })
        && (p.GetAttributes().Any(a => a.AttributeClass?.Name == "CallerMemberNameAttribute")
            || p.Name.IndexOf("name", System.StringComparison.OrdinalIgnoreCase) >= 0);

    static bool IsQmlProperty(INamedTypeSymbol type, string name)
    {
        for (var t = type; t != null; t = t.BaseType)
            if (t.GetMembers(name).OfType<IPropertySymbol>().Any(Backends.IsVisibleToQml)) return true;
        return false;
    }
}

sealed class Mentions
{
    public HashSet<string> Names { get; } = new();
    public bool NotifiesAll { get; private set; }

    public static Mentions Of(INamedTypeSymbol type, System.Threading.CancellationToken ct)
    {
        var m = new Mentions();
        foreach (var r in type.DeclaringSyntaxReferences)
            foreach (var n in r.GetSyntax(ct).DescendantNodes())
                switch (n)
                {
                    case InvocationExpressionSyntax { Expression: IdentifierNameSyntax { Identifier.ValueText: "nameof" } } i
                        when i.ArgumentList.Arguments.Count == 1:
                        var e = i.ArgumentList.Arguments[0].Expression;
                        var last = e is MemberAccessExpressionSyntax ma ? ma.Name.Identifier.ValueText : e is IdentifierNameSyntax id ? id.Identifier.ValueText : null;
                        if (last != null) m.Names.Add(last);
                        break;
                    case LiteralExpressionSyntax { RawKind: (int)SyntaxKind.StringLiteralExpression } s:
                        m.Names.Add(s.Token.ValueText);
                        break;
                    case InvocationExpressionSyntax i when i.ArgumentList.Arguments.Any(x => IsEmpty(x.Expression))
                        && i.Expression.ToString().EndsWith("Notify", System.StringComparison.Ordinal) | i.Expression.ToString().EndsWith("PropertyChanged", System.StringComparison.Ordinal):
                        m.NotifiesAll = true;
                        break;
                    case ObjectCreationExpressionSyntax { ArgumentList.Arguments.Count: 1 } o
                        when o.Type.ToString().EndsWith("PropertyChangedEventArgs", System.StringComparison.Ordinal) && IsEmpty(o.ArgumentList.Arguments[0].Expression):
                    case ImplicitObjectCreationExpressionSyntax { ArgumentList.Arguments.Count: 1 } io when IsEmpty(io.ArgumentList.Arguments[0].Expression):
                        m.NotifiesAll = true;
                        break;
                }
        return m;
    }

    static bool IsEmpty(ExpressionSyntax e) =>
        e.IsKind(SyntaxKind.NullLiteralExpression)
        || (e is LiteralExpressionSyntax l && l.IsKind(SyntaxKind.StringLiteralExpression) && l.Token.ValueText.Length == 0)
        || e.ToString() is "string.Empty" or "String.Empty";
}
