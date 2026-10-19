namespace Bebop.JsonPath.Internal;

internal sealed record ComparisonExpr(Comparable Left, ComparisonOp Op, Comparable Right) : LogicalExpr;
