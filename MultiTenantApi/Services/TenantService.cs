using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using MultiTenantApi.Data;
using MultiTenantApi.Models;

namespace MultiTenantApi.Services;

public class TenantService : ITenantService
{
    private readonly MetadataDbContext _ctx;
    private readonly ILogger<TenantService> _logger;

    public TenantService(MetadataDbContext ctx, ILogger<TenantService> logger)
    {
        _ctx = ctx;
        _logger = logger;
    }

    public async Task<Tenant> GetTenantAsync(string tenantId)
    {
        var tenant = await _ctx.Tenants.FindAsync(tenantId);
        if (tenant is null)
        {
            _logger.LogWarning("Tenant '{TenantId}' not found", tenantId);
            throw new TenantNotFoundException(tenantId);
        }
        return tenant;
    }

    public async Task<SqliteConnection> GetConnectionAsync(string tenantId)
    {
        var tenant = await GetTenantAsync(tenantId);
        _logger.LogDebug("Opening SQLite connection for tenant '{TenantId}' at '{Path}'", tenantId, tenant.DatabasePath);
        var conn = new SqliteConnection($"Data Source={tenant.DatabasePath}");
        await conn.OpenAsync();
        return conn;
    }

    public async Task<IEnumerable<Tenant>> GetAllTenantsAsync()
        => await _ctx.Tenants.OrderBy(t => t.Name).ToListAsync();
}
