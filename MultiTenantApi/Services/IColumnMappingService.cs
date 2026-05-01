using MultiTenantApi.Models;

namespace MultiTenantApi.Services;

public interface IColumnMappingService
{
    /// <summary>
    /// Returns the display label for a column. Falls back to the raw column name when unmapped.
    /// </summary>
    Task<string> GetDisplayNameAsync(string tenantId, string tableName, string columnName);

    /// <summary>
    /// Returns the data type for a column. Falls back to String when unmapped.
    /// </summary>
    Task<ColumnDataType> GetDataTypeAsync(string tenantId, string tableName, string columnName);

    /// <summary>
    /// Returns all mappings for a (tenant, table) pair — used for efficient bulk resolution.
    /// </summary>
    Task<IReadOnlyDictionary<string, ColumnMapping>> GetMappingsAsync(string tenantId, string tableName);
}
