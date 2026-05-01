using Microsoft.AspNetCore.Mvc;
using MultiTenantApi.Models;
using MultiTenantApi.Services;

namespace MultiTenantApi.Controllers;

/// <summary>
/// Nested export endpoints under the same resource path as the data endpoint.
/// GET /{tenantId}/data/{tableName}/export/csv
/// GET /{tenantId}/data/{tableName}/export/excel
/// </summary>
[ApiController]
[Route("{tenantId}/data/{tableName}/export")]
public class ExportController : ControllerBase
{
    private readonly IExportService _exportService;

    public ExportController(IExportService exportService)
    {
        _exportService = exportService;
    }

    /// <summary>
    /// Downloads table data as a CSV file.
    /// Omit page/pageSize to export all rows (capped at 100 000).
    /// </summary>
    [HttpGet("csv")]
    public async Task<IActionResult> ExportCsv(
        string tenantId,
        string tableName,
        [FromQuery] PaginationParams? pagination)
    {
        var bytes = await _exportService.ExportCsvAsync(tenantId, tableName, pagination);
        var filename = $"{tenantId}_{tableName}_{DateTime.UtcNow:yyyyMMdd}.csv";

        return File(bytes, "text/csv", filename);
    }

    /// <summary>
    /// Downloads table data as an Excel (.xlsx) file with typed cells and bold headers.
    /// Omit page/pageSize to export all rows (capped at 100 000).
    /// </summary>
    [HttpGet("excel")]
    public async Task<IActionResult> ExportExcel(
        string tenantId,
        string tableName,
        [FromQuery] PaginationParams? pagination)
    {
        var bytes = await _exportService.ExportExcelAsync(tenantId, tableName, pagination);
        var filename = $"{tenantId}_{tableName}_{DateTime.UtcNow:yyyyMMdd}.xlsx";

        return File(
            bytes,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            filename);
    }
}
