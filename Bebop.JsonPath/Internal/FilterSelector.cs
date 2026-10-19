namespace Bebop.JsonPath.Internal;

internal sealed record FilterSelector(LogicalExpr Expression) : ISelector;
