using System.Text.Json;

namespace Bebop.JsonPath.Internal;

internal interface IFunctionArgument
{
    JsonElement? GetValue(in SelectorContext selectorContext);
}
