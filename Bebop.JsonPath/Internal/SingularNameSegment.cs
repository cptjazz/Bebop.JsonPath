using System.Text;
using System.Text.Json;

namespace Bebop.JsonPath.Internal;

[System.Diagnostics.DebuggerDisplay("['{DebuggerDisplay,nq}']")]
internal sealed class SingularNameSegment : SingularSegment
{
    public byte[] NameUtf8;

    internal string DebuggerDisplay => Encoding.UTF8.GetString(NameUtf8);

    public SingularNameSegment(string name)
    {
        NameUtf8 = Encoding.UTF8.GetBytes(name);
    }

    public override bool Evaluate(in JsonElement current, out JsonElement result)
    {
        if (current.ValueKind != JsonValueKind.Object)
        {
            result = default;
            return false;
        }

        return current.TryGetProperty(NameUtf8, out result);
    }

    public override ISelector ToSelector() => new NameSelector(NameUtf8);
}
