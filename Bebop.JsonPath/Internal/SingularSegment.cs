using System.Text.Json;

namespace Bebop.JsonPath.Internal;

internal abstract class SingularSegment
{
    public abstract bool Evaluate(in JsonElement current, out JsonElement result);

    public abstract ISelector ToSelector();
}
