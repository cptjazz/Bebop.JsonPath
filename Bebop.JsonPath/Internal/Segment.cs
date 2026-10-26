using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace Bebop.JsonPath.Internal;

/// <summary>
/// Segment with multiple selectors - requires iteration.
/// </summary>
[DebuggerDisplay("{DebuggerDisplay,nq}")]
internal sealed class Segment
{
    private readonly bool _isMulti;
    public bool IsDescendant { get; }
    [DebuggerBrowsable(DebuggerBrowsableState.RootHidden)]
    private readonly IReadOnlyList<ISelector> _selectors;

    private string DebuggerDisplay => $"{(IsDescendant ? ".." : "")}[{string.Join(",", _selectors.Select(s => s.DebuggerDisplay))}]";

    /// <summary>
    /// Segment with multiple selectors - requires iteration.
    /// </summary>
    public Segment(IReadOnlyList<ISelector> selectors, bool isMulti, bool isDescendant)
    {
        _selectors = selectors;
        _isMulti = isMulti;
        IsDescendant = isDescendant;
    }

    internal void Evaluate(in SelectorContext context, in ResultCollector results)
    {
        if (_isMulti)
        {
            EvaluateMulti(context, results);
        }
        else
        {
            EvaluateSingle(context, results);
        }
    }

    private void EvaluateSingle(in SelectorContext context, in ResultCollector results)
    {
        _selectors[0].Evaluate(context, results);
    }

    private void EvaluateMulti(in SelectorContext context, in ResultCollector results)
    {
        foreach (var selector in _selectors)
        {
            selector.Evaluate(context, results);
        }
    }
}
