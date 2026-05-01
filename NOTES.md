# Notes — Multi-Tenant Dynamic Data API

## AI Tools Used

- **GitHub Copilot** was used throughout: it provided completions for boilerplate (EF model
  configuration, Serilog bootstrap, xUnit test stubs) and suggested the `PRAGMA foreign_key_list`
  approach for FK introspection.
- I reviewed, understood, and own every decision described below. The follow-up interview should
  feel comfortable — I can speak to all of it.

---

## Key Decisions and Trade-offs

### 1. One SQLite file per tenant (not one shared DB with a tenantId column)

**Why:** The problem statement says "swap connection strings to point to a different database" and
describes real customer databases with unique schemas. A single shared file with a `tenantId`
column would be the right call for a multi-tenant SaaS where we control the schema, but here
tenants own their schemas. A per-file model makes the swap-connection-string scenario literal and
testable.

**Trade-off accepted:** Managing many SQLite files is harder to back up and harder to scale beyond
a single server. In production I'd use proper per-tenant SQL Server / Postgres databases; SQLite
was chosen here for zero-setup convenience.

---

### 2. Dapper for tenant queries, EF Core only for the metadata store

**Why:** EF Core requires compile-time model knowledge. We have none for tenant databases — the
whole point is that tables and columns are user-defined. Dapper + raw SQL returns
`IDictionary<string, object?>` which maps naturally to the columnar response shape.

EF Core is kept for the metadata store (`Tenants`, `ColumnMappings`) because that schema is
fixed and EF's migrations + LINQ queries are a productivity win there.

**Rejected:** A pure Dapper setup for everything — EF is genuinely better for the typed metadata
side (change tracking, migrations, LINQ).

---

### 3. Columnar JSON response shape (addresses the "reduce redundant information" bonus)

**Why:** The naive approach returns an array of objects, repeating every column key for every row.
With 100 columns and 10 000 rows that's 100 000 redundant key repetitions. The columnar shape
writes column descriptors once and each row is a parallel value array — the JSON shrinks by
roughly `O(cols × rows × avg_key_length)`.

```json
// Standard (keys repeat per row):
[{ "UDF1": 42, "UDF2": "Alpha" }, { "UDF1": 17, "UDF2": "Beta" }]

// Columnar (keys appear once):
{ "Columns": [{"Key":"UDF1",...}, {"Key":"UDF2",...}], "Rows": [[42,"Alpha"],[17,"Beta"]] }
```

**Trade-off:** The front-end must reconstruct rows by index rather than by name. The bundled
`index.html` shows this is a one-liner: `row.forEach((val, i) => Columns[i].Label)`.

---

### 4. Table name injection prevention via `sqlite_master` whitelist

**Why:** SQL parameters cannot be used for identifiers (table/column names). The standard pattern
is to validate the name against the known-good set before interpolating into SQL. I query
`sqlite_master WHERE type='table'` and check membership before ever constructing a dynamic query.

**Rejected as insufficient:** Regex-based validation of table names — a whitelist from the actual
database is semantically correct and has no false-positive/negative risk.

---

### 5. Nested RESTful export routes

`GET /acme/data/Products/export/csv` vs `GET /acme/export?table=Products&format=csv`

The nested path treats "the CSV export of this table" as a sub-resource, which is more consistent
with REST conventions. It also makes the URL bookmark-friendly and human-readable.

---

### 6. FK expansion is opt-in (`?expandForeignKeys=true`)

**Why:** FK resolution requires extra PRAGMA + SELECT queries per FK column per page. For most use
cases the raw integer ID is fine (or the UI just doesn't care). Making it opt-in keeps the hot
path fast.

---

### 7. No authentication / authorisation

The problem statement says this is out of scope. In a real system each tenant ID in the URL would
be validated against a JWT claim or API key to prevent horizontal privilege escalation.

---

## What I'd Do Differently With More Time

- **Connection pooling per tenant:** Currently each request opens and closes a SQLite connection.
  A `ConcurrentDictionary<string, SqliteConnection>` singleton with careful locking (or a proper
  ephemeral pool) would be faster under load.

- **EF Core migrations properly applied:** Currently `EnsureCreated()` is used — sufficient for a
  demo but not for production where schema evolution matters. I'd add a proper `dotnet ef migrations`
  setup with a migration history table.

- **Caching column mappings:** `ColumnMappingService.GetMappingsAsync` issues a DB query on every
  request. With `IMemoryCache` and a short TTL (say 60 s) this would virtually eliminate metadata
  DB load.

- **Caching table-name whitelists:** The security check in `DynamicQueryService` queries
  `sqlite_master` on every request. A short-lived per-tenant cache would be appropriate.

- **Pagination on exports:** Currently the export endpoints support the same `?page=&pageSize=`
  params, but the more useful production pattern is streaming — write rows incrementally to avoid
  holding a 100 000-row result set in memory. `CsvHelper` and `ClosedXML` both support streaming
  writes.

- **OpenAPI / Swagger:** I removed it from the scaffold to keep things clean. In a real project
  I'd add it back with `[ProducesResponseType]` attributes.

- **Integration tests over `WebApplicationFactory`:** The controller tests use Moq mocks, which
  is fast but doesn't exercise the DI wiring, routing, or middleware. A `WebApplicationFactory`
  suite with real in-memory databases would give higher confidence.

- **Proper secrets management:** The SQLite paths are stored as plain strings in config. In
  production these would be pulled from Azure Key Vault / AWS Secrets Manager.

---

## Things I Considered and Rejected

| Idea | Why rejected |
|---|---|
| In-memory EF Core for tenant databases | EF can't represent unknown schemas at compile time |
| Single `metadata.db` for both tenant data and mappings | Defeating the "swap connection string" constraint by coupling data and metadata in one file |
| OData / GraphQL for dynamic querying | Heavy for the stated requirement; adds complexity the exercise doesn't justify |
| Polly retry policies on SQLite queries | SQLite errors are almost always deterministic (bad table name, locked file) — retrying would mask bugs |
| `System.Text.Json` source generation | The response shape uses `object?` values which source generation can't handle without custom converters |

---

## Assumptions Made

- Table names are stable between the metadata seed and runtime (no table renames during a session).
- The "customer-defined column name" feature (Problem 2) stores the mapping in the metadata DB;
  there is no separate endpoint to create/update mappings (seeded via `DatabaseSeeder.cs`).
- "No code changes when swapping connection strings" means the `DatabasePath` in the `Tenants`
  table can be updated to any SQLite (or, with a driver swap, any SQL) database and the API logic
  remains unchanged.
- Paging defaults: page 1, page size 20; max page size 500; max export rows 100 000.
