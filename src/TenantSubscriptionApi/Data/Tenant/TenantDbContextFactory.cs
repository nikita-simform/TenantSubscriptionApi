using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace TenantSubscriptionApi.Data.Tenant;

// Used ONLY by `dotnet ef migrations add/script --context TenantDbContext` at design
// time. The real app never calls this — Program.cs's AddDbContext<TenantDbContext>
// resolves a per-request connection via ITenantContext instead, which doesn't exist
// outside an HTTP request. EF Core picks this factory up automatically when present,
// bypassing the DI-based discovery that would otherwise fail at design time.
public class TenantDbContextFactory : IDesignTimeDbContextFactory<TenantDbContext>
{
    public TenantDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<TenantDbContext>();
        optionsBuilder.UseNpgsql(
            "Host=localhost;Port=5432;Database=tenant_subscription_poc;Username=postgres;Password=postgres",
            npgsql => npgsql.MigrationsHistoryTable(TenantDbContext.MigrationsHistoryTableName));
        return new TenantDbContext(optionsBuilder.Options);
    }
}
