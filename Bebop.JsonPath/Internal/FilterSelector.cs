using System.Diagnostics;
using System.Text.Json;

namespace Bebop.JsonPath.Internal;

[DebuggerDisplay("[{DebuggerDisplay,nq}]")]
internal sealed record FilterSelector(LogicalExpr Expression) : ISelector
{
    public string DebuggerDisplay => $"?{Expression}";

    public void Evaluate(in SelectorContext context, in ResultCollector results)
    {
        switch (context.Current.ValueKind)
        {
            case JsonValueKind.Array:
                foreach (var item in context.Current.EnumerateArray())
                    _Evaluate(item, context, results);
                break;
            case JsonValueKind.Object:
                foreach (var property in context.Current.EnumerateObject())
                    _Evaluate(property.Value, context, results);
                break;
        }
    }

    private void _Evaluate(JsonElement item, in SelectorContext context, in ResultCollector results)
    {
        if (Expression.Evaluate(new SelectorContext(item, context.Root)))
        {
            results.Add(item);
        }
    }
}
