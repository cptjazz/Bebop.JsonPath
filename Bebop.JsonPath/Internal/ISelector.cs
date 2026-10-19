namespace Bebop.JsonPath.Internal;

internal interface ISelector
{
    void Evaluate(in SelectorContext context, in ResultCollector results) {}
}

