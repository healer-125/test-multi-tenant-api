using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using MultiTenantApi.Data;
using MultiTenantApi.Models;

namespace MultiTenantApi.Tests;

/// <summary>
/// Provides a shared in-memory SQLite MetadataDbContext and helpers for unit tests.
/// </summary>
public static class TestHelpers
{
    /// <summary>Creates an in-memory MetadataDbContext and ensures its schema is created.</summary>
    public static MetadataDbContext CreateMetadataContext()
    {
        // Use a named in-memory SQLite connection so the schema persists for the test's lifetime.
        var conn = new SqliteConnection("Data Source=:memory:");
        conn.Open();

        var options = new DbContextOptionsBuilder<MetadataDbContext>()
            .UseSqlite(conn)
            .Options;

        var ctx = new MetadataDbContext(options);
        ctx.Database.EnsureCreated();
        return ctx;
    }

    /// <summary>Seeds the metadata context with two tenants and sample column mappings.</summary>
    public static void SeedMetadata(MetadataDbContext ctx, string acmePath = "acme.db", string globexPath = "globex.db")
    {
        ctx.Tenants.AddRange(
            new Tenant { Id = "acme",   Name = "Acme Corp",   DatabasePath = acmePath   },
            new Tenant { Id = "globex", Name = "Globex Inc",  DatabasePath = globexPath }
        );
        ctx.ColumnMappings.AddRange(
            new ColumnMapping { TenantId = "acme", TableName = "Products", ColumnName = "UDF1", DisplayName = "Widgets in Stock", DataType = ColumnDataType.Integer  },
            new ColumnMapping { TenantId = "acme", TableName = "Products", ColumnName = "UDF2", DisplayName = "Product Name",     DataType = ColumnDataType.String   },
            new ColumnMapping { TenantId = "acme", TableName = "Products", ColumnName = "UDF3", DisplayName = "Unit Price",       DataType = ColumnDataType.Decimal  },
            new ColumnMapping { TenantId = "acme", TableName = "Products", ColumnName = "UDF4", DisplayName = "Launch Date",      DataType = ColumnDataType.DateTime }
        );
        ctx.SaveChanges();
    }

    /// <summary>Creates a physical temp SQLite file with a Products table and sample rows.</summary>
    public static string CreateTempProductsDb()
    {
        var path = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid():N}.db");
        using var conn = new SqliteConnection($"Data Source={path}");
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE Products (Id INTEGER PRIMARY KEY, UDF1 INTEGER, UDF2 TEXT, UDF3 REAL, UDF4 TEXT);
            INSERT INTO Products VALUES (1, 42, 'Widget Alpha', 9.99, '2024-01-01');
            INSERT INTO Products VALUES (2, 17, 'Widget Beta',  4.49, '2024-06-15');
            INSERT INTO Products VALUES (3, 99, 'Widget Gamma', 14.0, '2025-03-10');
            """;
        cmd.ExecuteNonQuery();
        return path;
    }
}
