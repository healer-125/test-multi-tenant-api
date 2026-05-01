using Microsoft.EntityFrameworkCore;
using MultiTenantApi.Models;

namespace MultiTenantApi.Data;

public class MetadataDbContext : DbContext
{
    public MetadataDbContext(DbContextOptions<MetadataDbContext> options) : base(options) { }

    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<ColumnMapping> ColumnMappings => Set<ColumnMapping>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Tenant>(e =>
        {
            e.HasKey(t => t.Id);
            e.Property(t => t.Id).HasMaxLength(64);
            e.Property(t => t.Name).IsRequired().HasMaxLength(256);
            e.Property(t => t.DatabasePath).IsRequired().HasMaxLength(512);
        });

        modelBuilder.Entity<ColumnMapping>(e =>
        {
            e.HasKey(c => c.Id);
            e.Property(c => c.TenantId).IsRequired().HasMaxLength(64);
            e.Property(c => c.TableName).IsRequired().HasMaxLength(128);
            e.Property(c => c.ColumnName).IsRequired().HasMaxLength(128);
            e.Property(c => c.DisplayName).IsRequired().HasMaxLength(256);
            e.Property(c => c.DataType).HasConversion<string>();

            // Ensure unique mapping per (tenant, table, column)
            e.HasIndex(c => new { c.TenantId, c.TableName, c.ColumnName }).IsUnique();

            e.HasOne(c => c.Tenant)
             .WithMany()
             .HasForeignKey(c => c.TenantId);
        });
    }
}
