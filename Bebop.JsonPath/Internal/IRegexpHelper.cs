using System.Text;
using System.Text.RegularExpressions;

namespace Bebop.JsonPath.Internal;

/// <summary>
/// Helper class for converting I-Regexp (RFC 9485) patterns to .NET Regex patterns
/// and compiling them.
/// </summary>
internal static class IRegexpHelper
{
    // Cache for I-Regexp to .NET Regex pattern conversion
    private static readonly Dictionary<string, string> _iregexpConversionCache = new();
    private const int MaxIRegexpCacheSize = 100;

    // Cache for compiled regex patterns (used only for dynamic patterns at runtime)
    private static readonly Dictionary<string, Regex?> _regexCache = new();
    private const int MaxRegexCacheSize = 100;

    /// <summary>
    /// Converts an I-Regexp (RFC 9485) pattern to a .NET Regex pattern.
    /// In I-Regexp, <c>.</c> matches any code point except <c>\n</c> and <c>\r</c>,
    /// including supplementary plane characters (surrogate pairs in UTF-16).
    /// </summary>
    public static string ConvertIRegexp(string pattern)
    {
        lock (_iregexpConversionCache)
        {
            if (_iregexpConversionCache.TryGetValue(pattern, out var cached))
                return cached;

            if (_iregexpConversionCache.Count >= MaxIRegexpCacheSize)
                _iregexpConversionCache.Clear();

            var sb = new StringBuilder(pattern.Length * 2);
            bool inCharClass = false;

            for (int i = 0; i < pattern.Length; i++)
            {
                char c = pattern[i];

                if (c == '\\' && i + 1 < pattern.Length)
                {
                    // Escaped character — pass through as-is
                    sb.Append(c);
                    sb.Append(pattern[i + 1]);
                    i++;
                    continue;
                }

                if (c == '[' && !inCharClass)
                {
                    inCharClass = true;
                    sb.Append(c);
                    continue;
                }

                if (c == ']' && inCharClass)
                {
                    inCharClass = false;
                    sb.Append(c);
                    continue;
                }

                if (c == '.' && !inCharClass)
                {
                    // I-Regexp dot: any code point except \n and \r, including surrogates
                    sb.Append("(?:[^\\n\\r\\uD800-\\uDFFF]|[\\uD800-\\uDBFF][\\uDC00-\\uDFFF])");
                    continue;
                }

                sb.Append(c);
            }

            var result = sb.ToString();
            _iregexpConversionCache[pattern] = result;
            return result;
        }
    }

    /// <summary>
    /// Compiles a regex pattern with a timeout. Returns null if compilation fails.
    /// </summary>
    public static Regex? TryCompileRegex(string pattern)
    {
        try
        {
            return new Regex(pattern, RegexOptions.Compiled, TimeSpan.FromSeconds(1));
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Gets a cached compiled regex or compiles and caches a new one.
    /// Used for dynamic patterns at runtime. Returns null if compilation fails.
    /// </summary>
    public static Regex? TryGetCachedRegex(string pattern)
    {
        lock (_regexCache)
        {
            if (_regexCache.TryGetValue(pattern, out var cached))
                return cached;

            if (_regexCache.Count >= MaxRegexCacheSize)
                _regexCache.Clear();

            var regex = TryCompileRegex(pattern);
            _regexCache[pattern] = regex;
            return regex;
        }
    }
}
