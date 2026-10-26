using System.Diagnostics;
using System.Text.Json;

namespace Bebop.JsonPath.Internal;

internal static class SliceSelectorFactory
{
    public static ISelector Create(long? start, long? end, long? step)
    {
        if (step == 0)
            return new ZeroStepSliceSelector(start, end);
        long st = step ?? 1;
        if (st < 0)
            return new NegativeStepSliceSelector(start, end, st);
        if (start < 0 && end < 0)
            return new NegativeStartEndSliceSelector(start.Value, end.Value, st);
        if (start < 0)
            return new NegativeStartSliceSelector(start.Value, end, st);
        if (end < 0)
            return new NegativeEndSliceSelector(start ?? 0, end.Value, st);
        return new SliceSelector(start, end, step);
    }
}

internal sealed class ZeroStepSliceSelector(long? start, long? end) : ISelector
{
    public void Evaluate(in SelectorContext context, in ResultCollector results)
    {
        // Do nothing, as step is zero.
    }

    public string DebuggerDisplay => $"{start}:{end}:0";
}
