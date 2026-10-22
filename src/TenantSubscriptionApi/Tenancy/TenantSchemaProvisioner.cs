using Npgsql;

namespace TenantSubscriptionApi.Tenancy;

// Runs CREATE SCHEMA + the full tenant migration replay as part of an AMBIENT
// transaction the caller already opened (see OnboardingService) — NOT via
// DbContext.Database.MigrateAsync(), which applies each migration in its own
// transaction and would leave an orphaned partially-migrated schema if a later
// migration failed. Postgres DDL is transactional, so if anything in this method
// throws, the caller's transaction rollback undoes the CREATE SCHEMA too.
public class TenantSchemaProvisioner
{
    private readonly ITenantMigrationScriptProvider _scriptProvider;

    public TenantSchemaProvisioner(ITenantMigrationScriptProvider scriptProvider)
    {
        _scriptProvider = scriptProvider;
    }

    public async Task ProvisionAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, string schemaName)
    {
        // schemaName must already be validated by SchemaNameGenerator.AssertSafe —
        // this method trusts its caller, since schema names can't be parameterized
        // like ordinary values once they're part of DDL text.
        await ExecuteAsync(connection, transaction, $"CREATE SCHEMA \"{schemaName}\"");

        // SET LOCAL lasts only for this transaction — once it commits or rolls back,
        // the connection's search_path reverts, so it can't leak into later unrelated work.
        await ExecuteAsync(connection, transaction, $"SET LOCAL search_path TO \"{schemaName}\"");

        // The tenant migration script contains unqualified "CREATE TABLE ..." statements
        // (TenantDbContext has no schema annotations) — they resolve against whichever
        // schema is first in search_path, which is why the SET LOCAL above must run first.
        await ExecuteAsync(connection, transaction, _scriptProvider.GetScript());
    }

    private static async Task ExecuteAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, string sql)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync();
    }
}
