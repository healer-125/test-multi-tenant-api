using MultiTenantApi.Models;

namespace MultiTenantApi.Services;

public interface IDynamicQueryService
{
    /// <summary>
    /// Returns paginated data from a table in the tenant's database.
    /// Validates that the table exists before querying to prevent injection.
    /// </summary>
    Task<(IEnumerable<IDictionary<string, object?>> Rows, int TotalCount)> QueryTableAsync(
        string tenantId,
        string tableName,
        PaginationParams pagination);

    /// <summary>Returns all user table names in the tenant's database.</summary>
    Task<IEnumerable<string>> GetTableNamesAsync(string tenantId);
}
