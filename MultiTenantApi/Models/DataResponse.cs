namespace MultiTenantApi.Models;

/// <summary>
/// Column metadata returned per request — key, human label, and data type hint for the front-end.
/// </summary>
public class ColumnDescriptor
{
    public string Key { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string DataType { get; set; } = "String";
}

/// <summary>
/// Pagination metadata attached to every data response.
/// </summary>
public class PaginationInfo
{
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalRows { get; set; }
    public int TotalPages { get; set; }
}

/// <summary>
/// Columnar response: column descriptors appear once; each row is a parallel array of values.
/// This reduces redundancy vs. returning an array of objects where every key repeats per row.
/// </summary>
public class DataResponse
{
    public List<ColumnDescriptor> Columns { get; set; } = new();
    public List<List<object?>> Rows { get; set; } = new();
    public PaginationInfo Pagination { get; set; } = new();
}
