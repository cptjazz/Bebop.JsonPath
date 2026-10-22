using System.Text.Json;

namespace Bebop.JsonPath.Internal;

internal sealed class NameSelector(string Name) : ISelector
{
    private readonly byte[] _utf8Name = System.Text.Encoding.UTF8.GetBytes(Name);

    public void Evaluate(in SelectorContext context, in ResultCollector results)
    {
        if (context.Current.ValueKind is not JsonValueKind.Object)
            return;
        
        _Evaluate(context, results);
    }

    private void _Evaluate(in SelectorContext context, in ResultCollector results)
    {
        if (context.Current.TryGetProperty(_utf8Name, out var value))
        {
            results.Add(value);
        }
    }
}
