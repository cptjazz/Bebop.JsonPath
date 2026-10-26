using System.Text.Json;

namespace Bebop.JsonPath.Internal;

[System.Diagnostics.DebuggerDisplay("[{DebuggerDisplay,nq}]")]
internal sealed class SingularIndexSegment : SingularSegment
{
    private readonly int _index;

    internal string DebuggerDisplay => _index.ToString();

    public SingularIndexSegment(int index)
    {
        _index = index;
    }

    public override bool Evaluate(in JsonElement current, out JsonElement result)
    {
        if (current.ValueKind != JsonValueKind.Array)
        {
            result = default;
            return false;
        }

        var length = current.GetArrayLength();

        var i = _index < 0 ? length + _index : _index;
        if (i < 0 || i >= length)
        {
            result = default;
            return false;
        }

        result = current[i];
        return true;
    }

    public override ISelector ToSelector() => IndexSelectorFactory.Create(_index);
}
