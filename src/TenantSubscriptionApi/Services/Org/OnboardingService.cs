using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using TenantSubscriptionApi.Data.Public;
using TenantSubscriptionApi.Domain.Entities;
using TenantSubscriptionApi.Domain.Enums;
using TenantSubscriptionApi.Domain.Exceptions;
using TenantSubscriptionApi.Logging;
using TenantSubscriptionApi.Tenancy;

namespace TenantSubscriptionApi.Services.Org;

public record OnboardingResult(Guid OrganizationId, string SchemaName, Guid AdminUserId);

public class OnboardingService
{
    private readonly string _publicConnectionString;
    private readonly TenantSchemaProvisioner _provisioner;
    private readonly AuditLogger _auditLogger;
    private readonly PasswordHasher<User> _passwordHasher = new();

    public OnboardingService(string publicConnectionString, TenantSchemaProvisioner provisioner, AuditLogger auditLogger)
    {
        _publicConnectionString = publicConnectionString;
        _provisioner = provisioner;
        _auditLogger = auditLogger;
    }

    public async Task<OnboardingResult> OnboardOrganizationAsync(
        string organizationName, string adminEmail, string adminPassword, string planName = "Free")
    {
        var schemaName = SchemaNameGenerator.Derive(organizationName);

        await using var connection = new NpgsqlConnection(_publicConnectionString);
        await connection.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();

        try
        {
            var options = new DbContextOptionsBuilder<PublicDbContext>().UseNpgsql(connection).Options;
            await using var publicDb = new PublicDbContext(options);
            await publicDb.Database.UseTransactionAsync(transaction);

            var plan = await publicDb.Plans.FirstOrDefaultAsync(p => p.Name == planName)
                ?? throw new ValidationException($"Plan '{planName}' does not exist.");

            // CREATE SCHEMA + full migration replay, inside the SAME transaction as the
            // Organization/User insert below — one failure anywhere rolls back everything,
            // including the schema itself (Postgres DDL is transactional).
            await _provisioner.ProvisionAsync(connection, (NpgsqlTransaction)transaction, schemaName);

            var organization = new Organization
            {
                Id = Guid.NewGuid(),
                Name = organizationName,
                SchemaName = schemaName,
                PlanId = plan.Id,
                UserCount = 1
            };

            var admin = new User
            {
                Id = Guid.NewGuid(),
                Email = adminEmail,
                Role = UserRole.OrgAdmin,
                OrganizationId = organization.Id
            };
            admin.PasswordHash = _passwordHasher.HashPassword(admin, adminPassword);

            publicDb.Organizations.Add(organization);
            publicDb.Users.Add(admin);
            await publicDb.SaveChangesAsync();

            await transaction.CommitAsync();

            await _auditLogger.RecordAsync("org.onboarded", organization.Id, admin.Id,
                new { organization.Name, organization.SchemaName });

            return new OnboardingResult(organization.Id, schemaName, admin.Id);
        }
        catch (PostgresException ex) when (ex.SqlState is PostgresErrorCodes.UniqueViolation or PostgresErrorCodes.DuplicateSchema)
        {
            // Disposing an uncommitted transaction rolls it back automatically — this
            // catch only translates the low-level Postgres error into our domain exception.
            throw new ValidationException(
                "An organization with this name already exists.");
        }
    }
}
