using MultiTenantApi.Models;

namespace MultiTenantApi.Services;

public interface IExportService
{
    /// <summary>
    /// Generates a CSV byte stream for all rows from the given table (respects pagination if provided).
    /// </summary>
    Task<byte[]> ExportCsvAsync(string tenantId, string tableName, PaginationParams? pagination = null);

    /// <summary>
    /// Generates an Excel (.xlsx) byte stream for all rows from the given table.
    /// </summary>
    Task<byte[]> ExportExcelAsync(string tenantId, string tableName, PaginationParams? pagination = null);
}
