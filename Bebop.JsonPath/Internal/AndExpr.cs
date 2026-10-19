namespace Bebop.JsonPath.Internal;

internal sealed record AndExpr(LogicalExpr[] Operands) : LogicalExpr;
