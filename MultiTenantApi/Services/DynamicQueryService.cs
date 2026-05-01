using Dapper;
using MultiTenantApi.Models;

namespace MultiTenantApi.Services;

public class DynamicQueryService : IDynamicQueryService
{
    private readonly ITenantService _tenantService;
    private readonly ILogger<DynamicQueryService> _logger;

    public DynamicQueryService(ITenantService tenantService, ILogger<DynamicQueryService> logger)
    {
        _tenantService = tenantService;
        _logger = logger;
    }

    public async Task<IEnumerable<string>> GetTableNamesAsync(string tenantId)
    {
        await using var conn = await _tenantService.GetConnectionAsync(tenantId);
        var tables = await conn.QueryAsync<string>(
            "SELECT name FROM sqlite_master WHERE type = 'table' AND name NOT LIKE 'sqlite_%' ORDER BY name");
        return tables;
    }

    public async Task<(IEnumerable<IDictionary<string, object?>> Rows, int TotalCount)> QueryTableAsync(
        string tenantId,
        string tableName,
        PaginationParams pagination)
    {
        await using var conn = await _tenantService.GetConnectionAsync(tenantId);

        // Security: validate table name against the schema before interpolating into SQL.
        // Table names cannot be passed as SQL parameters, so a whitelist check is required.
        var validTables = (await conn.QueryAsync<string>(
            "SELECT name FROM sqlite_master WHERE type = 'table' AND name NOT LIKE 'sqlite_%'")).ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (!validTables.Contains(tableName))
            throw new TableNotFoundException(tableName);

        var sw = System.Diagnostics.Stopwatch.StartNew();

        var totalCount = await conn.ExecuteScalarAsync<int>($"SELECT COUNT(*) FROM \"{tableName}\"");

        // Dapper returns dynamic rows as IDictionary<string,object> which we cast to typed dict
        var rawRows = await conn.QueryAsync(
            $"SELECT * FROM \"{tableName}\" LIMIT @PageSize OFFSET @Offset",
            new { pagination.PageSize, pagination.Offset });

        var rows = rawRows.Select(r => (IDictionary<string, object?>)r).ToList();

        sw.Stop();
        _logger.LogInformation(
            "Queried table '{Table}' for tenant '{Tenant}': {Count}/{Total} rows in {Ms}ms",
            tableName, tenantId, rows.Count, totalCount, sw.ElapsedMilliseconds);

        return (rows, totalCount);
    }
}
