using Microsoft.AspNetCore.Mvc;
using MultiTenantApi.Models;
using MultiTenantApi.Services;

namespace MultiTenantApi.Controllers;

/// <summary>
/// Returns paginated, metadata-enriched data from any table in a tenant's database.
/// Also provides helper endpoints for the front-end to discover tenants and tables.
/// </summary>
[ApiController]
[Route("{tenantId}")]
[Produces("application/json")]
public class DataController : ControllerBase
{
    private readonly IDynamicQueryService _queryService;
    private readonly IColumnMappingService _mappingService;
    private readonly IForeignKeyService _fkService;
    private readonly ITenantService _tenantService;

    public DataController(
        IDynamicQueryService queryService,
        IColumnMappingService mappingService,
        IForeignKeyService fkService,
        ITenantService tenantService)
    {
        _queryService  = queryService;
        _mappingService = mappingService;
        _fkService      = fkService;
        _tenantService  = tenantService;
    }

    /// <summary>
    /// Returns all registered tenants (used by the front-end to populate the tenant selector).
    /// </summary>
    [HttpGet("/api/tenants")]
    public async Task<IActionResult> GetTenants()
    {
        var tenants = await _tenantService.GetAllTenantsAsync();
        return Ok(tenants.Select(t => new { t.Id, t.Name }));
    }

    /// <summary>
    /// Returns all table names in a tenant's database (used by the front-end).
    /// </summary>
    [HttpGet("tables")]
    public async Task<IActionResult> GetTables(string tenantId)
    {
        var tables = await _queryService.GetTableNamesAsync(tenantId);
        return Ok(tables);
    }

    /// <summary>
    /// Returns paginated data from a table with column metadata.
    /// Response uses a columnar shape to minimise payload size: column descriptors appear once
    /// while each row is a parallel value array.
    ///
    /// GET /{tenantId}/data/{tableName}?page=1&amp;pageSize=20&amp;expandForeignKeys=false
    /// </summary>
    [HttpGet("data/{tableName}")]
    public async Task<IActionResult> GetData(
        string tenantId,
        string tableName,
        [FromQuery] PaginationParams pagination,
        [FromQuery] bool expandForeignKeys = false)
    {
        var (rawRows, totalCount) = await _queryService.QueryTableAsync(tenantId, tableName, pagination);
        var rows = rawRows.ToList();

        if (expandForeignKeys && rows.Count > 0)
            rows = (await _fkService.ExpandForeignKeysAsync(tenantId, tableName, rows)).ToList();

        if (rows.Count == 0)
        {
            // Return empty result but with correct column descriptors inferred from mappings if available
            return Ok(new DataResponse
            {
                Columns = [],
                Rows    = [],
                Pagination = new PaginationInfo
                {
                    Page       = pagination.Page,
                    PageSize   = pagination.PageSize,
                    TotalRows  = 0,
                    TotalPages = 0
                }
            });
        }

        var columnKeys = rows[0].Keys.ToList();
        var mappings   = await _mappingService.GetMappingsAsync(tenantId, tableName);

        // Build column descriptors once
        var columns = columnKeys.Select(key =>
        {
            var hasMapping = mappings.TryGetValue(key, out var m);
            return new ColumnDescriptor
            {
                Key      = key,
                Label    = hasMapping ? m!.DisplayName : key,
                DataType = hasMapping ? m!.DataType.ToString() : "String"
            };
        }).ToList();

        // Build parallel row arrays
        var rowArrays = rows.Select(row =>
            columnKeys.Select(k => row.TryGetValue(k, out var v) ? v : null).ToList()
        ).ToList();

        var totalPages = totalCount == 0 ? 0 : (int)Math.Ceiling((double)totalCount / pagination.PageSize);

        return Ok(new DataResponse
        {
            Columns = columns,
            Rows    = rowArrays,
            Pagination = new PaginationInfo
            {
                Page       = pagination.Page,
                PageSize   = pagination.PageSize,
                TotalRows  = totalCount,
                TotalPages = totalPages
            }
        });
    }
}
