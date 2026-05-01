using Microsoft.Data.Sqlite;

namespace MultiTenantApi.Services;

public interface ITenantService
{
    /// <summary>Returns the tenant record for the given id.</summary>
    /// <exception cref="TenantNotFoundException">Thrown when the tenant id is unknown.</exception>
    Task<Models.Tenant> GetTenantAsync(string tenantId);

    /// <summary>Opens and returns a SqliteConnection for the given tenant's database.</summary>
    Task<SqliteConnection> GetConnectionAsync(string tenantId);

    /// <summary>Returns all registered tenants.</summary>
    Task<IEnumerable<Models.Tenant>> GetAllTenantsAsync();
}
