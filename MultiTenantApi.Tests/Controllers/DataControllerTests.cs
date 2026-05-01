using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Moq;
using MultiTenantApi.Controllers;
using MultiTenantApi.Models;
using MultiTenantApi.Services;

namespace MultiTenantApi.Tests.Controllers;

public class DataControllerTests
{
    private readonly Mock<IDynamicQueryService> _queryMock;
    private readonly Mock<IColumnMappingService> _mappingMock;
    private readonly Mock<IForeignKeyService> _fkMock;
    private readonly Mock<ITenantService> _tenantMock;
    private readonly DataController _sut;

    public DataControllerTests()
    {
        _queryMock   = new Mock<IDynamicQueryService>();
        _mappingMock = new Mock<IColumnMappingService>();
        _fkMock      = new Mock<IForeignKeyService>();
        _tenantMock  = new Mock<ITenantService>();

        _sut = new DataController(_queryMock.Object, _mappingMock.Object, _fkMock.Object, _tenantMock.Object);
    }

    [Fact]
    public async Task GetData_ValidTenantAndTable_Returns200WithColumnarShape()
    {
        var rows = new List<IDictionary<string, object?>>
        {
            new Dictionary<string, object?> { ["UDF1"] = 42L, ["UDF2"] = "Widget Alpha" },
            new Dictionary<string, object?> { ["UDF1"] = 17L, ["UDF2"] = "Widget Beta"  }
        };

        _queryMock.Setup(s => s.QueryTableAsync("acme", "Products", It.IsAny<PaginationParams>()))
            .ReturnsAsync((rows, 2));

        _mappingMock.Setup(s => s.GetMappingsAsync("acme", "Products"))
            .ReturnsAsync(new Dictionary<string, ColumnMapping>
            {
                ["UDF1"] = new() { ColumnName = "UDF1", DisplayName = "Widgets in Stock", DataType = ColumnDataType.Integer },
                ["UDF2"] = new() { ColumnName = "UDF2", DisplayName = "Product Name",     DataType = ColumnDataType.String  }
            });

        var result = await _sut.GetData("acme", "Products", new PaginationParams(), expandForeignKeys: false);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var response = ok.Value.Should().BeOfType<DataResponse>().Subject;

        response.Columns.Should().HaveCount(2);
        response.Columns[0].Label.Should().Be("Widgets in Stock");
        response.Columns[0].DataType.Should().Be("Integer");
        response.Rows.Should().HaveCount(2);
        response.Rows[0].Should().HaveCount(2);
        response.Pagination.TotalRows.Should().Be(2);
    }

    [Fact]
    public async Task GetData_EmptyTable_Returns200WithEmptyRowsAndColumns()
    {
        _queryMock.Setup(s => s.QueryTableAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<PaginationParams>()))
            .ReturnsAsync((Enumerable.Empty<IDictionary<string, object?>>(), 0));

        var result = await _sut.GetData("acme", "EmptyTable", new PaginationParams());

        var ok       = result.Should().BeOfType<OkObjectResult>().Subject;
        var response = ok.Value.Should().BeOfType<DataResponse>().Subject;
        response.Rows.Should().BeEmpty();
        response.Pagination.TotalRows.Should().Be(0);
    }

    [Fact]
    public async Task GetData_TotalPagesCalculatedCorrectly()
    {
        var rows = Enumerable.Range(1, 5).Select(i =>
            (IDictionary<string, object?>)new Dictionary<string, object?> { ["Id"] = (long)i }).ToList();

        _queryMock.Setup(s => s.QueryTableAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<PaginationParams>()))
            .ReturnsAsync((rows, 23));     // 23 total rows

        _mappingMock.Setup(s => s.GetMappingsAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(new Dictionary<string, ColumnMapping>());

        var result = await _sut.GetData("acme", "T", new PaginationParams { Page = 1, PageSize = 5 });

        var ok       = result.Should().BeOfType<OkObjectResult>().Subject;
        var response = ok.Value.Should().BeOfType<DataResponse>().Subject;

        response.Pagination.TotalPages.Should().Be(5);   // ceil(23/5)
        response.Pagination.TotalRows.Should().Be(23);
    }

    [Fact]
    public async Task GetData_ExpandForeignKeys_CallsFkService()
    {
        var rows = new List<IDictionary<string, object?>>
        {
            new Dictionary<string, object?> { ["CategoryId"] = 1L }
        };

        _queryMock.Setup(s => s.QueryTableAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<PaginationParams>()))
            .ReturnsAsync(((IEnumerable<IDictionary<string, object?>>)rows, 1));

        _fkMock.Setup(s => s.ExpandForeignKeysAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IEnumerable<IDictionary<string, object?>>>()))
            .ReturnsAsync(rows);

        _mappingMock.Setup(s => s.GetMappingsAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(new Dictionary<string, ColumnMapping>());

        await _sut.GetData("acme", "Products", new PaginationParams(), expandForeignKeys: true);

        _fkMock.Verify(s => s.ExpandForeignKeysAsync("acme", "Products", It.IsAny<IEnumerable<IDictionary<string, object?>>>()), Times.Once);
    }

    [Fact]
    public async Task GetData_UnmappedColumns_FallBackToRawColumnName()
    {
        var rows = new List<IDictionary<string, object?>>
        {
            new Dictionary<string, object?> { ["RawColumn"] = "value" }
        };

        _queryMock.Setup(s => s.QueryTableAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<PaginationParams>()))
            .ReturnsAsync(((IEnumerable<IDictionary<string, object?>>)rows, 1));

        _mappingMock.Setup(s => s.GetMappingsAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(new Dictionary<string, ColumnMapping>());   // no mappings

        var result = await _sut.GetData("acme", "Something", new PaginationParams());

        var ok       = result.Should().BeOfType<OkObjectResult>().Subject;
        var response = ok.Value.Should().BeOfType<DataResponse>().Subject;

        response.Columns[0].Label.Should().Be("RawColumn");
        response.Columns[0].DataType.Should().Be("String");
    }

    [Fact]
    public async Task GetTenants_ReturnsAllTenants()
    {
        _tenantMock.Setup(s => s.GetAllTenantsAsync())
            .ReturnsAsync(new[]
            {
                new Tenant { Id = "acme",   Name = "Acme" },
                new Tenant { Id = "globex", Name = "Globex" }
            });

        var result = await _sut.GetTenants();

        result.Should().BeOfType<OkObjectResult>();
    }
}
