using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using MultiTenantApi.Data;
using MultiTenantApi.Models;

namespace MultiTenantApi.Seed;

/// <summary>
/// Creates and populates the two sample tenant SQLite databases and the metadata store.
/// Runs only in Development; all operations are idempotent.
/// </summary>
public static class DatabaseSeeder
{
    public static async Task SeedAsync(IServiceProvider services, string dataDirectory)
    {
        // ------------------------------------------------------------------
        // 1. Ensure metadata EF schema exists
        // ------------------------------------------------------------------
        using var scope = services.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<MetadataDbContext>();
        await ctx.Database.EnsureCreatedAsync();

        // ------------------------------------------------------------------
        // 2. Seed tenant SQLite files
        // ------------------------------------------------------------------
        await SeedAcmeAsync(dataDirectory);
        await SeedGlobexAsync(dataDirectory);

        // ------------------------------------------------------------------
        // 3. Seed metadata (tenants + column mappings)
        // ------------------------------------------------------------------
        await SeedMetadataAsync(ctx, dataDirectory);
    }

    // ------------------------------------------------------------------ ACME
    private static async Task SeedAcmeAsync(string dataDir)
    {
        var path = Path.Combine(dataDir, "tenant_acme.db");
        await using var conn = new SqliteConnection($"Data Source={path}");
        await conn.OpenAsync();

        await ExecuteAsync(conn, """
            CREATE TABLE IF NOT EXISTS Categories (
                Id   INTEGER PRIMARY KEY AUTOINCREMENT,
                Name TEXT NOT NULL
            )
            """);

        await ExecuteAsync(conn, """
            CREATE TABLE IF NOT EXISTS Products (
                Id         INTEGER PRIMARY KEY AUTOINCREMENT,
                UDF1       INTEGER,
                UDF2       TEXT,
                UDF3       REAL,
                UDF4       TEXT,
                CategoryId INTEGER REFERENCES Categories(Id)
            )
            """);

        // Idempotent: only seed when empty
        var count = await ScalarAsync<long>(conn, "SELECT COUNT(*) FROM Categories");
        if (count == 0)
        {
            var categories = new[] { "Electronics", "Clothing", "Food & Beverage", "Home & Garden", "Sports" };
            foreach (var cat in categories)
                await ExecuteAsync(conn, "INSERT INTO Categories (Name) VALUES (@n)", new { n = cat });

            var rand = new Random(42);
            string[] names = ["Widget Alpha", "Gadget Beta", "Thingamajig Gamma", "Doohickey Delta",
                               "Gizmo Epsilon", "Contraption Zeta", "Device Eta", "Apparatus Theta",
                               "Mechanism Iota", "Contrivance Kappa", "Instrument Lambda", "Appliance Mu",
                               "Tool Nu", "Implement Xi", "Utensil Omicron", "Apparatus Pi",
                               "Machine Rho", "Engine Sigma", "Motor Tau", "Turbine Upsilon"];
            string[] dates = ["2024-01-15", "2024-03-22", "2024-06-10", "2024-08-05",
                               "2024-09-18", "2024-10-30", "2024-11-12", "2024-12-01",
                               "2025-01-07", "2025-02-14", "2025-03-03", "2025-04-20",
                               "2025-05-11", "2025-06-25", "2025-07-08", "2025-08-19",
                               "2025-09-02", "2025-10-17", "2025-11-28", "2025-12-15"];

            for (int i = 0; i < 20; i++)
            {
                await ExecuteAsync(conn,
                    "INSERT INTO Products (UDF1, UDF2, UDF3, UDF4, CategoryId) VALUES (@u1, @u2, @u3, @u4, @cid)",
                    new
                    {
                        u1 = rand.Next(1, 500),
                        u2 = names[i],
                        u3 = Math.Round(rand.NextDouble() * 999 + 1, 2),
                        u4 = dates[i],
                        cid = rand.Next(1, 6)
                    });
            }
        }
    }

