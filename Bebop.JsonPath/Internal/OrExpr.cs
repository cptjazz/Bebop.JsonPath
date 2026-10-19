namespace Bebop.JsonPath.Internal;

internal sealed record OrExpr(LogicalExpr[] Operands) : LogicalExpr;
