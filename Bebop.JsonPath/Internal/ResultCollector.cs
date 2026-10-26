using System.Runtime.CompilerServices;
using System.Text.Json;

namespace Bebop.JsonPath.Internal;

internal sealed class CountHelper
{
    private const int InitialCapacity = 8;

    private int _count;
    private JsonElement[] _arr = [];
    private JsonElement[] _other = [];

    // Swaps buffers so results of the previous segment (still being read as the node set)
    // are not overwritten by results of the current segment.
    public void Reset()
    {
        (_arr, _other) = (_other, _arr);
        _count = 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void EnsureCapacity(int c)
    {
        if (_arr.Length >= c)
            return;

        _EnsureCapacitySlow(c);
    }

    private void _EnsureCapacitySlow(int c)
    {
        Array.Resize(ref _arr, Math.Max(c, Math.Max(InitialCapacity, _arr.Length * 2)));
    }

    public void Add(in JsonElement e)
    {
        EnsureCapacity(_count + 1);
        _arr[_count++] = e;
    }

    public ReadOnlySpan<JsonElement> Slice(int c) => _arr.AsSpan(0, c);

    public static implicit operator int(CountHelper count) => count._count;

    public void AddList(ReadOnlySpan<JsonElement> values)
    {
        EnsureCapacity(_count + values.Length);
        values.CopyTo(_arr.AsSpan(_count));
    }

    public void AddList(IEnumerable<JsonElement> values)
    {
        foreach (var v in values)
        {
            EnsureCapacity(_count + 1);
            _arr[_count++] = v;
        }
    }

    public void AddList(IEnumerable<JsonElement> values, int count)
    {
        EnsureCapacity(_count + count);

        foreach (var v in values)
        {
            _arr[_count++] = v;
        }
    }

    public void AddList(IEnumerable<JsonProperty> values)
    {
        foreach (var v in values)
        {
            EnsureCapacity(_count + 1);
            _arr[_count++] = v.Value;
        }
    }

    public void AddList(IEnumerable<JsonProperty> values, int count)
    {
        EnsureCapacity(_count + count);

        foreach (var v in values)
        {
            _arr[_count++] = v.Value;
        }
    }
}

internal readonly ref struct ResultCollector
{
    private readonly CountHelper _helper;

    public ResultCollector(CountHelper helper)
    {
        _helper = helper;
        _helper.Reset();
    }

    public ReadOnlySpan<JsonElement> Results => _helper.Slice(_helper);

    public void Add(in JsonElement value)
    {
        _helper.Add(value);
    }

    public void Add(ReadOnlySpan<JsonElement> values)
    {
        _helper.AddList(values);
    }

    public void Add(IEnumerable<JsonElement> values)
    {
        if (values.TryGetNonEnumeratedCount(out var count))
            _helper.AddList(values, count);
        else
            _helper.AddList(values);
    }

    public void Add(IEnumerable<JsonProperty> values)
    {
        if (values.TryGetNonEnumeratedCount(out var count))
            _helper.AddList(values, count);
        else
            _helper.AddList(values);
    }
}
