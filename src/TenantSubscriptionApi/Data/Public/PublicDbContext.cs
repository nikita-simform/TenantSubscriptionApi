using Microsoft.EntityFrameworkCore;
using TenantSubscriptionApi.Domain.Entities;

namespace TenantSubscriptionApi.Data.Public;

// Control-plane data, shared across every tenant: which orgs exist, what plan
// each is on, who belongs to which org, and the audit trail. Always lives in
// Postgres's "public" schema — unlike TenantDbContext, this one's schema is
// fixed and known at compile time, so HasDefaultSchema is appropriate here.
public class PublicDbContext : DbContext
{
    public PublicDbContext(DbContextOptions<PublicDbContext> options) : base(options) { }

    public DbSet<Organization> Organizations => Set<Organization>();
    public DbSet<Plan> Plans => Set<Plan>();
    public DbSet<User> Users => Set<User>();
    public DbSet<Invite> Invites => Set<Invite>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("public");

        modelBuilder.Entity<Organization>(entity =>
        {
            entity.HasIndex(o => o.Name).IsUnique();
            entity.HasIndex(o => o.SchemaName).IsUnique();
            entity.HasOne(o => o.Plan).WithMany().HasForeignKey(o => o.PlanId);
        });

        modelBuilder.Entity<Plan>(entity =>
        {
            entity.HasIndex(p => p.Name).IsUnique();
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.HasIndex(u => u.Email).IsUnique();
            entity.HasOne(u => u.Organization)
                  .WithMany()
                  .HasForeignKey(u => u.OrganizationId)
                  .IsRequired(false);
        });
    }
}
