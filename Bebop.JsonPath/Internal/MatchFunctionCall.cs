using System.Text.RegularExpressions;

namespace Bebop.JsonPath.Internal;

/// <summary>
/// Function call for match() with optional pre-compiled regex.
/// Pattern is anchored (^...$) for full string matching.
/// </summary>
internal sealed record MatchFunctionCall(IReadOnlyList<IFunctionArgument> Arguments, Regex? CompiledRegex)
    : RegexFunctionCall("match", Arguments, CompiledRegex, true);
