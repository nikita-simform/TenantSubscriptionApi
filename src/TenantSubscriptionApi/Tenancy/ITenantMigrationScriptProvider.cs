namespace TenantSubscriptionApi.Tenancy;

// Seam for tests: a fake implementation can return deliberately broken SQL to prove
// onboarding rolls back cleanly on a mid-replay failure (see OnboardingRollbackTests).
public interface ITenantMigrationScriptProvider
{
    string GetScript();
}
