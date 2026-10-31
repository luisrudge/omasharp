using Microsoft.CodeAnalysis;

namespace Omasharp.Analyzers;

static class Crossing
{
    public const string Supported = "string, bool, a number type, an enum, string[], double[], int[] or object (a parsed JSON value)";

    public static bool CanComeFromQml(ITypeSymbol t)
    {
        if (t is INamedTypeSymbol { OriginalDefinition.SpecialType: SpecialType.System_Nullable_T } n) t = n.TypeArguments[0];
        switch (t.SpecialType)
        {
            case SpecialType.System_String:
            case SpecialType.System_Boolean:
            case SpecialType.System_Object:
            case SpecialType.System_Int32:
            case SpecialType.System_Int64:
            case SpecialType.System_Int16:
            case SpecialType.System_UInt16:
            case SpecialType.System_Byte:
            case SpecialType.System_SByte:
            case SpecialType.System_UInt32:
            case SpecialType.System_UInt64:
            case SpecialType.System_Double:
            case SpecialType.System_Single:
            case SpecialType.System_Decimal:
                return true;
        }
        if (t.TypeKind == TypeKind.Enum) return true;
        return t is IArrayTypeSymbol { Rank: 1, ElementType.SpecialType: SpecialType.System_String or SpecialType.System_Double or SpecialType.System_Int32 };
    }

    public static string? CantGoToQml(ITypeSymbol t)
    {
        if (t is INamedTypeSymbol { IsGenericType: true } task && task.ConstructedFrom.ToDisplayString() == "System.Threading.Tasks.Task<TResult>")
            t = task.TypeArguments[0];
        if (t is not INamedTypeSymbol n) return null;
        var name = n.ConstructedFrom.ToDisplayString();
        if (name.StartsWith("System.Threading.Tasks.ValueTask", System.StringComparison.Ordinal))
            return "the bridge awaits Task and Task<T> only, so QML would get the task object itself; return Task or Task<T> (QML gets a Promise)";
        if (name == "System.Collections.Generic.IAsyncEnumerable<T>")
            return "QML can't iterate it; collect the items into a List<T>, or add them to a list property as they arrive and Notify";
        if (n.IsTupleType || name.StartsWith("System.Tuple<", System.StringComparison.Ordinal))
            return "QML would get a string like \"(1, 2)\"; return a record instead (QML gets { name: ..., ... })";
        return null;
    }
}
