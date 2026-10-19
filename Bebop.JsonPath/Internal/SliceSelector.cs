using System.Text.Json;

namespace Bebop.JsonPath.Internal;

internal sealed record SliceSelector(long? Start, long? End, long? Step) : ISelector
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
        long start = Start ?? 0;
        long end = End ?? length;
        long step = Step ?? 1;

        if (start < 0)
            start += length;

        if (end < 0)
            end += length;

        start = Math.Max(0, Math.Min(start, length));
        end = Math.Max(0, Math.Min(end, length));
        for (long i = start; i < end; i += step)
        {
            results.Add(array[(int)i]);
        }
    }
}
