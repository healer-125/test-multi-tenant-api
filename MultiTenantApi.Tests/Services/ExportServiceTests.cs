using System.Text;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using MultiTenantApi.Models;
using MultiTenantApi.Services;

namespace MultiTenantApi.Tests.Services;

public class ExportServiceTests : IDisposable
{
    private readonly string _dbPath;
    private readonly ExportService _sut;
    private readonly MultiTenantApi.Data.MetadataDbContext _metaCtx;

    public ExportServiceTests()
    {
        _dbPath = TestHelpers.CreateTempProductsDb();
        _metaCtx = TestHelpers.CreateMetadataContext();
        TestHelpers.SeedMetadata(_metaCtx, acmePath: _dbPath);

        var tenantMock = new Mock<ITenantService>();
        tenantMock.Setup(s => s.GetConnectionAsync(It.IsAny<string>()))
            .ReturnsAsync(() =>
            {
                var conn = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={_dbPath}");
                conn.Open();
                return conn;
            });

        var queryService   = new DynamicQueryService(tenantMock.Object, NullLogger<DynamicQueryService>.Instance);
        var mappingService = new ColumnMappingService(_metaCtx);

        _sut = new ExportService(queryService, mappingService, NullLogger<ExportService>.Instance);
    }

    // --------------------------------------------------------------------- CSV
    [Fact]
    public async Task ExportCsvAsync_ReturnsByteArray()
    {
        var bytes = await _sut.ExportCsvAsync("acme", "Products");
        bytes.Should().NotBeEmpty();
    }

    [Fact]
    public async Task ExportCsvAsync_HeadersUseDisplayNames()
    {
        var bytes  = await _sut.ExportCsvAsync("acme", "Products");
        var csv    = Encoding.UTF8.GetString(bytes);
        var header = csv.Split('\n')[0];

        header.Should().Contain("Widgets in Stock");
        header.Should().Contain("Product Name");
        header.Should().Contain("Unit Price");
        header.Should().Contain("Launch Date");
    }

    [Fact]
    public async Task ExportCsvAsync_ContainsDataRows()
    {
        var bytes = await _sut.ExportCsvAsync("acme", "Products");
        var csv   = Encoding.UTF8.GetString(bytes);
        var lines = csv.Split('\n').Where(l => l.Length > 0).ToList();

        // 1 header + 3 data rows
        lines.Should().HaveCountGreaterThanOrEqualTo(4);
    }

    [Fact]
    public async Task ExportCsvAsync_WithPagination_ExportsSubset()
    {
        var pagination = new PaginationParams { Page = 1, PageSize = 1 };
        var bytes = await _sut.ExportCsvAsync("acme", "Products", pagination);
        var csv   = Encoding.UTF8.GetString(bytes);
        var lines = csv.Split('\n').Where(l => l.Length > 0).ToList();

        // 1 header + 1 data row
        lines.Should().HaveCount(2);
    }

    // ------------------------------------------------------------------- Excel
    [Fact]
    public async Task ExportExcelAsync_ReturnsByteArray()
    {
        var bytes = await _sut.ExportExcelAsync("acme", "Products");
        bytes.Should().NotBeEmpty();
    }

    [Fact]
    public async Task ExportExcelAsync_HeadersUseDisplayNames()
    {
        var bytes = await _sut.ExportExcelAsync("acme", "Products");

        using var wb = new ClosedXML.Excel.XLWorkbook(new MemoryStream(bytes));
        var ws = wb.Worksheets.First();
        var headerRow = ws.Row(1);

        var headers = Enumerable.Range(1, 10)
            .Select(c => ws.Cell(1, c).Value.ToString())
            .Where(v => !string.IsNullOrEmpty(v))
            .ToList();

        headers.Should().Contain("Widgets in Stock");
        headers.Should().Contain("Product Name");
    }

    [Fact]
    public async Task ExportExcelAsync_HeaderRowIsBold()
    {
        var bytes = await _sut.ExportExcelAsync("acme", "Products");
        using var wb = new ClosedXML.Excel.XLWorkbook(new MemoryStream(bytes));
        var ws = wb.Worksheets.First();
        ws.Cell(1, 1).Style.Font.Bold.Should().BeTrue();
    }

    [Fact]
    public async Task ExportExcelAsync_ContainsDataRows()
    {
        var bytes = await _sut.ExportExcelAsync("acme", "Products");
        using var wb = new ClosedXML.Excel.XLWorkbook(new MemoryStream(bytes));
        var ws = wb.Worksheets.First();

        // Row 1 = header, rows 2+ = data
        var lastRow = ws.LastRowUsed()?.RowNumber() ?? 0;
        lastRow.Should().BeGreaterThanOrEqualTo(4); // header + 3 rows
    }

    public void Dispose()
    {
        _metaCtx.Dispose();
        // Clear SQLite connection pool before deleting the temp file (required on Windows)
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        if (File.Exists(_dbPath)) File.Delete(_dbPath);
    }
}