    // --------------------------------------------------------------- GLOBEX
    private static async Task SeedGlobexAsync(string dataDir)
    {
        var path = Path.Combine(dataDir, "tenant_globex.db");
        await using var conn = new SqliteConnection($"Data Source={path}");
        await conn.OpenAsync();

        await ExecuteAsync(conn, """
            CREATE TABLE IF NOT EXISTS Inventory (
                Id          INTEGER PRIMARY KEY AUTOINCREMENT,
                SKU         TEXT NOT NULL,
                UDF1        INTEGER,
                UDF2        TEXT,
                UDF3        REAL,
                UDF4        TEXT,
                Quantity    INTEGER NOT NULL DEFAULT 0,
                LastUpdated TEXT NOT NULL
            )
            """);

        await ExecuteAsync(conn, """
            CREATE TABLE IF NOT EXISTS Warehouses (
                Id       INTEGER PRIMARY KEY AUTOINCREMENT,
                Code     TEXT NOT NULL UNIQUE,
                Location TEXT NOT NULL
            )
            """);

        var count = await ScalarAsync<long>(conn, "SELECT COUNT(*) FROM Warehouses");
        if (count == 0)
        {
            string[][] warehouses =
            [
                ["WH-NORTH", "Chicago, IL"],
                ["WH-SOUTH", "Dallas, TX"],
                ["WH-EAST",  "New York, NY"],
                ["WH-WEST",  "Los Angeles, CA"]
            ];
            foreach (var w in warehouses)
                await ExecuteAsync(conn, "INSERT INTO Warehouses (Code, Location) VALUES (@c, @l)",
                    new { c = w[0], l = w[1] });

            var rand = new Random(99);
            string[] skus = ["SKU-1001","SKU-1002","SKU-1003","SKU-1004","SKU-1005",
                              "SKU-2001","SKU-2002","SKU-2003","SKU-2004","SKU-2005",
                              "SKU-3001","SKU-3002","SKU-3003","SKU-3004","SKU-3005",
                              "SKU-4001","SKU-4002","SKU-4003","SKU-4004","SKU-4005"];
            string[] descriptions = ["High-grade steel bolt", "Carbon fibre panel", "Titanium bracket",
                                      "Aluminium chassis", "Copper wire spool", "Plastic housing unit",
                                      "Rubber seal kit", "Silicon wafer pack", "Glass lens array",
                                      "Ceramic filter pad", "Nylon mesh screen", "Polyester film roll",
                                      "Stainless pipe joint", "Brass valve set", "Zinc alloy casing",
                                      "Tungsten filament", "Nickel alloy sheet", "Lead-free solder",
                                      "Gold contact pin", "Silver conductive paste"];

            for (int i = 0; i < 20; i++)
            {
                await ExecuteAsync(conn,
                    "INSERT INTO Inventory (SKU, UDF1, UDF2, UDF3, UDF4, Quantity, LastUpdated) VALUES (@sku,@u1,@u2,@u3,@u4,@qty,@lu)",
                    new
                    {
                        sku = skus[i],
                        u1 = rand.Next(100, 10000),
                        u2 = descriptions[i],
                        u3 = Math.Round(rand.NextDouble() * 499 + 0.5, 4),
                        u4 = rand.Next(0, 2) == 1 ? "true" : "false",
                        qty = rand.Next(0, 1000),
                        lu = $"2025-{rand.Next(1,13):D2}-{rand.Next(1,28):D2}"
                    });
            }
        }
    }

