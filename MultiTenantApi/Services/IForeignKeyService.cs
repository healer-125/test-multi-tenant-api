using MultiTenantApi.Models;

namespace MultiTenantApi.Services;

public interface IForeignKeyService
{
    /// <summary>
    /// Returns FK definitions for the given table.
    /// </summary>
    Task<IEnumerable<ForeignKeyInfo>> GetForeignKeysAsync(string tenantId, string tableName);

    /// <summary>
    /// For a set of rows, replaces FK integer values with the referenced row's display label.
    /// </summary>
    Task<IEnumerable<IDictionary<string, object?>>> ExpandForeignKeysAsync(
        string tenantId,
        string tableName,
        IEnumerable<IDictionary<string, object?>> rows);
}
