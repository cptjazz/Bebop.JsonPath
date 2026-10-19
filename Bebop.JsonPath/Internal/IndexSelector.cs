using System.Text.Json;

namespace Bebop.JsonPath.Internal;

internal sealed class IndexSelector(long Index) : ISelector
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
        int length = array.GetArrayLength();

        long index = Index >= 0 ? Index : length + Index;
        if (index >= 0 && index < length)
        {
            results.Add(array[(int)index]);
        }
    }
}
