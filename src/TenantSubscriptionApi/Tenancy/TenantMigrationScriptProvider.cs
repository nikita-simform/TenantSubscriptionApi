using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using TenantSubscriptionApi.Data.Tenant;

namespace TenantSubscriptionApi.Tenancy;

// Generates the tenant schema's full migration history as ONE non-idempotent SQL
// script, once, and caches it — every org onboarded afterward replays the same
// script. Non-idempotent deliberately: a brand-new schema has nothing to guard
// against, and the idempotent form wraps statements in DO $EF$...$EF$ blocks that
// would complicate running the whole thing as one multi-statement command.
public class TenantMigrationScriptProvider : ITenantMigrationScriptProvider
{
    private readonly string _baseConnectionString;
    private string? _cachedScript;

    public TenantMigrationScriptProvider(string baseConnectionString)
    {
        _baseConnectionString = baseConnectionString;
    }

    public string GetScript()
    {
        if (_cachedScript is not null)
            return _cachedScript;

        // A throwaway context purely to reach its IMigrator — the schema this
        // connection string points at doesn't matter, since the generated script
        // for TenantDbContext never references a schema at all (see TenantDbContext.cs).
        var options = new DbContextOptionsBuilder<TenantDbContext>()
            .UseNpgsql(_baseConnectionString, npgsql => npgsql.MigrationsHistoryTable(TenantDbContext.MigrationsHistoryTableName))
            .Options;
        using var context = new TenantDbContext(options);
        var migrator = context.GetService<IMigrator>();
        // NoTransactions: the default script wraps itself in its own START
        // TRANSACTION/COMMIT — running that embedded COMMIT inside the caller's
        // ambient transaction (TenantSchemaProvisioner) would end that outer
        // transaction early (same physical Postgres session), silently breaking the
        // atomicity onboarding depends on. Without its own wrapping, this script is
        // just plain DDL/DML text, safe to run as part of a larger transaction.
        _cachedScript = migrator.GenerateScript(options: MigrationsSqlGenerationOptions.NoTransactions);
        return _cachedScript;
    }
}
