using System.Globalization;
using System.Text;
using System.Text.Json;

namespace Bebop.JsonPath.Internal;

/// <summary>
/// Recursive descent parser for RFC 9535 JSONPath queries.
/// </summary>
internal ref struct JsonPathParser
{
    private ReadOnlySpan<char> _source;
    private int _pos;

    // I-JSON exact integer range: [-(2^53)+1, (2^53)-1]
    private const long IJsonMin = -9007199254740991L;
    private const long IJsonMax = 9007199254740991L;

    private static readonly Dictionary<string, FunctionSignature> BuiltInFunctions = new()
    {
        ["length"] = new("length", [FunctionParamType.ValueType], FunctionResultType.ValueType),
        ["count"] = new("count", [FunctionParamType.NodesType], FunctionResultType.ValueType),
        ["match"] = new("match", [FunctionParamType.ValueType, FunctionParamType.ValueType], FunctionResultType.LogicalType),
        ["search"] = new("search", [FunctionParamType.ValueType, FunctionParamType.ValueType], FunctionResultType.LogicalType),
        ["value"] = new("value", [FunctionParamType.NodesType], FunctionResultType.ValueType),
    };

    internal JsonPathParser(ReadOnlySpan<char> source)
    {
        _source = source;
        _pos = 0;
    }

    /// <summary>
    /// Parses the full JSONPath query and returns the list of segments.
    /// </summary>
    internal Segment[] Parse()
    {
        if (_source.IsEmpty)
            throw new FormatException("JSONPath query must not be empty.");

        Expect('$');
        var segments = ParseSegments();

        if (_pos < _source.Length)
            throw new FormatException($"Unexpected character '{_source[_pos]}' at position {_pos}.");

        return segments;
    }

    // ── Segments ──────────────────────────────────────────────────────────

    private Segment[] ParseSegments()
    {
        var segments = new List<Segment>();
        while (true)
        {
            // Per RFC 9535 ABNF: segments = *(S segment). Whitespace is only
            // valid when followed by an actual segment (starting with '[' or '.').
            int savedPos = _pos;
            SkipWhitespace();
            if (_pos >= _source.Length || (_source[_pos] != '[' && _source[_pos] != '.'))
            {
                _pos = savedPos;
                break;
            }

            char c = _source[_pos];

            if (c == '[')
            {
                // Child segment: bracketed-selection
                segments.Add(ParseBracketedSelection(isDescendant: false));
            }
            else if (c == '.')
            {
                if (_pos + 1 < _source.Length && _source[_pos + 1] == '.')
                {
                    // Descendant segment
                    _pos += 2; // consume ".."
                    // Per RFC 9535 ABNF, no whitespace allowed between ".." and wildcard/member-name.
                    // Only bracketed-selection allows leading whitespace (handled at '[' parsing).
                    if (_pos < _source.Length && _source[_pos] == '[')
                    {
                        segments.Add(ParseBracketedSelection(isDescendant: true));
                    }
                    else if (_pos < _source.Length && _source[_pos] == '*')
                    {
                        _pos++; // consume '*'
                        segments.Add(new Segment([new WildcardSelector()], true));
                    }
                    else
                    {
                        // member-name-shorthand
                        string name = ParseMemberNameShorthand();
                        segments.Add(new Segment([new NameSelector(name)], true));
                    }
                }
                else
                {
                    // Child segment: dot notation
                    _pos++; // consume '.'
                    if (_pos < _source.Length && _source[_pos] == '*')
                    {
                        _pos++; // consume '*'
                        segments.Add(new Segment([new WildcardSelector()], false));
                    }
                    else
                    {
                        string name = ParseMemberNameShorthand();
                        segments.Add(new Segment([new NameSelector(name)], false));
                    }
                }
            }
            else
            {
                break;
            }
        }
        return segments.ToArray();
    }

    private Segment ParseBracketedSelection(bool isDescendant)
    {
        Expect('[');
        SkipWhitespace();

        var selectors = new List<ISelector>();
        selectors.Add(ParseSelector());

        while (true)
        {
            SkipWhitespace();
            if (_pos < _source.Length && _source[_pos] == ',')
            {
                _pos++; // consume ','
                SkipWhitespace();
                selectors.Add(ParseSelector());
            }
            else
            {
                break;
            }
        }

        SkipWhitespace();
        Expect(']');
        return new Segment(selectors.ToArray(), isDescendant);
    }

    // ── Selectors ─────────────────────────────────────────────────────────

    private ISelector ParseSelector()
    {
        SkipWhitespace();
        if (_pos >= _source.Length)
            throw new FormatException("Unexpected end of query while parsing selector.");

        char c = _source[_pos];

        if (c == '\'')
            return new NameSelector(ParseSingleQuotedString());
        if (c == '"')
            return new NameSelector(ParseDoubleQuotedString());
        if (c == '*')
        {
            _pos++;
            return new WildcardSelector();
        }
        if (c == '?')
        {
            _pos++; // consume '?'
            SkipWhitespace();
            var expr = ParseLogicalExpr();
            return new FilterSelector(expr);
        }
        if (c == ':')
        {
            // Slice with no start
            return ParseSliceSelector(start: null);
        }
        if (c == '-' || char.IsAsciiDigit(c))
        {
            return ParseIndexOrSlice();
        }

        throw new FormatException($"Unexpected character '{c}' at position {_pos} while parsing selector.");
    }

    private ISelector ParseIndexOrSlice()
    {
        long val = ParseInt();

        SkipWhitespace();
        if (_pos < _source.Length && _source[_pos] == ':')
        {
            return ParseSliceSelector(start: val);
        }

        ValidateIJsonRange(val);
        return new IndexSelector(val);
    }

    private SliceSelector ParseSliceSelector(long? start)
    {
        if (start.HasValue)
            ValidateIJsonRange(start.Value);

        // We're at ':' or just parsed start
        Expect(':');
        SkipWhitespace();

        long? end = null;
        if (_pos < _source.Length && (_source[_pos] == '-' || char.IsAsciiDigit(_source[_pos])))
        {
            end = ParseInt();
            if (end.HasValue)
                ValidateIJsonRange(end.Value);
            SkipWhitespace();
        }

        long? step = null;
        if (_pos < _source.Length && _source[_pos] == ':')
        {
            _pos++; // consume ':'
            SkipWhitespace();
            if (_pos < _source.Length && (_source[_pos] == '-' || char.IsAsciiDigit(_source[_pos])))
            {
                step = ParseInt();
                if (step.HasValue)
                    ValidateIJsonRange(step.Value);
            }
        }

        return new SliceSelector(start, end, step);
    }

    // ── Filter / Logical Expressions ──────────────────────────────────────

    private LogicalExpr ParseLogicalExpr()
    {
        return ParseLogicalOrExpr();
    }

    private LogicalExpr ParseLogicalOrExpr()
    {
        var left = ParseLogicalAndExpr();
        var operands = new List<LogicalExpr> { left };

        while (true)
        {
            SkipWhitespace();
            if (_pos + 1 < _source.Length && _source[_pos] == '|' && _source[_pos + 1] == '|')
            {
                _pos += 2;
                SkipWhitespace();
                operands.Add(ParseLogicalAndExpr());
            }
            else
            {
                break;
            }
        }

        return operands.Count == 1 ? operands[0] : new OrExpr(operands.ToArray());
    }

    private LogicalExpr ParseLogicalAndExpr()
    {
        var left = ParseBasicExpr();
        var operands = new List<LogicalExpr> { left };

        while (true)
        {
            SkipWhitespace();
            if (_pos + 1 < _source.Length && _source[_pos] == '&' && _source[_pos + 1] == '&')
            {
                _pos += 2;
                SkipWhitespace();
                operands.Add(ParseBasicExpr());
            }
            else
            {
                break;
            }
        }

        return operands.Count == 1 ? operands[0] : new AndExpr(operands.ToArray());
    }

    private LogicalExpr ParseBasicExpr()
    {
        SkipWhitespace();
        if (_pos >= _source.Length)
            throw new FormatException("Unexpected end of query in filter expression.");

        // Handle NOT
        if (_source[_pos] == '!')
        {
            _pos++; // consume '!'
            SkipWhitespace();

            if (_pos < _source.Length && _source[_pos] == '(')
            {
                // !( ... )
                var inner = ParseParenExpr();
                return new NotExpr(inner);
            }

            // !filter-query or !function-expr
            var testExpr = ParseTestExprInner();
            return new NotExpr(testExpr);
        }

        // Parenthesized expression
        if (_source[_pos] == '(')
        {
            return ParseParenExpr();
        }

        // Now: could be comparison-expr, test-expr (existence or function)
        // We need to look ahead to decide.
        return ParseComparisonOrTestExpr();
    }

    private LogicalExpr ParseParenExpr()
    {
        Expect('(');
        SkipWhitespace();
        var inner = ParseLogicalExpr();
        SkipWhitespace();
        Expect(')');
        return inner;
    }

    private LogicalExpr ParseTestExprInner()
    {
        SkipWhitespace();
        if (_pos >= _source.Length)
            throw new FormatException("Unexpected end of query in test expression.");

        // Could be filter-query (@... or $...) or function-expr (name(...))
        if (_source[_pos] == '@' || _source[_pos] == '$')
        {
            var query = ParseFilterQuery();
            return new ExistenceExpr(query);
        }

        if (IsLowerAlpha(_source[_pos]))
        {
            var func = ParseFunctionExpr();
            ValidateFunctionInTestContext(func);
            return new FunctionTestExpr(func);
        }

        throw new FormatException($"Unexpected character '{_source[_pos]}' at position {_pos} in test expression.");
    }

    private LogicalExpr ParseComparisonOrTestExpr()
    {
        SkipWhitespace();
        if (_pos >= _source.Length)
            throw new FormatException("Unexpected end of query.");

        char c = _source[_pos];

        // If starts with @ or $, could be existence test or comparison
        if (c == '@' || c == '$')
        {
            int savedPos = _pos;

            // Try to parse as a comparable (singular query) for comparison
            if (TryParseSingularQuery(out var sq))
            {
                SkipWhitespace();
                if (_pos < _source.Length && IsComparisonOpStart(_source[_pos]))
                {
                    var op = ParseComparisonOp();
                    SkipWhitespace();
                    var right = ParseComparable();
                    return new ComparisonExpr(new SingularQueryComparable(sq!), op, right);
                }

                // Not a comparison — backtrack and parse as filter-query (existence test)
                _pos = savedPos;
            }
            else
            {
                _pos = savedPos;
            }

            // Parse as existence test (filter-query)
            var query = ParseFilterQuery();
            return new ExistenceExpr(query);
        }

        // Literal or function on left side => must be comparison
        if (IsLowerAlpha(c))
        {
            // Could be function-expr used as comparable, or function-expr as test-expr
            int savedPos = _pos;
            var func = ParseFunctionExpr();

            SkipWhitespace();
            if (_pos < _source.Length && IsComparisonOpStart(_source[_pos]))
            {
                // comparison with function result on left
                ValidateFunctionInComparableContext(func);
                var op = ParseComparisonOp();
                SkipWhitespace();
                var right = ParseComparable();
                return new ComparisonExpr(new FunctionComparable(func), op, right);
            }

            // function used as test expression
            ValidateFunctionInTestContext(func);
            return new FunctionTestExpr(func);
        }

        // Must be a literal on the left side of a comparison
        var leftComparable = ParseComparable();
        SkipWhitespace();
        var compOp = ParseComparisonOp();
        SkipWhitespace();
        var rightComparable = ParseComparable();
        return new ComparisonExpr(leftComparable, compOp, rightComparable);
    }

    private Comparable ParseComparable()
    {
        SkipWhitespace();
        if (_pos >= _source.Length)
            throw new FormatException("Unexpected end of query while parsing comparable.");

        char c = _source[_pos];

        if (c == '@' || c == '$')
        {
            var sq = ParseSingularQueryRequired();
            return new SingularQueryComparable(sq);
        }

        if (IsLowerAlpha(c))
        {
            // Check for literal keywords (true/false/null) before function call
            if (LookaheadIsFunction())
            {
                var func = ParseFunctionExpr();
                ValidateFunctionInComparableContext(func);
                return new FunctionComparable(func);
            }
            // Keyword literal (true/false/null)
            var lit = ParseLiteral();
            return new LiteralComparable(lit);
        }

        // Literal (number, string)
        var literal = ParseLiteral();
        return new LiteralComparable(literal);
    }

    // ── Literals ──────────────────────────────────────────────────────────

    private JsonElement? ParseLiteral()
    {
        SkipWhitespace();
        if (_pos >= _source.Length)
            throw new FormatException("Unexpected end of query while parsing literal.");

        char c = _source[_pos];

        if (c == '\'' || c == '"')
        {
            string s = c == '\'' ? ParseSingleQuotedString() : ParseDoubleQuotedString();
            return JsonDocument.Parse($"\"{EscapeJsonString(s)}\"").RootElement.Clone();
        }

        if (c == '-' || char.IsAsciiDigit(c))
        {
            return ParseNumberLiteral();
        }

        if (TryConsumeKeyword("true"))
            return JsonDocument.Parse("true").RootElement.Clone();
        if (TryConsumeKeyword("false"))
            return JsonDocument.Parse("false").RootElement.Clone();
        if (TryConsumeKeyword("null"))
            return JsonDocument.Parse("null").RootElement.Clone();

        throw new FormatException($"Unexpected character '{c}' at position {_pos} while parsing literal.");
    }

    private JsonElement ParseNumberLiteral()
    {
        int start = _pos;

        // int / "-0"
        if (_pos < _source.Length && _source[_pos] == '-')
            _pos++;

        if (_pos < _source.Length && _source[_pos] == '0')
        {
            _pos++;
        }
        else if (_pos < _source.Length && _source[_pos] >= '1' && _source[_pos] <= '9')
        {
            _pos++;
            while (_pos < _source.Length && char.IsAsciiDigit(_source[_pos]))
                _pos++;
        }
        else
        {
            throw new FormatException($"Invalid number at position {start}.");
        }

        // frac
        if (_pos < _source.Length && _source[_pos] == '.')
        {
            _pos++;
            if (_pos >= _source.Length || !char.IsAsciiDigit(_source[_pos]))
                throw new FormatException($"Invalid fraction in number at position {start}.");
            while (_pos < _source.Length && char.IsAsciiDigit(_source[_pos]))
                _pos++;
        }

        // exp
        if (_pos < _source.Length && (_source[_pos] == 'e' || _source[_pos] == 'E'))
        {
            _pos++;
            if (_pos < _source.Length && (_source[_pos] == '+' || _source[_pos] == '-'))
                _pos++;
            if (_pos >= _source.Length || !char.IsAsciiDigit(_source[_pos]))
                throw new FormatException($"Invalid exponent in number at position {start}.");
            while (_pos < _source.Length && char.IsAsciiDigit(_source[_pos]))
                _pos++;
        }

        string numStr = _source[start.._pos].ToString();
        return JsonDocument.Parse(numStr).RootElement.Clone();
    }

    // ── String Parsing ────────────────────────────────────────────────────

    private string ParseSingleQuotedString()
    {
        Expect('\'');
        var sb = new StringBuilder();
        while (_pos < _source.Length)
        {
            char c = _source[_pos];
            if (c == '\'')
            {
                _pos++;
                return sb.ToString();
            }
            if (c == '\\')
            {
                _pos++;
                sb.Append(ParseEscapeSequence(isSingleQuoted: true));
            }
            else if (IsUnescapedChar(c))
            {
                sb.Append(c);
                _pos++;
            }
            else if (char.IsHighSurrogate(c) && _pos + 1 < _source.Length && char.IsLowSurrogate(_source[_pos + 1]))
            {
                sb.Append(c);
                sb.Append(_source[_pos + 1]);
                _pos += 2;
            }
            else if (c == '"')
            {
                // double quote is allowed unescaped in single-quoted strings
                sb.Append(c);
                _pos++;
            }
            else
            {
                throw new FormatException($"Invalid character U+{(int)c:X4} at position {_pos} in single-quoted string.");
            }
        }
        throw new FormatException("Unterminated single-quoted string.");
    }

    private string ParseDoubleQuotedString()
    {
        Expect('"');
        var sb = new StringBuilder();
        while (_pos < _source.Length)
        {
            char c = _source[_pos];
            if (c == '"')
            {
                _pos++;
                return sb.ToString();
            }
            if (c == '\\')
            {
                _pos++;
                sb.Append(ParseEscapeSequence(isSingleQuoted: false));
            }
            else if (IsUnescapedChar(c))
            {
                sb.Append(c);
                _pos++;
            }
            else if (char.IsHighSurrogate(c) && _pos + 1 < _source.Length && char.IsLowSurrogate(_source[_pos + 1]))
            {
                sb.Append(c);
                sb.Append(_source[_pos + 1]);
                _pos += 2;
            }
            else if (c == '\'')
            {
                // single quote is allowed unescaped in double-quoted strings
                sb.Append(c);
                _pos++;
            }
            else
            {
                throw new FormatException($"Invalid character U+{(int)c:X4} at position {_pos} in double-quoted string.");
            }
        }
        throw new FormatException("Unterminated double-quoted string.");
    }

    private string ParseEscapeSequence(bool isSingleQuoted)
    {
        if (_pos >= _source.Length)
            throw new FormatException("Unterminated escape sequence.");

        char c = _source[_pos];
        _pos++;

        return c switch
        {
            'b' => "\b",
            'f' => "\f",
            'n' => "\n",
            'r' => "\r",
            't' => "\t",
            '/' => "/",
            '\\' => "\\",
            '\'' when isSingleQuoted => "'",
            '"' when !isSingleQuoted => "\"",
            'u' => ParseUnicodeEscape(),
            _ => throw new FormatException($"Invalid escape sequence '\\{c}' at position {_pos - 1}.")
        };
    }

    private string ParseUnicodeEscape()
    {
        // We already consumed \u, now read 4 hex digits
        if (_pos + 4 > _source.Length)
            throw new FormatException("Incomplete unicode escape.");

        string hex = _source[_pos..(_pos + 4)].ToString();
        if (!ushort.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out ushort val))
            throw new FormatException($"Invalid unicode escape '\\u{hex}' at position {_pos}.");
        _pos += 4;

        // Handle surrogate pairs
        if (val >= 0xD800 && val <= 0xDBFF)
        {
            // High surrogate — must be followed by \uXXXX low surrogate
            if (_pos + 6 > _source.Length || _source[_pos] != '\\' || _source[_pos + 1] != 'u')
                throw new FormatException("High surrogate not followed by low surrogate.");
            _pos += 2; // consume \u

            if (_pos + 4 > _source.Length)
                throw new FormatException("Incomplete low surrogate.");

            string lowHex = _source[_pos..(_pos + 4)].ToString();
            if (!ushort.TryParse(lowHex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out ushort lowVal))
                throw new FormatException($"Invalid low surrogate '\\u{lowHex}'.");
            _pos += 4;

            if (lowVal < 0xDC00 || lowVal > 0xDFFF)
                throw new FormatException($"Invalid low surrogate U+{lowVal:X4}.");

            int codePoint = 0x10000 + ((val - 0xD800) << 10) + (lowVal - 0xDC00);
            return char.ConvertFromUtf32(codePoint);
        }

        if (val >= 0xDC00 && val <= 0xDFFF)
            throw new FormatException("Unexpected low surrogate without high surrogate.");

        return ((char)val).ToString();
    }

    // ── Member Name Shorthand ─────────────────────────────────────────────

    private string ParseMemberNameShorthand()
    {
        if (_pos >= _source.Length || !IsNameFirstOrSurrogate(_pos))
            throw new FormatException($"Expected member name at position {_pos}.");

        int start = _pos;
        AdvancePastNameChar();
        while (_pos < _source.Length && IsNameCharOrSurrogate(_pos))
            AdvancePastNameChar();

        return _source[start.._pos].ToString();
    }

    private void AdvancePastNameChar()
    {
        if (_pos < _source.Length && char.IsHighSurrogate(_source[_pos]) && _pos + 1 < _source.Length && char.IsLowSurrogate(_source[_pos + 1]))
            _pos += 2;
        else
            _pos++;
    }

    private bool IsNameFirstOrSurrogate(int pos)
    {
        if (pos >= _source.Length) return false;
        if (IsNameFirst(_source[pos])) return true;
        // Supplementary plane character (surrogate pair) — all supplementary plane code points are valid name-first chars
        return char.IsHighSurrogate(_source[pos]) && pos + 1 < _source.Length && char.IsLowSurrogate(_source[pos + 1]);
    }

    private bool IsNameCharOrSurrogate(int pos)
    {
        if (pos >= _source.Length) return false;
        if (IsNameChar(_source[pos])) return true;
        return char.IsHighSurrogate(_source[pos]) && pos + 1 < _source.Length && char.IsLowSurrogate(_source[pos + 1]);
    }

    // ── Integer Parsing ───────────────────────────────────────────────────

    private long ParseInt()
    {
        if (_pos >= _source.Length)
            throw new FormatException("Unexpected end of query while parsing integer.");

        int start = _pos;
        bool negative = false;

        if (_source[_pos] == '-')
        {
            negative = true;
            _pos++;
        }

        if (_pos >= _source.Length || !char.IsAsciiDigit(_source[_pos]))
            throw new FormatException($"Expected digit at position {_pos}.");

        if (_source[_pos] == '0')
        {
            _pos++;
            // -0 is not a valid int (only valid in number literal)
            if (negative)
                throw new FormatException($"Invalid integer '-0' at position {start}.");
            // No leading zeros: if next is digit, invalid
            if (_pos < _source.Length && char.IsAsciiDigit(_source[_pos]))
                throw new FormatException($"Leading zeros in integer at position {start}.");
            return 0;
        }

        // DIGIT1 *DIGIT
        long val = _source[_pos] - '0';
        _pos++;
        while (_pos < _source.Length && char.IsAsciiDigit(_source[_pos]))
        {
            // Check for overflow before multiplying
            if (val > (long.MaxValue - (_source[_pos] - '0')) / 10)
                throw new FormatException($"Integer overflow at position {start}.");
            val = val * 10 + (_source[_pos] - '0');
            _pos++;
        }

        long result = negative ? -val : val;
        return result;
    }

    // ── Comparison Operators ──────────────────────────────────────────────

    private ComparisonOp ParseComparisonOp()
    {
        if (_pos >= _source.Length)
            throw new FormatException("Expected comparison operator.");

        char c = _source[_pos];
        if (c == '=' && _pos + 1 < _source.Length && _source[_pos + 1] == '=')
        {
            _pos += 2;
            return ComparisonOp.Eq;
        }
        if (c == '!' && _pos + 1 < _source.Length && _source[_pos + 1] == '=')
        {
            _pos += 2;
            return ComparisonOp.Ne;
        }
        if (c == '<' && _pos + 1 < _source.Length && _source[_pos + 1] == '=')
        {
            _pos += 2;
            return ComparisonOp.Le;
        }
        if (c == '>' && _pos + 1 < _source.Length && _source[_pos + 1] == '=')
        {
            _pos += 2;
            return ComparisonOp.Ge;
        }
        if (c == '<')
        {
            _pos++;
            return ComparisonOp.Lt;
        }
        if (c == '>')
        {
            _pos++;
            return ComparisonOp.Gt;
        }

        throw new FormatException($"Expected comparison operator at position {_pos}.");
    }

    // ── Filter Query ──────────────────────────────────────────────────────

    private FilterQuery ParseFilterQuery()
    {
        bool isRelative;
        if (_source[_pos] == '@')
        {
            isRelative = true;
            _pos++;
        }
        else if (_source[_pos] == '$')
        {
            isRelative = false;
            _pos++;
        }
        else
        {
            throw new FormatException($"Expected '@' or '$' at position {_pos}.");
        }

        var segments = ParseSegments();
        return new FilterQuery(isRelative, segments);
    }

    // ── Singular Query ────────────────────────────────────────────────────

    private bool TryParseSingularQuery(out SingularQuery? result)
    {
        int savedPos = _pos;
        result = null;

        bool isRelative;
        if (_source[_pos] == '@') isRelative = true;
        else if (_source[_pos] == '$') isRelative = false;
        else return false;
        _pos++;

        var segments = new List<SingularSegment>();
        while (true)
        {
            SkipWhitespace();
            if (_pos >= _source.Length) break;

            if (_source[_pos] == '[')
            {
                int bracketPos = _pos;
                _pos++; // consume '['
                SkipWhitespace();

                if (_pos < _source.Length && (_source[_pos] == '\'' || _source[_pos] == '"'))
                {
                    // Name selector
                    string name = _source[_pos] == '\''
                        ? ParseSingleQuotedString()
                        : ParseDoubleQuotedString();
                    SkipWhitespace();
                    if (_pos < _source.Length && _source[_pos] == ']')
                    {
                        _pos++; // consume ']'
                        segments.Add(new SingularNameSegment(name));
                        continue;
                    }
                    // Not a simple name segment
                    _pos = savedPos;
                    return false;
                }

                if (_pos < _source.Length && (_source[_pos] == '-' || char.IsAsciiDigit(_source[_pos])))
                {
                    int intStart = _pos;
                    try
                    {
                        long idx = ParseInt();
                        SkipWhitespace();
                        if (_pos < _source.Length && _source[_pos] == ']')
                        {
                            // Check it's not a slice (no colon before ])
                            _pos++; // consume ']'
                            ValidateIJsonRange(idx);
                            segments.Add(new SingularIndexSegment(idx));
                            continue;
                        }
                    }
                    catch
                    {
                        // Not a valid int, backtrack
                    }
                    _pos = savedPos;
                    return false;
                }

                // Not a singular query bracket
                _pos = savedPos;
                return false;
            }
            else if (_source[_pos] == '.')
            {
                if (_pos + 1 < _source.Length && _source[_pos + 1] == '.')
                    break; // descendant segment — not part of singular query

                _pos++; // consume '.'
                if (_pos < _source.Length && IsNameFirst(_source[_pos]))
                {
                    string name = ParseMemberNameShorthand();
                    segments.Add(new SingularNameSegment(name));
                    continue;
                }

                // Not a valid member name shorthand
                _pos = savedPos;
                return false;
            }
            else
            {
                break;
            }
        }

        result = new SingularQuery(isRelative, segments.ToArray());
        return true;
    }

    private SingularQuery ParseSingularQueryRequired()
    {
        if (TryParseSingularQuery(out var sq) && sq is not null)
            return sq;
        throw new FormatException($"Expected singular query at position {_pos}.");
    }

    // ── Function Expressions ──────────────────────────────────────────────

    private FunctionCall ParseFunctionExpr()
    {
        string name = ParseFunctionName();
        Expect('(');
        SkipWhitespace();

        var args = new List<IFunctionArgument>();
        if (_pos < _source.Length && _source[_pos] != ')')
        {
            args.Add(ParseFunctionArgument(name, 0));
            int paramIndex = 1;
            while (true)
            {
                SkipWhitespace();
                if (_pos < _source.Length && _source[_pos] == ',')
                {
                    _pos++; // consume ','
                    SkipWhitespace();
                    args.Add(ParseFunctionArgument(name, paramIndex));
                    paramIndex++;
                }
                else
                {
                    break;
                }
            }
        }

        SkipWhitespace();
        Expect(')');

        var call = new FunctionCall(name, args.ToArray());
        ValidateFunctionCall(call);
        return call;
    }

    private IFunctionArgument ParseFunctionArgument(string funcName, int paramIndex)
    {
        SkipWhitespace();
        if (_pos >= _source.Length)
            throw new FormatException("Unexpected end of query in function argument.");

        // Determine expected parameter type if known
        FunctionParamType? expectedType = null;
        if (BuiltInFunctions.TryGetValue(funcName, out var sig) && paramIndex < sig.Parameters.Length)
            expectedType = sig.Parameters[paramIndex];

        char c = _source[_pos];

        // If the expected type is NodesType and we have @/$, parse as filter-query
        if ((c == '@' || c == '$') && expectedType == FunctionParamType.NodesType)
        {
            var query = ParseFilterQuery();
            return new FilterQueryArgument(query);
        }

        // @/$ could be filter-query or singular-query depending on context
        if (c == '@' || c == '$')
        {
            if (expectedType == FunctionParamType.ValueType)
            {
                // Parse as singular query
                var sq = ParseSingularQueryRequired();
                return new FilterQueryArgument(new FilterQuery(sq.IsRelative,
                    sq.Segments.Select<SingularSegment, Segment>(s => s switch
                    {
                        SingularNameSegment n => new Segment([new NameSelector(n.Name)], false),
                        SingularIndexSegment i => new Segment([new IndexSelector(i.Index)], false),
                        _ => throw new InvalidOperationException()
                    }).ToArray()));
            }

            // Default: parse as filter-query
            var fq = ParseFilterQuery();
            return new FilterQueryArgument(fq);
        }

        // Function call
        if (IsLowerAlpha(c))
        {
            // Could be true/false/null literal or function call
            if (LookaheadIsFunction())
            {
                var func = ParseFunctionExpr();
                return new FunctionCallArgument(func);
            }
            // literal keyword
            var lit = ParseLiteral();
            return new LiteralArgument(lit);
        }

        // Literal (number, string)
        if (c == '\'' || c == '"' || c == '-' || char.IsAsciiDigit(c))
        {
            var lit = ParseLiteral();
            return new LiteralArgument(lit);
        }

        // Logical expression (e.g., 1==1)
        // This is complex — for now handle the literal/query/function cases
        // A logical expression argument would start with !, (, or a comparable
        if (c == '!' || c == '(')
        {
            var expr = ParseLogicalExpr();
            return new LogicalExprArgument(expr);
        }

        throw new FormatException($"Unexpected character '{c}' at position {_pos} in function argument.");
    }

    private string ParseFunctionName()
    {
        if (_pos >= _source.Length || !IsLowerAlpha(_source[_pos]))
            throw new FormatException($"Expected function name at position {_pos}.");

        int start = _pos;
        _pos++;
        while (_pos < _source.Length && IsFunctionNameChar(_source[_pos]))
            _pos++;

        return _source[start.._pos].ToString();
    }

    // ── Validation ────────────────────────────────────────────────────────

    private static void ValidateIJsonRange(long value)
    {
        if (value < IJsonMin || value > IJsonMax)
            throw new FormatException($"Integer {value} is outside the I-JSON exact value range.");
    }

    private void ValidateFunctionCall(FunctionCall call)
    {
        if (!BuiltInFunctions.TryGetValue(call.Name, out var sig))
            throw new FormatException($"Unknown function '{call.Name}'.");

        if (call.Arguments.Length != sig.Parameters.Length)
            throw new FormatException($"Function '{call.Name}' expects {sig.Parameters.Length} argument(s), got {call.Arguments.Length}.");

        for (int i = 0; i < sig.Parameters.Length; i++)
        {
            ValidateArgumentType(call.Arguments[i], sig.Parameters[i], call.Name, i);
        }
    }

    private void ValidateArgumentType(IFunctionArgument arg, FunctionParamType paramType, string funcName, int paramIndex)
    {
        switch (paramType)
        {
            case FunctionParamType.ValueType:
                // Acceptable: literal, singular-query (as FilterQuery), function returning ValueType
                if (arg is LiteralArgument) return;
                if (arg is FilterQueryArgument) return; // singular query will be resolved at eval time
                if (arg is FunctionCallArgument fca)
                {
                    if (BuiltInFunctions.TryGetValue(fca.Call.Name, out var innerSig)
                        && innerSig.ResultType == FunctionResultType.ValueType)
                        return;
                    throw new FormatException($"Argument {paramIndex} of '{funcName}' must be ValueType.");
                }
                if (arg is LogicalExprArgument)
                    throw new FormatException($"Argument {paramIndex} of '{funcName}' must be ValueType, got logical expression.");
                break;

            case FunctionParamType.NodesType:
                // Acceptable: filter-query, function returning NodesType
                if (arg is FilterQueryArgument) return;
                if (arg is FunctionCallArgument fca2)
                {
                    if (BuiltInFunctions.TryGetValue(fca2.Call.Name, out var innerSig2)
                        && innerSig2.ResultType == FunctionResultType.NodesType)
                        return;
                    throw new FormatException($"Argument {paramIndex} of '{funcName}' must be NodesType.");
                }
                throw new FormatException($"Argument {paramIndex} of '{funcName}' must be NodesType.");

            case FunctionParamType.LogicalType:
                // Acceptable: logical-expr, function returning LogicalType or NodesType (converted)
                if (arg is LogicalExprArgument) return;
                if (arg is FilterQueryArgument) return; // NodesType → LogicalType conversion
                if (arg is FunctionCallArgument fca3)
                {
                    if (BuiltInFunctions.TryGetValue(fca3.Call.Name, out var innerSig3)
                        && (innerSig3.ResultType == FunctionResultType.LogicalType
                            || innerSig3.ResultType == FunctionResultType.NodesType))
                        return;
                    throw new FormatException($"Argument {paramIndex} of '{funcName}' must be LogicalType.");
                }
                throw new FormatException($"Argument {paramIndex} of '{funcName}' must be LogicalType.");
        }
    }

    private void ValidateFunctionInTestContext(FunctionCall func)
    {
        if (!BuiltInFunctions.TryGetValue(func.Name, out var sig))
            throw new FormatException($"Unknown function '{func.Name}'.");

        // test-expr: result must be LogicalType or NodesType
        if (sig.ResultType != FunctionResultType.LogicalType && sig.ResultType != FunctionResultType.NodesType)
            throw new FormatException($"Function '{func.Name}' returns {sig.ResultType} and cannot be used in a test expression.");
    }

    private void ValidateFunctionInComparableContext(FunctionCall func)
    {
        if (!BuiltInFunctions.TryGetValue(func.Name, out var sig))
            throw new FormatException($"Unknown function '{func.Name}'.");

        if (sig.ResultType != FunctionResultType.ValueType)
            throw new FormatException($"Function '{func.Name}' returns {sig.ResultType} and cannot be used in a comparison.");
    }

    // ── Helper Methods ────────────────────────────────────────────────────

    private void SkipWhitespace()
    {
        while (_pos < _source.Length && IsBlank(_source[_pos]))
            _pos++;
    }

    private void Expect(char c)
    {
        if (_pos >= _source.Length || _source[_pos] != c)
            throw new FormatException($"Expected '{c}' at position {_pos}.");
        _pos++;
    }

    private bool TryConsumeKeyword(string keyword)
    {
        if (_pos + keyword.Length > _source.Length)
            return false;

        for (int i = 0; i < keyword.Length; i++)
        {
            if (_source[_pos + i] != keyword[i])
                return false;
        }

        // Must not be followed by a name char (to avoid matching "trueX" as "true")
        if (_pos + keyword.Length < _source.Length && IsNameChar(_source[_pos + keyword.Length]))
            return false;

        _pos += keyword.Length;
        return true;
    }

    private bool LookaheadIsFunction()
    {
        // Look ahead to see if this is a function call: name(
        int saved = _pos;
        if (!IsLowerAlpha(_source[_pos])) return false;
        _pos++;
        while (_pos < _source.Length && IsFunctionNameChar(_source[_pos]))
            _pos++;
        SkipWhitespace();
        bool isFunc = _pos < _source.Length && _source[_pos] == '(';
        _pos = saved;
        return isFunc;
    }

    private static bool IsBlank(char c) => c is ' ' or '\t' or '\n' or '\r';

    private static bool IsComparisonOpStart(char c) => c is '=' or '!' or '<' or '>';

    private static bool IsLowerAlpha(char c) => c is >= 'a' and <= 'z';

    private static bool IsFunctionNameChar(char c) => IsLowerAlpha(c) || c == '_' || char.IsAsciiDigit(c);

    private static bool IsNameFirst(char c) =>
        c is (>= 'A' and <= 'Z') or (>= 'a' and <= 'z') or '_'
        || (c >= 0x80 && c <= 0xD7FF) || (c >= 0xE000);

    private static bool IsNameChar(char c) =>
        IsNameFirst(c) || char.IsAsciiDigit(c);

    private static bool IsUnescapedChar(char c) =>
        c is >= '\x20' and <= '\x21'  // space, !
        || c is >= '\x23' and <= '\x26'  // #, $, %, &
        || c is >= '\x28' and <= '\x5B'  // (, ), ..., [
        || c is >= '\x5D' and <= '\uD7FF'
        || c >= '\uE000';

    private static string EscapeJsonString(string s)
    {
        var sb = new StringBuilder(s.Length);
        foreach (char c in s)
        {
            switch (c)
            {
                case '"': sb.Append("\\\""); break;
                case '\\': sb.Append("\\\\"); break;
                case '\b': sb.Append("\\b"); break;
                case '\f': sb.Append("\\f"); break;
                case '\n': sb.Append("\\n"); break;
                case '\r': sb.Append("\\r"); break;
                case '\t': sb.Append("\\t"); break;
                default:
                    if (c < '\x20')
                        sb.Append($"\\u{(int)c:X4}");
                    else
                        sb.Append(c);
                    break;
            }
        }
        return sb.ToString();
    }
}
