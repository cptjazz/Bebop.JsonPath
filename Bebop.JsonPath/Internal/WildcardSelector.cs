using System.Diagnostics;
using System.Text.Json;

namespace Bebop.JsonPath.Internal;

[DebuggerDisplay("[*]")]
internal sealed record WildcardSelector : ISelector
{
    public string DebuggerDisplay => "*";

    public static readonly WildcardSelector Instance = new();

    private WildcardSelector()
    {
    }

    public void Evaluate(in SelectorContext context, in ResultCollector results)
    {
        if (context.Current.ValueKind == JsonValueKind.Object)
        {
            _EvaluateObj(context, results);
        }
        else if (context.Current.ValueKind == JsonValueKind.Array)
        {
            _EvaluateArr(context, results);
        }
    }

    private static void _EvaluateArr(in SelectorContext context, in ResultCollector results)
    {
        results.Add(context.Current.EnumerateArray());
    }

    private static void _EvaluateObj(in SelectorContext context, in ResultCollector results)
    {
        results.Add(context.Current.EnumerateObject());
    }
}
