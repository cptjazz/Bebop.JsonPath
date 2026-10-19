namespace Bebop.JsonPath.Internal;

/// <summary>
/// Function call for match() with pre-compiled regex.
/// Pattern is anchored (^...$) for full string matching.
/// </summary>
internal sealed record MatchFunctionCall(IFunctionArgument[] Arguments, System.Text.RegularExpressions.Regex? CompiledRegex) 
    : FunctionCall("match", Arguments);
