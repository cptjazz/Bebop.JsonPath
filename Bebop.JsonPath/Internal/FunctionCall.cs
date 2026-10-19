namespace Bebop.JsonPath.Internal;

internal abstract record FunctionCall(string Name, IFunctionArgument[] Arguments);
