using System.Text.Json;
using System.Text.RegularExpressions;

namespace Bebop.JsonPath.Internal;

/// <summary>
/// Base for regex based functions (match, search) with an optional pre-compiled regex.
/// </summary>
internal abstract record RegexFunctionCall(
    string Name,
    IReadOnlyList<IFunctionArgument> Arguments,
    Regex? CompiledRegex,
    bool Anchored) : FunctionCall(Name, Arguments)
{
    internal override bool Evaluate(SelectorContext selectorContext)
    {
        var input = GetValue(Arguments[0], selectorContext);
        if (input is not { ValueKind: JsonValueKind.String } inputValue)
            return false;

        Regex? regex;
        if (CompiledRegex is not null)
        {
            regex = CompiledRegex;
        }
        else if (Arguments[1] is LiteralArgument)
        {
            return false; // pattern was invalid at parse time
        }
        else
        {
            var pattern = GetValue(Arguments[1], selectorContext);
            if (pattern is not { ValueKind: JsonValueKind.String } patternValue)
                return false;

            var converted = RegexpHelper.ConvertIRegexp(patternValue.GetString()!);
            regex = RegexpHelper.TryGetCachedRegex(Anchored ? $"^(?:{converted})$" : converted);
            if (regex is null)
                return false;
        }

        try
        {
            return regex.IsMatch(inputValue.GetString()!);
        }
        catch (RegexMatchTimeoutException)
        {
            return false;
        }
    }
}
