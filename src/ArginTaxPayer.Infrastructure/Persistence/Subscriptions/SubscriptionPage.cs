namespace ArginTaxPayer.Infrastructure.Persistence.Subscriptions;

public sealed record SubscriptionPage<T>(T[] Items,int TotalCount,int Page,int PageSize=10);

public static class SubscriptionPaging
{
    public const int PageSize=10;
    public static int Offset(int page)
    {
        if(page<1 || page>int.MaxValue/PageSize)
            throw new ArgumentOutOfRangeException(nameof(page));
        return (page-1)*PageSize;
    }
}
