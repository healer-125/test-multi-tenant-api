namespace MultiTenantApi.Models;

public class Tenant
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string DatabasePath { get; set; } = string.Empty;
}
