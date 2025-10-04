using Bogus;

namespace BadBort.AzureRm.Foundation.Infra.Tests.Utility;

public static class TestExtensions
{
    private static readonly Faker Faker = new("en");
    
    public static FileSystemConventionBuilder SetupRandomTenantAndSubscription(this FileSystemConventionBuilder builder)
    {
        builder.SetupTenant(Guid.NewGuid().ToString("D"), GenerateTenantName());
        builder.SetupSubscription(Guid.NewGuid().ToString("D"), GenerateSubscriptionName());
        
        return builder;
    }
    
    public static FileSystemConventionBuilder SetupRandomSubscription(this FileSystemConventionBuilder builder)
    {
        builder.SetupSubscription(Guid.NewGuid().ToString("D"), GenerateSubscriptionName());
        
        return builder;
    }
    
    public static string GenerateTenantName()
    {
        // Combines a fake company name and a short domain-style suffix
        var company = Faker.Company.CompanyName()
            .Split(' ')[0]               // just first word (e.g. "Contoso")
            .ToLowerInvariant();

        var suffix = Faker.Hacker.Noun()
            .Replace(" ", "-")
            .ToLowerInvariant();

        return $"{company}-{suffix}";
    }

    public static string GenerateSubscriptionName()
    {
        // More product/env style subscription names
        var product = Faker.Commerce.ProductName()
            .Split(' ')[0]
            .ToLowerInvariant();

        var env = Faker.PickRandom(new[] { "dev", "test", "uat", "prod", "infra" });

        return $"{product}-{env}";
    }
}