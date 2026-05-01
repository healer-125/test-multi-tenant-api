namespace MultiTenantApi.Models;

public class ColumnMapping
{
    public int Id { get; set; }
    public string TenantId { get; set; } = string.Empty;
    public string TableName { get; set; } = string.Empty;
    public string ColumnName { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public ColumnDataType DataType { get; set; } = ColumnDataType.String;

    public Tenant? Tenant { get; set; }
}
