namespace TenantSubscriptionApi.Domain.Entities;

// Static reference data — "Free", "Pro", etc. Lives in the shared public schema.
public class Plan
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public int MaxUsers { get; set; }
    public int MaxStorageMB { get; set; }
}
