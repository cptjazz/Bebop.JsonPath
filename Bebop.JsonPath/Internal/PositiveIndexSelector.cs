using System.Diagnostics;
using System.Text.Json;

namespace Bebop.JsonPath.Internal;

internal static class IndexSelectorFactory
{
    public static ISelector Create(long index)
    {
        if (index >= 0)
        {
            if (index > int.MaxValue)
                // Index is too large to be valid for an array.
                return new EmptyIndexSelector(index);

            return new PositiveIndexSelector((int)index);
        }
        else
        {
            if (index < int.MinValue)
                // Index is too small to be valid for an array.
                return new EmptyIndexSelector(index);

            return new NegativeIndexSelector((int)index);
        }
    }
}

[DebuggerDisplay("[{DebuggerDisplay,nq}]")]
internal class EmptyIndexSelector(long Index) : ISelector
{
    public string DebuggerDisplay => Index.ToString();

    public void Evaluate(in SelectorContext context, in ResultCollector results)
    {
        // No results for out-of-bounds index.
    }
}

[DebuggerDisplay("[{DebuggerDisplay,nq}]")]
internal sealed class PositiveIndexSelector(int Index) : ISelector
{
    public string DebuggerDisplay => Index.ToString();

    public void Evaluate(in SelectorContext context, in ResultCollector results)
    {
        if (context.Current.ValueKind is not JsonValueKind.Array)
            return;
        
        _Evaluate(context, results);
    }

    private void _Evaluate(in SelectorContext context, in ResultCollector results)
    {
        var array = context.Current;
        var length = array.GetArrayLength();

        if (Index >= 0 && Index < length)
        {
            results.Add(array[Index]);
        }
    }
}

[DebuggerDisplay("[{DebuggerDisplay,nq}]")]
internal sealed class NegativeIndexSelector(int Index) : ISelector
{
    public string DebuggerDisplay => Index.ToString();

    public void Evaluate(in SelectorContext context, in ResultCollector results)
    {
        if (context.Current.ValueKind is not JsonValueKind.Array)
            return;
        
        _Evaluate(context, results);
    }

    private void _Evaluate(in SelectorContext context, in ResultCollector results)
    {
        var array = context.Current;
        var length = array.GetArrayLength();

        var index = length + Index;
        if (index >= 0 && index < length)
        {
            results.Add(array[index]);
        }
    }
}
