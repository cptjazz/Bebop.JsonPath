namespace Bebop.JsonPath.Internal;

internal sealed record SliceSelector(long? Start, long? End, long? Step) : ISelector;
