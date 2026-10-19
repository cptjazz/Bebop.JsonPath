namespace Bebop.JsonPath.Internal;

internal sealed record FunctionSignature(
    string Name,
    FunctionParamType[] Parameters,
    FunctionResultType ResultType);
