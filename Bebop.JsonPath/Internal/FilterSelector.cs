using System.Text.Json;

namespace Bebop.JsonPath.Internal;

internal sealed record FilterSelector(LogicalExpr Expression) : ISelector
{
    public void Evaluate(in SelectorContext context, in ResultCollector results)
    {
        if (context.Current.ValueKind is not JsonValueKind.Array)
            return;

        _Evaluate(context, results);
    }

    private void _Evaluate(in SelectorContext context, in ResultCollector results)
    {
        var array = context.Current;
        foreach (var item in array.EnumerateArray())
        {
            if (Expression.Evaluate(new SelectorContext(item, context.Root)))
            {
                results.Add(item);
            }
        }
    }
}
