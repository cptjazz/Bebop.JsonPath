using System.Diagnostics;
using System.Text.Json;

namespace Bebop.JsonPath.Internal;

[DebuggerDisplay("[{DebuggerDisplay,nq}]")]
internal sealed class SliceSelector(long? start, long? end, long? step) : ISelector
{
    private readonly long? _end = end;
    private readonly long _step = step ?? 1;
    private readonly long _start = start ?? 0;

    public string DebuggerDisplay => $"{_start}:{_end}:{_step}";

    public void Evaluate(in SelectorContext context, in ResultCollector results)
    {
        if (context.Current.ValueKind is not JsonValueKind.Array)
            return;

        var array = context.Current;
        int length = array.GetArrayLength();
        long s = Math.Min(_start, length);
        long e = Math.Min(_end ?? length, length);
        for (long i = s; i < e; i += _step)
            results.Add(array[(int)i]);
    }
}

// Positive step, negative start, end is null or non-negative.
[DebuggerDisplay("[{DebuggerDisplay,nq}]")]
internal sealed class NegativeStartSliceSelector(long start, long? end, long step) : ISelector
{
    public string DebuggerDisplay => $"{start}:{end}:{step}";

    public void Evaluate(in SelectorContext context, in ResultCollector results)
    {
        if (context.Current.ValueKind is not JsonValueKind.Array)
            return;

        var array = context.Current;
        int length = array.GetArrayLength();
        long s = Math.Max(0, start + length);
        long e = Math.Min(end ?? length, length);
        for (long i = s; i < e; i += step)
            results.Add(array[(int)i]);
    }
}

// Positive step, non-negative start, negative end.
[DebuggerDisplay("[{DebuggerDisplay,nq}]")]
internal sealed class NegativeEndSliceSelector(long start, long end, long step) : ISelector
{
    public string DebuggerDisplay => $"{start}:{end}:{step}";

    public void Evaluate(in SelectorContext context, in ResultCollector results)
    {
        if (context.Current.ValueKind is not JsonValueKind.Array)
            return;

        var array = context.Current;
        int length = array.GetArrayLength();
        long s = Math.Min(start, length);
        long e = Math.Max(0, end + length);
        for (long i = s; i < e; i += step)
            results.Add(array[(int)i]);
    }
}

// Positive step, negative start and negative end.
[DebuggerDisplay("[{DebuggerDisplay,nq}]")]
internal sealed class NegativeStartEndSliceSelector(long start, long end, long step) : ISelector
{
    public string DebuggerDisplay => $"{start}:{end}:{step}";

    public void Evaluate(in SelectorContext context, in ResultCollector results)
    {
        if (context.Current.ValueKind is not JsonValueKind.Array)
            return;

        var array = context.Current;
        int length = array.GetArrayLength();
        long s = Math.Max(0, start + length);
        long e = Math.Max(0, end + length);
        for (long i = s; i < e; i += step)
            results.Add(array[(int)i]);
    }
}

// RFC 9535 section 2.3.4.2.2: a negative step walks from the upper to the lower bound.
[DebuggerDisplay("[{DebuggerDisplay,nq}]")]
internal sealed class NegativeStepSliceSelector(long? start, long? end, long step) : ISelector
{
    public string DebuggerDisplay => $"{start}:{end}:{step}";

    public void Evaluate(in SelectorContext context, in ResultCollector results)
    {
        if (context.Current.ValueKind is not JsonValueKind.Array)
            return;

        var array = context.Current;
        int length = array.GetArrayLength();
        long s = start is null ? length - 1 : Normalize(start.Value, length);
        long e = end is null ? -1 : Normalize(end.Value, length);
        long upper = Math.Min(Math.Max(s, -1), length - 1);
        long lower = Math.Min(Math.Max(e, -1), length - 1);
        for (long i = upper; lower < i; i += step)
            results.Add(array[(int)i]);
    }

    private static long Normalize(long i, int length) => i >= 0 ? i : length + i;
}
