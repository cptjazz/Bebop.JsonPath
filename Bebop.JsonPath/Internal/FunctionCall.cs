namespace Bebop.JsonPath.Internal;

internal abstract record FunctionCall(string Name, IFunctionArgument[] Arguments)
{
    internal bool Evaluate(SelectorContext selectorContext)
    {
        //return FunctionRegistry.TryGetFunction(Name, out var function)
        //    && function.Evaluate(selectorContext, Arguments);
        return false;
    }
}
