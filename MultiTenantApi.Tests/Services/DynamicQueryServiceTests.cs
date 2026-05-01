using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using MultiTenantApi.Models;
using MultiTenantApi.Services;

namespace MultiTenantApi.Tests.Services;

public class DynamicQueryServiceTests : IDisposable
{
    private readonly string _dbPath;
    private readonly Mock<ITenantService> _tenantMock;
    private readonly DynamicQueryService _sut;

    public DynamicQueryServiceTests()
    {
        _dbPath = TestHelpers.CreateTempProductsDb();

        _tenantMock = new Mock<ITenantService>();
        _tenantMock.Setup(s => s.GetConnectionAsync(It.IsAny<string>()))
            .ReturnsAsync(() =>
            {
                var conn = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={_dbPath}");
                conn.Open();
                return conn;
            });

        _sut = new DynamicQueryService(_tenantMock.Object, NullLogger<DynamicQueryService>.Instance);
    }

    [Fact]
    public async Task QueryTableAsync_ValidTable_ReturnsRows()
    {
        var pagination = new PaginationParams { Page = 1, PageSize = 10 };

        var (rows, total) = await _sut.QueryTableAsync("acme", "Products", pagination);

        rows.Should().HaveCount(3);
        total.Should().Be(3);
    }

    [Fact]
    public async Task QueryTableAsync_Pagination_ReturnsCorrectSlice()
    {
        var pagination = new PaginationParams { Page = 1, PageSize = 2 };

        var (rows, total) = await _sut.QueryTableAsync("acme", "Products", pagination);

        rows.Should().HaveCount(2);
        total.Should().Be(3);
    }

    [Fact]
    public async Task QueryTableAsync_PageTwo_ReturnsRemainingRows()
    {
        var pagination = new PaginationParams { Page = 2, PageSize = 2 };

        var (rows, total) = await _sut.QueryTableAsync("acme", "Products", pagination);

        rows.Should().HaveCount(1);
        total.Should().Be(3);
    }

    [Fact]
    public async Task QueryTableAsync_InvalidTable_ThrowsTableNotFoundException()
    {
        var act = () => _sut.QueryTableAsync("acme", "NonExistentTable", new PaginationParams());

        await act.Should().ThrowAsync<TableNotFoundException>()
            .WithMessage("*NonExistentTable*");
    }

    [Fact]
    public async Task QueryTableAsync_RowsContainExpectedColumns()
    {
        var (rows, _) = await _sut.QueryTableAsync("acme", "Products", new PaginationParams { Page = 1, PageSize = 10 });
        var first = rows.First();

        first.Keys.Should().Contain(new[] { "Id", "UDF1", "UDF2", "UDF3", "UDF4" });
    }

    [Fact]
    public async Task GetTableNamesAsync_ReturnsTables()
    {
        var tables = (await _sut.GetTableNamesAsync("acme")).ToList();

        tables.Should().Contain("Products");
    }

    [Fact]
    public void PaginationParams_PageSizeCappedAt500()
    {
        var p = new PaginationParams { PageSize = 9999 };
        p.PageSize.Should().Be(500);
    }

    public void Dispose()
    {
        // Clear SQLite connection pool before deleting the temp file (required on Windows)
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        if (File.Exists(_dbPath)) File.Delete(_dbPath);
    }
}
