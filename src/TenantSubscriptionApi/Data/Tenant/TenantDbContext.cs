using Microsoft.EntityFrameworkCore;
using TenantSubscriptionApi.Domain.Entities;

namespace TenantSubscriptionApi.Data.Tenant;

// Deliberately SCHEMA-AGNOSTIC: no HasDefaultSchema, no per-entity schema argument.
// Which Postgres schema this context actually talks to is determined entirely by
// the "Search Path" parameter on the connection string it's constructed with
// (see Program.cs's AddDbContext<TenantDbContext> registration) — Postgres resolves
// the unqualified "Resource" table name via whatever schema is first in that
// connection's search_path. This is what lets every organization share one compiled
// EF Core model instead of needing a distinct IModel per schema.
public class TenantDbContext : DbContext
{
    // Npgsql's EF Core provider resolves the migrations-history-table EXISTENCE CHECK
    // against the "public" schema regardless of the connection's search_path (even
    // though the table itself, once created, is unqualified text that lands wherever
    // search_path points) — confirmed by direct testing. If this name matched
    // PublicDbContext's default history table name, the very first org to onboard
    // AFTER PublicDbContext's own migrations have run would have its existence check
    // find PublicDbContext's table in "public", wrongly conclude history already
    // exists, then fail trying to read a table that was never created in its own
    // (empty) schema. Using a distinct name here means that check always correctly
    // finds nothing in "public", regardless of what PublicDbContext has done.
    public const string MigrationsHistoryTableName = "__TenantMigrationsHistory";

    public TenantDbContext(DbContextOptions<TenantDbContext> options) : base(options) { }

    public DbSet<Resource> Resources => Set<Resource>();
}
