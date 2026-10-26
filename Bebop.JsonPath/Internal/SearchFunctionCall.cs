using System.Text.RegularExpressions;

namespace Bebop.JsonPath.Internal;

/// <summary>
/// Function call for search() with optional pre-compiled regex.
/// Pattern is unanchored for substring matching.
/// </summary>
internal sealed record SearchFunctionCall(IReadOnlyList<IFunctionArgument> Arguments, Regex? CompiledRegex)
    : RegexFunctionCall("search", Arguments, CompiledRegex, false);
