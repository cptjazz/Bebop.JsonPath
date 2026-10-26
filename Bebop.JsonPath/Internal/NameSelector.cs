using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace Bebop.JsonPath.Internal;

[DebuggerDisplay("[{DebuggerDisplay,nq}]")]
internal sealed class NameSelector : ISelector
{
    public string DebuggerDisplay => $"'{Encoding.UTF8.GetString(_utf8Name)}'";

    private readonly byte[] _utf8Name;

    public NameSelector(byte[] nameUtf8)
    {
        _utf8Name = nameUtf8;
    }

    public NameSelector(string name)
    {
        _utf8Name = Encoding.UTF8.GetBytes(name);
    }

    public NameSelector(ReadOnlySpan<char> propertyName)
    {
        _utf8Name = new byte[Encoding.UTF8.GetByteCount(propertyName)];
        Encoding.UTF8.GetBytes(propertyName, _utf8Name);
    }

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
