namespace MultiTenantApi.Services;

/// <summary>
/// Thrown when a tenant id does not exist in the metadata store.
/// Maps to HTTP 404 in the error-handling middleware.
/// </summary>
public class TenantNotFoundException : Exception
{
    public string TenantId { get; }

    public TenantNotFoundException(string tenantId)
        : base($"Tenant '{tenantId}' was not found.")
    {
        TenantId = tenantId;
    }
}

/// <summary>
/// Thrown when a requested table does not exist in the tenant's database.
/// Maps to HTTP 404 in the error-handling middleware.
/// </summary>
public class TableNotFoundException : Exception
{
    public string TableName { get; }

    public TableNotFoundException(string tableName)
        : base($"Table '{tableName}' was not found in the tenant database.")
    {
        TableName = tableName;
    }
}
