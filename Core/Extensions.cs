namespace WlxOverlay.Core;

public static class Extensions
{
#pragma warning disable CS1998
    public static async IAsyncEnumerable<T> AsAsync<T>(this IEnumerable<T> e)
    {
        foreach (var item in e)
            yield return item;
    }
}