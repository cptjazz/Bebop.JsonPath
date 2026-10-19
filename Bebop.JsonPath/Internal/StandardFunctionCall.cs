namespace Bebop.JsonPath.Internal;

/// <summary>
/// Standard function call without pre-compiled data.
/// </summary>
internal sealed record StandardFunctionCall(string Name, IFunctionArgument[] Arguments) : FunctionCall(Name, Arguments);
