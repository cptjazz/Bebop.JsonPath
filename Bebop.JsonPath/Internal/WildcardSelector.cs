using System.Text.Json;

namespace Bebop.JsonPath.Internal;

internal sealed record WildcardSelector : ISelector
{
    public static readonly WildcardSelector Instance = new();

    private WildcardSelector()
    {
    }

    public void Evaluate(in SelectorContext context, ResultCollector results)
    {
        if (context.Current.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in context.Current.EnumerateObject())
            {
                results.Add(property.Value);
            }
        }
        else if (context.Current.ValueKind == JsonValueKind.Array)
        {
            foreach (var element in context.Current.EnumerateArray())
            {
                results.Add(element);
            }
        }
        else
        {
            results.Add(context.Current);
        }
    }
}
