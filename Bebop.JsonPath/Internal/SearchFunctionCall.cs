namespace Bebop.JsonPath.Internal;

/// <summary>
/// Function call for search() with pre-compiled regex.
/// Pattern is unanchored for substring matching.
/// </summary>
internal sealed record SearchFunctionCall(IFunctionArgument[] Arguments, System.Text.RegularExpressions.Regex? CompiledRegex) 
    : FunctionCall("search", Arguments);
