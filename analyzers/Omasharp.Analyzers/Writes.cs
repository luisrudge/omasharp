using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Omasharp.Analyzers;

/// <summary>The backend's fields and properties that its own code changes after startup: assigned,
/// incremented, passed by ref, or (collections) added to or cleared, anywhere but in a constructor
/// or an initializer. Lambdas count as after startup even when a constructor creates them (a timer
/// callback, a file watcher's handler). Only writes it's sure about: when in doubt, it says no.</summary>
sealed class Writes
{
    static readonly HashSet<string> s_mutators = new()
    {
        "Add", "AddRange", "Insert", "InsertRange", "Remove", "RemoveAt", "RemoveAll", "RemoveRange", "Clear",
        "Sort", "Reverse", "Enqueue", "Dequeue", "Push", "Pop", "TryAdd", "TryRemove", "TryDequeue", "TryPop",
        "AddOrUpdate", "Append", "AppendLine", "Prepend", "AddFirst", "AddLast", "RemoveFirst", "RemoveLast",
    };

    readonly HashSet<ISymbol> _changed = new(SymbolEqualityComparer.Default);

    public bool Changes(ISymbol s) => _changed.Contains(s.OriginalDefinition);

    public static Writes Of(INamedTypeSymbol type, Compilation compilation, CancellationToken ct)
    {
        var w = new Writes();
        foreach (var r in type.DeclaringSyntaxReferences)
        {
            var root = r.GetSyntax(ct);
            var model = compilation.GetSemanticModel(root.SyntaxTree);
            foreach (var n in root.DescendantNodes())
            {
                ExpressionSyntax? target = n switch
                {
                    AssignmentExpressionSyntax a => a.Left,
                    PrefixUnaryExpressionSyntax u when u.IsKind(SyntaxKind.PreIncrementExpression) || u.IsKind(SyntaxKind.PreDecrementExpression) => u.Operand,
                    PostfixUnaryExpressionSyntax u when u.IsKind(SyntaxKind.PostIncrementExpression) || u.IsKind(SyntaxKind.PostDecrementExpression) => u.Operand,
                    ArgumentSyntax arg when arg.RefKindKeyword.IsKind(SyntaxKind.RefKeyword) || arg.RefKindKeyword.IsKind(SyntaxKind.OutKeyword) => arg.Expression,
                    InvocationExpressionSyntax { Expression: MemberAccessExpressionSyntax ma } when s_mutators.Contains(ma.Name.Identifier.ValueText) => ma.Expression,
                    _ => null,
                };
                if (target == null || AtStartup(n)) continue;
                // _items[i] = x changes _items.
                while (target is ElementAccessExpressionSyntax e) target = e.Expression;
                var s = model.GetSymbolInfo(target, ct).Symbol;
                if (s is IFieldSymbol or IPropertySymbol && SymbolEqualityComparer.Default.Equals(s.ContainingType?.OriginalDefinition, type.OriginalDefinition))
                    w._changed.Add(s.OriginalDefinition);
            }
        }
        return w;
    }

    // In a constructor or a field/property initializer, and not inside a lambda or local function there.
    static bool AtStartup(SyntaxNode n)
    {
        foreach (var a in n.Ancestors())
            switch (a)
            {
                case AnonymousFunctionExpressionSyntax or LocalFunctionStatementSyntax: return false;
                case ConstructorDeclarationSyntax: return true;
                case EqualsValueClauseSyntax { Parent: VariableDeclaratorSyntax { Parent.Parent: FieldDeclarationSyntax } }: return true;
                case EqualsValueClauseSyntax { Parent: PropertyDeclarationSyntax }: return true;
                case MemberDeclarationSyntax: return false;
            }
        return false;
    }
}
