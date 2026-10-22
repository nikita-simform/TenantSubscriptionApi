using NetArchTest.Rules;
using TenantSubscriptionApi.Services.Org;
using Xunit;

namespace TenantSubscriptionApi.Tests;

// Converts "we were disciplined about constructors" into something that fails the
// build. PlatformAdminService (M5) doesn't exist yet, but this rule is written to
// cover the general case — every service namespace EXCEPT Services.Tenant — so it
// already holds true today and will keep holding once Platform services are added,
// without needing to be rewritten.
public class ArchitectureTests
{
    [Fact]
    public void NonTenantServices_NeverDependOn_TenantDbContext()
    {
        var result = Types.InAssembly(typeof(OnboardingService).Assembly)
            .That().ResideInNamespaceStartingWith("TenantSubscriptionApi.Services")
            .And().DoNotResideInNamespace("TenantSubscriptionApi.Services.Tenant")
            .ShouldNot().HaveDependencyOn("TenantSubscriptionApi.Data.Tenant")
            .GetResult();

        Assert.True(result.IsSuccessful, string.Join(", ", result.FailingTypeNames ?? Enumerable.Empty<string>()));
    }
}
