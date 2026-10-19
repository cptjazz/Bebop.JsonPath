using System.Text.Json;

namespace Bebop.JsonPath.Internal;

internal sealed record LiteralArgument(JsonElement? Value) : IFunctionArgument;
