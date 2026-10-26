using System.Text.Json;

namespace Bebop.JsonPath.Internal;

internal readonly ref struct SelectorContext
{
    public SelectorContext(in JsonElement current, in JsonElement root)
    {
        Current = ref current;
        Root = ref root;
    }
    public readonly ref readonly JsonElement Current;
    public readonly ref readonly JsonElement Root;
}
