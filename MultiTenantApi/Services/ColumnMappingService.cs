using Microsoft.EntityFrameworkCore;
using MultiTenantApi.Data;
using MultiTenantApi.Models;

namespace MultiTenantApi.Services;

public class ColumnMappingService : IColumnMappingService
{
    private readonly MetadataDbContext _ctx;

    public ColumnMappingService(MetadataDbContext ctx)
    {
        _ctx = ctx;
    }

    public async Task<IReadOnlyDictionary<string, ColumnMapping>> GetMappingsAsync(string tenantId, string tableName)
    {
        var mappings = await _ctx.ColumnMappings
            .Where(m => m.TenantId == tenantId && m.TableName == tableName)
            .ToListAsync();

        return mappings.ToDictionary(m => m.ColumnName, StringComparer.OrdinalIgnoreCase);
    }

    public async Task<string> GetDisplayNameAsync(string tenantId, string tableName, string columnName)
    {
        var mappings = await GetMappingsAsync(tenantId, tableName);
        return mappings.TryGetValue(columnName, out var m) ? m.DisplayName : columnName;
    }

    public async Task<ColumnDataType> GetDataTypeAsync(string tenantId, string tableName, string columnName)
    {
        var mappings = await GetMappingsAsync(tenantId, tableName);
        return mappings.TryGetValue(columnName, out var m) ? m.DataType : ColumnDataType.String;
    }
}