    // ------------------------------------------------------------ METADATA
    private static async Task SeedMetadataAsync(MetadataDbContext ctx, string dataDir)
    {
        if (await ctx.Tenants.AnyAsync()) return;

        var acmePath   = Path.Combine(dataDir, "tenant_acme.db");
        var globexPath = Path.Combine(dataDir, "tenant_globex.db");

        ctx.Tenants.AddRange(
            new Tenant { Id = "acme",   Name = "Acme Corporation",  DatabasePath = acmePath },
            new Tenant { Id = "globex", Name = "Globex Industries",  DatabasePath = globexPath }
        );

        ctx.ColumnMappings.AddRange(
            // --- Acme: Products table ---
            new ColumnMapping { TenantId = "acme", TableName = "Products", ColumnName = "Id",         DisplayName = "Product ID",           DataType = ColumnDataType.Integer  },
            new ColumnMapping { TenantId = "acme", TableName = "Products", ColumnName = "UDF1",       DisplayName = "Widgets in Stock",     DataType = ColumnDataType.Integer  },
            new ColumnMapping { TenantId = "acme", TableName = "Products", ColumnName = "UDF2",       DisplayName = "Product Name",         DataType = ColumnDataType.String   },
            new ColumnMapping { TenantId = "acme", TableName = "Products", ColumnName = "UDF3",       DisplayName = "Unit Price (USD)",     DataType = ColumnDataType.Decimal  },
            new ColumnMapping { TenantId = "acme", TableName = "Products", ColumnName = "UDF4",       DisplayName = "Launch Date",          DataType = ColumnDataType.DateTime },
            new ColumnMapping { TenantId = "acme", TableName = "Products", ColumnName = "CategoryId", DisplayName = "Category",             DataType = ColumnDataType.Integer  },

            // --- Acme: Categories table ---
            new ColumnMapping { TenantId = "acme", TableName = "Categories", ColumnName = "Id",   DisplayName = "Category ID",   DataType = ColumnDataType.Integer },
            new ColumnMapping { TenantId = "acme", TableName = "Categories", ColumnName = "Name", DisplayName = "Category Name", DataType = ColumnDataType.String  },

            // --- Globex: Inventory table ---
            new ColumnMapping { TenantId = "globex", TableName = "Inventory", ColumnName = "Id",          DisplayName = "Inventory ID",       DataType = ColumnDataType.Integer  },
            new ColumnMapping { TenantId = "globex", TableName = "Inventory", ColumnName = "SKU",         DisplayName = "Stock Keeping Unit", DataType = ColumnDataType.String   },
            new ColumnMapping { TenantId = "globex", TableName = "Inventory", ColumnName = "UDF1",        DisplayName = "Reorder Level",      DataType = ColumnDataType.Integer  },
            new ColumnMapping { TenantId = "globex", TableName = "Inventory", ColumnName = "UDF2",        DisplayName = "Item Description",   DataType = ColumnDataType.String   },
            new ColumnMapping { TenantId = "globex", TableName = "Inventory", ColumnName = "UDF3",        DisplayName = "Unit Cost (USD)",    DataType = ColumnDataType.Decimal  },
            new ColumnMapping { TenantId = "globex", TableName = "Inventory", ColumnName = "UDF4",        DisplayName = "Hazardous Material", DataType = ColumnDataType.Boolean  },
            new ColumnMapping { TenantId = "globex", TableName = "Inventory", ColumnName = "Quantity",    DisplayName = "Qty on Hand",        DataType = ColumnDataType.Integer  },
            new ColumnMapping { TenantId = "globex", TableName = "Inventory", ColumnName = "LastUpdated", DisplayName = "Last Stock Check",   DataType = ColumnDataType.DateTime },

            // --- Globex: Warehouses table ---
            new ColumnMapping { TenantId = "globex", TableName = "Warehouses", ColumnName = "Id",       DisplayName = "Warehouse ID",   DataType = ColumnDataType.Integer },
            new ColumnMapping { TenantId = "globex", TableName = "Warehouses", ColumnName = "Code",     DisplayName = "Warehouse Code", DataType = ColumnDataType.String  },
            new ColumnMapping { TenantId = "globex", TableName = "Warehouses", ColumnName = "Location", DisplayName = "Location",       DataType = ColumnDataType.String  }
        );

        await ctx.SaveChangesAsync();
    }

    // ----------------------------------------------------------- Helpers
    private static async Task ExecuteAsync(SqliteConnection conn, string sql, object? param = null)
    {
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        if (param != null)
        {
            foreach (var prop in param.GetType().GetProperties())
                cmd.Parameters.AddWithValue($"@{prop.Name}", prop.GetValue(param) ?? DBNull.Value);
        }
        await cmd.ExecuteNonQueryAsync();
    }

    private static async Task<T> ScalarAsync<T>(SqliteConnection conn, string sql)
    {
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        var result = await cmd.ExecuteScalarAsync();
        return (T)Convert.ChangeType(result!, typeof(T));
    }
}
