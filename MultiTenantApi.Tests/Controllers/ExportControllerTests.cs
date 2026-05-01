using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Moq;
using MultiTenantApi.Controllers;
using MultiTenantApi.Models;
using MultiTenantApi.Services;

namespace MultiTenantApi.Tests.Controllers;

public class ExportControllerTests
{
    private readonly Mock<IExportService> _exportMock;
    private readonly ExportController _sut;

    public ExportControllerTests()
    {
        _exportMock = new Mock<IExportService>();
        _sut = new ExportController(_exportMock.Object);
    }

    [Fact]
    public async Task ExportCsv_Returns200WithCsvContentType()
    {
        _exportMock.Setup(s => s.ExportCsvAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<PaginationParams>()))
            .ReturnsAsync([0x01, 0x02]);

        var result = await _sut.ExportCsv("acme", "Products", null);

        var fileResult = result.Should().BeOfType<FileContentResult>().Subject;
        fileResult.ContentType.Should().Be("text/csv");
    }

    [Fact]
    public async Task ExportCsv_FileNameContainsTenantAndTable()
    {
        _exportMock.Setup(s => s.ExportCsvAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<PaginationParams>()))
            .ReturnsAsync([0x01]);

        var result = await _sut.ExportCsv("acme", "Products", null);

        var fileResult = result.Should().BeOfType<FileContentResult>().Subject;
        fileResult.FileDownloadName.Should().Contain("acme");
        fileResult.FileDownloadName.Should().Contain("Products");
        fileResult.FileDownloadName.Should().EndWith(".csv");
    }

    [Fact]
    public async Task ExportExcel_Returns200WithXlsxContentType()
    {
        _exportMock.Setup(s => s.ExportExcelAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<PaginationParams>()))
            .ReturnsAsync([0x50, 0x4B]);   // PK magic bytes

        var result = await _sut.ExportExcel("acme", "Products", null);

        var fileResult = result.Should().BeOfType<FileContentResult>().Subject;
        fileResult.ContentType.Should().Be("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
    }

    [Fact]
    public async Task ExportExcel_FileNameEndsWithXlsx()
    {
        _exportMock.Setup(s => s.ExportExcelAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<PaginationParams>()))
            .ReturnsAsync([0x50, 0x4B]);

        var result = await _sut.ExportExcel("acme", "Products", null);

        var fileResult = result.Should().BeOfType<FileContentResult>().Subject;
        fileResult.FileDownloadName.Should().EndWith(".xlsx");
    }

    [Fact]
    public async Task ExportCsv_PassesPaginationParamsToService()
    {
        _exportMock.Setup(s => s.ExportCsvAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<PaginationParams?>()))
            .ReturnsAsync([0x01]);

        var pagination = new PaginationParams { Page = 2, PageSize = 25 };
        await _sut.ExportCsv("acme", "Products", pagination);

        _exportMock.Verify(s => s.ExportCsvAsync("acme", "Products",
            It.Is<PaginationParams>(p => p.Page == 2 && p.PageSize == 25)), Times.Once);
    }
}
