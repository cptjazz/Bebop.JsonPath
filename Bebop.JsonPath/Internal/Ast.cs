using System.Text.Json;

namespace Bebop.JsonPath.Internal;

// ── Segments ──────────────────────────────────────────────────────────────────

internal sealed record Segment(ISelector[] Selectors, bool IsDescendant);

// ── Selectors ─────────────────────────────────────────────────────────────────

internal interface ISelector;
internal sealed record NameSelector(string Name) : ISelector;
internal sealed record WildcardSelector : ISelector
{
    public static readonly WildcardSelector Instance = new();

    private WildcardSelector()
    {
    }
}
internal sealed record IndexSelector(long Index) : ISelector;
internal sealed record SliceSelector(long? Start, long? End, long? Step) : ISelector;
internal sealed record FilterSelector(LogicalExpr Expression) : ISelector;

// ── Filter / Logical Expressions ──────────────────────────────────────────────

internal abstract record LogicalExpr;
internal sealed record OrExpr(LogicalExpr[] Operands) : LogicalExpr;
internal sealed record AndExpr(LogicalExpr[] Operands) : LogicalExpr;
internal sealed record NotExpr(LogicalExpr Operand) : LogicalExpr;
internal sealed record ComparisonExpr(Comparable Left, ComparisonOp Op, Comparable Right) : LogicalExpr;
internal sealed record ExistenceExpr(FilterQuery Query) : LogicalExpr;
internal sealed record FunctionTestExpr(FunctionCall Function) : LogicalExpr;

internal enum ComparisonOp { Eq, Ne, Lt, Le, Gt, Ge }

// ── Comparables ───────────────────────────────────────────────────────────────

internal abstract record Comparable;
internal sealed record LiteralComparable(JsonElement? Value) : Comparable;
internal sealed record SingularQueryComparable(SingularQuery Query) : Comparable;
internal sealed record FunctionComparable(FunctionCall Function) : Comparable;

// ── Queries ───────────────────────────────────────────────────────────────────

/// <summary>
/// A query used in existence tests. Can be relative (@-rooted) or absolute ($-rooted).
/// </summary>
internal sealed record FilterQuery(bool IsRelative, Segment[] Segments);

/// <summary>
/// A singular query (guaranteed to produce at most one node).
/// Used in comparisons.
/// </summary>
internal sealed record SingularQuery(bool IsRelative, SingularSegment[] Segments);

internal abstract record SingularSegment;
internal sealed record SingularNameSegment(string Name) : SingularSegment;
internal sealed record SingularIndexSegment(long Index) : SingularSegment;

// ── Function Calls ────────────────────────────────────────────────────────────

internal sealed record FunctionCall(string Name, IFunctionArgument[] Arguments);

internal interface IFunctionArgument;
internal sealed record LiteralArgument(JsonElement? Value) : IFunctionArgument;
internal sealed record FilterQueryArgument(FilterQuery Query) : IFunctionArgument;
internal sealed record LogicalExprArgument(LogicalExpr Expr) : IFunctionArgument;
internal sealed record FunctionCallArgument(FunctionCall Call) : IFunctionArgument;

// ── Function Type System ──────────────────────────────────────────────────────

internal enum FunctionParamType { ValueType, LogicalType, NodesType }
internal enum FunctionResultType { ValueType, LogicalType, NodesType }

internal sealed record FunctionSignature(
    string Name,
    FunctionParamType[] Parameters,
    FunctionResultType ResultType);
