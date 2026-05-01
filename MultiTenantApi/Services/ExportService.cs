using System.Globalization;
using CsvHelper;
using CsvHelper.Configuration;
using MultiTenantApi.Models;

namespace MultiTenantApi.Services;

public class ExportService : IExportService
{
    private readonly IDynamicQueryService _queryService;
    private readonly IColumnMappingService _mappingService;
    private readonly ILogger<ExportService> _logger;

    // When no pagination is specified, export all rows in one shot (capped to 100 000 for safety).
    private const int MaxExportRows = 100_000;

    public ExportService(
        IDynamicQueryService queryService,
        IColumnMappingService mappingService,
        ILogger<ExportService> logger)
    {
        _queryService = queryService;
        _mappingService = mappingService;
        _logger = logger;
    }

    public async Task<byte[]> ExportCsvAsync(string tenantId, string tableName, PaginationParams? pagination = null)
    {
        var (rows, mappings) = await FetchDataAsync(tenantId, tableName, pagination);
        _logger.LogInformation("Exporting CSV for tenant '{Tenant}', table '{Table}', {Count} rows", tenantId, tableName, rows.Count);

        await using var memStream = new MemoryStream();
        await using var writer = new StreamWriter(memStream, leaveOpen: true);

        var config = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            HasHeaderRecord = true,
            NewLine = Environment.NewLine
        };

        await using var csv = new CsvWriter(writer, config);

        if (rows.Count > 0)
        {
            var headers = rows[0].Keys.ToList();

            // Write header row using display names
            foreach (var col in headers)
            {
                var displayName = mappings.TryGetValue(col, out var m) ? m.DisplayName : col;
                csv.WriteField(displayName);
            }
            await csv.NextRecordAsync();

            // Write data rows
            foreach (var row in rows)
            {
                foreach (var col in headers)
                    csv.WriteField(row[col]);
                await csv.NextRecordAsync();
            }
        }

        await writer.FlushAsync();
        return memStream.ToArray();
    }

    public async Task<byte[]> ExportExcelAsync(string tenantId, string tableName, PaginationParams? pagination = null)
    {
        var (rows, mappings) = await FetchDataAsync(tenantId, tableName, pagination);
        _logger.LogInformation("Exporting Excel for tenant '{Tenant}', table '{Table}', {Count} rows", tenantId, tableName, rows.Count);

        using var workbook = new ClosedXML.Excel.XLWorkbook();
        var ws = workbook.Worksheets.Add(tableName.Length > 31 ? tableName[..31] : tableName);

        if (rows.Count > 0)
        {
            var headers = rows[0].Keys.ToList();

            // Bold header row (row 1)
            for (int col = 0; col < headers.Count; col++)
            {
                var colName = headers[col];
                var displayName = mappings.TryGetValue(colName, out var m) ? m.DisplayName : colName;
                var cell = ws.Cell(1, col + 1);
                cell.Value = displayName;
                cell.Style.Font.Bold = true;
                cell.Style.Fill.BackgroundColor = ClosedXML.Excel.XLColor.LightBlue;
            }

            // Data rows
            for (int rowIdx = 0; rowIdx < rows.Count; rowIdx++)
            {
                var row = rows[rowIdx];
                for (int col = 0; col < headers.Count; col++)
                {
                    var colName = headers[col];
                    var rawVal  = row[colName];
                    var dataType = mappings.TryGetValue(colName, out var mapping) ? mapping.DataType : ColumnDataType.String;
                    var cell    = ws.Cell(rowIdx + 2, col + 1);

                    SetCellValue(cell, rawVal, dataType);
                }
            }

            // Auto-fit columns for readability
            ws.Columns().AdjustToContents();
        }

        await using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    // ---------------------------------------------------------------- helpers

    private async Task<(List<IDictionary<string, object?>> Rows, IReadOnlyDictionary<string, ColumnMapping> Mappings)>
        FetchDataAsync(string tenantId, string tableName, PaginationParams? pagination)
    {
        var pageParams = pagination ?? new PaginationParams { Page = 1, PageSize = MaxExportRows };
        var (rawRows, _) = await _queryService.QueryTableAsync(tenantId, tableName, pageParams);
        var rows = rawRows.ToList();
        var mappings = await _mappingService.GetMappingsAsync(tenantId, tableName);
        return (rows, mappings);
    }

    private static void SetCellValue(ClosedXML.Excel.IXLCell cell, object? value, ColumnDataType dataType)
    {
        if (value is null || value is DBNull)
        {
            cell.Value = ClosedXML.Excel.Blank.Value;
            return;
        }

        var str = value.ToString()!;

        switch (dataType)
        {
            case ColumnDataType.Integer when long.TryParse(str, out var l):
                cell.Value = l;
                break;

            case ColumnDataType.Decimal when double.TryParse(str, NumberStyles.Any, CultureInfo.InvariantCulture, out var d):
                cell.Value = d;
                cell.Style.NumberFormat.Format = "#,##0.0000";
                break;

            case ColumnDataType.DateTime when DateTime.TryParse(str, out var dt):
                cell.Value = dt;
                cell.Style.DateFormat.Format = "yyyy-MM-dd";
                break;

            case ColumnDataType.Boolean when bool.TryParse(str, out var b):
                cell.Value = b;
                break;

            default:
                cell.Value = str;
                break;
        }
    }
}
