# Multi-Tenant Dynamic Data API

A take-home coding challenge submission. ASP.NET Core 8 Web API that lets a single endpoint serve
dynamic data from any table in any tenant's database — with no code changes required to switch tenants.

---

## Prerequisites

| Tool | Version |
|---|---|
| [.NET SDK](https://dotnet.microsoft.com/download) | 8.0+ |
| No database server required | SQLite is bundled |

---

## How to Run

```bash
# Clone / unzip, then from the solution root:
cd multi_tenant_api

# Run the API (seeds all sample data on first start)
dotnet run --project MultiTenantApi

# The API listens on http://localhost:5000 by default.
# Open http://localhost:5000 in a browser — the frontend loads automatically.
```

### Run Tests

```bash
dotnet test
# Expected: 38 tests, 0 failures
```

---

## Endpoint Reference

### Data Endpoint — Problems 1–3

```
GET /{tenantId}/data/{tableName}
    ?page=1
    &pageSize=20          (default 20, capped at 500)
    &expandForeignKeys=false
```

**Response shape** (columnar — column keys appear once, rows are parallel arrays):

```json
{
  "Columns": [
    { "Key": "UDF1", "Label": "Widgets in Stock", "DataType": "Integer" },
    { "Key": "UDF2", "Label": "Product Name",     "DataType": "String"  }
  ],
  "Rows": [
    [42, "Widget Alpha"],
    [17, "Widget Beta"]
  ],
  "Pagination": { "Page": 1, "PageSize": 20, "TotalRows": 20, "TotalPages": 1 }
}
```

### Export Endpoints — Problem 4

```
GET /{tenantId}/data/{tableName}/export/csv
GET /{tenantId}/data/{tableName}/export/excel
    ?page=1&pageSize=500   (omit to export all rows, capped at 100 000)
```

Both return a file download with `Content-Disposition: attachment`.

### Helper Endpoints (for the UI)

```
GET /api/tenants          — list all tenants
GET /{tenantId}/tables    — list all tables in a tenant's database
```

---

## Sample Requests (curl)

```bash
# List tenants
curl http://localhost:5000/api/tenants

# Query Acme Products — page 1
curl "http://localhost:5000/acme/data/Products?page=1&pageSize=5"

# Same endpoint with FK expansion
curl "http://localhost:5000/acme/data/Products?expandForeignKeys=true"

# Swap to Globex (different database, zero code changes)
curl "http://localhost:5000/globex/data/Inventory"

# Download CSV
curl -OJ "http://localhost:5000/acme/data/Products/export/csv"

# Download Excel
curl -OJ "http://localhost:5000/acme/data/Products/export/excel"
```

---

## Seeded Sample Data

On first run (Development environment) the seeder creates:

| File | Tenant ID | Tables |
|---|---|---|
| `data/tenant_acme.db` | `acme` | `Products` (20 rows), `Categories` (5 rows) |
| `data/tenant_globex.db` | `globex` | `Inventory` (20 rows), `Warehouses` (4 rows) |
| `data/metadata.db` | — | Tenant registry + column mappings |

---

## Project Structure

```
multi_tenant_api/
├── MultiTenantApi/              Main API project
│   ├── Controllers/             DataController, ExportController
│   ├── Data/                    MetadataDbContext (EF Core)
│   ├── Middleware/              ErrorHandlingMiddleware
│   ├── Models/                  Tenant, ColumnMapping, DataResponse, etc.
│   ├── Seed/                    DatabaseSeeder (dev only, idempotent)
│   ├── Services/                All business logic behind interfaces
│   ├── wwwroot/index.html       Minimal front-end (vanilla HTML + Fetch)
│   └── Program.cs               DI wiring + Serilog bootstrap
├── MultiTenantApi.Tests/        xUnit test project (38 tests)
│   ├── Services/                TenantService, DynamicQuery, ColumnMapping, Export
│   └── Controllers/             DataController, ExportController
├── data/                        SQLite files (auto-created on first run)
├── logs/                        Serilog daily rolling files
└── MultiTenantApi.sln
```
