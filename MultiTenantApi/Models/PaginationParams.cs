namespace MultiTenantApi.Models;

public class PaginationParams
{
    private const int MaxPageSize = 500;
    private int _pageSize = 20;

    public int Page { get; set; } = 1;

    public int PageSize
    {
        get => _pageSize;
        set => _pageSize = value > MaxPageSize ? MaxPageSize : (value < 1 ? 1 : value);
    }

    public int Offset => (Page - 1) * PageSize;
}
