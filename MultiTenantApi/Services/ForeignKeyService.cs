using Dapper;
using MultiTenantApi.Models;

namespace MultiTenantApi.Services;

public class ForeignKeyService : IForeignKeyService
{
    private readonly ITenantService _tenantService;
    private readonly ILogger<ForeignKeyService> _logger;

    public ForeignKeyService(ITenantService tenantService, ILogger<ForeignKeyService> logger)
    {
        _tenantService = tenantService;
        _logger = logger;
    }

    public async Task<IEnumerable<ForeignKeyInfo>> GetForeignKeysAsync(string tenantId, string tableName)
    {
        await using var conn = await _tenantService.GetConnectionAsync(tenantId);
        // PRAGMA foreign_key_list returns: id, seq, table, from, to, on_update, on_delete, match
        var pragmaRows = await conn.QueryAsync($"PRAGMA foreign_key_list(\"{tableName}\")");

        return pragmaRows.Select(r => new ForeignKeyInfo
        {
            FromColumn = (string)r.from,
            ToTable    = (string)r.table,
            ToColumn   = (string)r.to
        }).ToList();
    }

    public async Task<IEnumerable<IDictionary<string, object?>>> ExpandForeignKeysAsync(
        string tenantId,
        string tableName,
        IEnumerable<IDictionary<string, object?>> rows)
    {
        var rowList = rows.ToList();
        if (rowList.Count == 0) return rowList;

        var fks = (await GetForeignKeysAsync(tenantId, tableName)).ToList();
        if (fks.Count == 0) return rowList;

        await using var conn = await _tenantService.GetConnectionAsync(tenantId);

        // For each FK, build a lookup map: referenced pk value → display label
        var lookups = new Dictionary<string, Dictionary<object, string>>(StringComparer.OrdinalIgnoreCase);

        foreach (var fk in fks)
        {
            // Discover the "label" column: first non-id TEXT column in the referenced table
            var columnInfoRows = await conn.QueryAsync($"PRAGMA table_info(\"{fk.ToTable}\")");
            var referencedColumns = columnInfoRows.Select(r => new { name = (string)r.name, type = (string)r.type }).ToList();

            var labelColumn = referencedColumns
                .Where(c => !c.name.Equals("id", StringComparison.OrdinalIgnoreCase)
                         && c.type.Contains("TEXT", StringComparison.OrdinalIgnoreCase))
                .Select(c => c.name)
                .FirstOrDefault();

            if (labelColumn is null)
            {
                _logger.LogDebug("No suitable label column found in '{ToTable}' — FK expansion skipped", fk.ToTable);
                continue;
            }

            var refRows = await conn.QueryAsync(
                $"SELECT \"{fk.ToColumn}\", \"{labelColumn}\" FROM \"{fk.ToTable}\"");

            var lookup = new Dictionary<object, string>();
            foreach (var r in refRows)
            {
                var pk = ((IDictionary<string, object?>)r)[fk.ToColumn];
                var label = ((IDictionary<string, object?>)r)[labelColumn]?.ToString() ?? string.Empty;
                if (pk is not null) lookup[pk] = label;
            }
            lookups[fk.FromColumn] = lookup;
        }

        // Replace FK values in each row
        var expanded = rowList.Select(row =>
        {
            var newRow = new Dictionary<string, object?>(row, StringComparer.OrdinalIgnoreCase);
            foreach (var fk in fks)
            {
                if (!lookups.TryGetValue(fk.FromColumn, out var lookup)) continue;
                if (!newRow.TryGetValue(fk.FromColumn, out var rawVal) || rawVal is null) continue;
                if (lookup.TryGetValue(rawVal, out var label))
                    newRow[fk.FromColumn] = label;
            }
            return (IDictionary<string, object?>)newRow;
        }).ToList();

        return expanded;
    }
}
