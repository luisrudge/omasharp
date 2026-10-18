using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Omasharp.Analyzers;

/// <summary>Turns off CA1822 ("can be static") for the backend's public members: QML only sees
/// instance members, so following that advice would hide them from QML.</summary>
[DiagnosticAnalyzer(Microsoft.CodeAnalysis.LanguageNames.CSharp)]
public sealed class StaticSuppressor : DiagnosticSuppressor
{
    public override ImmutableArray<SuppressionDescriptor> SupportedSuppressions { get; } = ImmutableArray.Create(Rules.StaticSuppression);

    public override void ReportSuppressions(SuppressionAnalysisContext context)
    {
        Backends? backends = null;
        foreach (var d in context.ReportedDiagnostics)
        {
            var tree = d.Location.SourceTree;
            if (tree == null) continue;
            var node = tree.GetRoot(context.CancellationToken).FindNode(d.Location.SourceSpan);
            var decl = node.AncestorsAndSelf().FirstOrDefault(n => n is MethodDeclarationSyntax or PropertyDeclarationSyntax);
            if (decl == null) continue;
            var model = context.GetSemanticModel(tree);
            backends ??= Backends.For(model.Compilation, context.Options);
            var symbol = model.GetDeclaredSymbol(decl, context.CancellationToken);
            if (symbol != null && backends.IsBackend(symbol.ContainingType) && Backends.IsVisibleToQml(symbol))
                context.ReportSuppression(Suppression.Create(Rules.StaticSuppression, d));
        }
    }
}
