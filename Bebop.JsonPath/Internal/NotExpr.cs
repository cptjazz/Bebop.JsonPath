namespace Bebop.JsonPath.Internal;

internal sealed record NotExpr(LogicalExpr Operand) : LogicalExpr;
