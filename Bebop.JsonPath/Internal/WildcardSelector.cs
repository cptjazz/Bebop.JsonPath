namespace Bebop.JsonPath.Internal;

internal sealed record WildcardSelector : ISelector
{
    public static readonly WildcardSelector Instance = new();

    private WildcardSelector()
    {
    }
}
