namespace Bebop.JsonPath.Internal;

internal interface ISelector
{
    void Evaluate(in SelectorContext context, in ResultCollector results);

    /// <summary>
    /// JSONPath text of the selector without surrounding brackets, used for debugger display.
    /// </summary>
    string DebuggerDisplay { get; }
}

