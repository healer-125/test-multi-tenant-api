using FluentAssertions;
using MultiTenantApi.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace MultiTenantApi.Tests.Services;

public class TenantServiceTests : IDisposable
{
    private readonly MultiTenantApi.Data.MetadataDbContext _ctx;
    private readonly TenantService _sut;

    public TenantServiceTests()
    {
        _ctx = TestHelpers.CreateMetadataContext();
        TestHelpers.SeedMetadata(_ctx);
        _sut = new TenantService(_ctx, NullLogger<TenantService>.Instance);
    }

    [Fact]
    public async Task GetTenantAsync_KnownId_ReturnsTenant()
    {
        var tenant = await _sut.GetTenantAsync("acme");

        tenant.Should().NotBeNull();
        tenant.Id.Should().Be("acme");
        tenant.Name.Should().Be("Acme Corp");
    }

    [Fact]
    public async Task GetTenantAsync_UnknownId_ThrowsTenantNotFoundException()
    {
        var act = () => _sut.GetTenantAsync("does-not-exist");

        await act.Should().ThrowAsync<TenantNotFoundException>()
            .WithMessage("*does-not-exist*");
    }

    [Fact]
    public async Task GetAllTenantsAsync_ReturnsBothTenants()
    {
        var tenants = (await _sut.GetAllTenantsAsync()).ToList();

        tenants.Should().HaveCount(2);
        tenants.Select(t => t.Id).Should().Contain(new[] { "acme", "globex" });
    }

    [Fact]
    public async Task GetAllTenantsAsync_ReturnsTenantsOrderedByName()
    {
        var tenants = (await _sut.GetAllTenantsAsync()).ToList();
        var names = tenants.Select(t => t.Name).ToList();
        names.Should().BeInAscendingOrder();
    }

    public void Dispose() => _ctx.Dispose();
}
