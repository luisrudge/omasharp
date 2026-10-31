using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Omasharp.Analyzers;

sealed class Backends
{
    readonly INamedTypeSymbol? _base;
    readonly INamedTypeSymbol? _configured;

    Backends(INamedTypeSymbol? @base, INamedTypeSymbol? configured)
    {
        _base = @base;
        _configured = configured;
    }

    public static Backends For(Compilation compilation, AnalyzerOptions options)
    {
        INamedTypeSymbol? configured = null;
        if (options.AnalyzerConfigOptionsProvider.GlobalOptions.TryGetValue("build_property.BackendType", out var name)
            && !string.IsNullOrWhiteSpace(name))
            configured = compilation.GetTypeByMetadataName(name.Trim());
        return new Backends(compilation.GetTypeByMetadataName("Omasharp.BackendBase"), configured);
    }

    public bool Any => _base != null || _configured != null;

    public bool IsBackendBase(INamedTypeSymbol? type) => type != null && _base != null && SymbolEqualityComparer.Default.Equals(type.OriginalDefinition, _base);

    public bool IsBackend(INamedTypeSymbol? type)
    {
        if (type == null || type.TypeKind != TypeKind.Class || IsBackendBase(type)) return false;
        if (_configured != null && SymbolEqualityComparer.Default.Equals(type.OriginalDefinition, _configured)) return true;
        if (_base == null) return false;
        for (var t = type.BaseType; t != null; t = t.BaseType)
            if (IsBackendBase(t)) return true;
        return false;
    }

    public static bool IsVisibleToQml(ISymbol member) => member switch
    {
        IPropertySymbol p => p.DeclaredAccessibility == Accessibility.Public && !p.IsStatic && !p.IsIndexer
            && p.GetMethod?.DeclaredAccessibility == Accessibility.Public,
        IMethodSymbol m => m.DeclaredAccessibility == Accessibility.Public && !m.IsStatic && m.MethodKind == MethodKind.Ordinary
            && !m.IsGenericMethod && !IsDispose(m),
        _ => false,
    };

    public static bool IsDispose(IMethodSymbol m) =>
        m.Name == "Dispose" && m.Parameters.Length == 0 && m.ReturnsVoid
        && m.ContainingType.AllInterfaces.Any(i => i.ToDisplayString() == "System.IDisposable");
}
