using FluentAssertions;
using MultiTenantApi.Models;
using MultiTenantApi.Services;

namespace MultiTenantApi.Tests.Services;

public class ColumnMappingServiceTests : IDisposable
{
    private readonly MultiTenantApi.Data.MetadataDbContext _ctx;
    private readonly ColumnMappingService _sut;

    public ColumnMappingServiceTests()
    {
        _ctx = TestHelpers.CreateMetadataContext();
        TestHelpers.SeedMetadata(_ctx);
        _sut = new ColumnMappingService(_ctx);
    }

    [Fact]
    public async Task GetDisplayNameAsync_MappedColumn_ReturnsDisplayName()
    {
        var name = await _sut.GetDisplayNameAsync("acme", "Products", "UDF1");
        name.Should().Be("Widgets in Stock");
    }

    [Fact]
    public async Task GetDisplayNameAsync_UnmappedColumn_ReturnsRawColumnName()
    {
        var name = await _sut.GetDisplayNameAsync("acme", "Products", "SomeUnmappedCol");
        name.Should().Be("SomeUnmappedCol");
    }

    [Fact]
    public async Task GetDisplayNameAsync_CaseInsensitive()
    {
        var name = await _sut.GetDisplayNameAsync("acme", "Products", "udf1");
        name.Should().Be("Widgets in Stock");
    }

    [Fact]
    public async Task GetDataTypeAsync_MappedColumn_ReturnsCorrectType()
    {
        var type = await _sut.GetDataTypeAsync("acme", "Products", "UDF3");
        type.Should().Be(ColumnDataType.Decimal);
    }

    [Fact]
    public async Task GetDataTypeAsync_UnmappedColumn_ReturnsString()
    {
        var type = await _sut.GetDataTypeAsync("acme", "Products", "NonExistent");
        type.Should().Be(ColumnDataType.String);
    }

    [Fact]
    public async Task GetMappingsAsync_ReturnsDictionaryIndexedByColumnName()
    {
        var mappings = await _sut.GetMappingsAsync("acme", "Products");

        mappings.Should().ContainKey("UDF1");
        mappings.Should().ContainKey("UDF2");
        mappings.Should().ContainKey("UDF3");
        mappings.Should().ContainKey("UDF4");
    }

    [Fact]
    public async Task GetMappingsAsync_UnknownTable_ReturnsEmpty()
    {
        var mappings = await _sut.GetMappingsAsync("acme", "NonExistentTable");
        mappings.Should().BeEmpty();
    }

    [Fact]
    public async Task GetMappingsAsync_WrongTenant_ReturnsEmpty()
    {
        var mappings = await _sut.GetMappingsAsync("globex", "Products");
        mappings.Should().BeEmpty();
    }

    public void Dispose() => _ctx.Dispose();
}
