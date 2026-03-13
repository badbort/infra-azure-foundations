using BadBort.AzureRm.Foundation.Infra.Model;
using BadBort.AzureRm.Foundation.Infra.Serialization;
using Shouldly;

namespace BadBort.AzureRm.Foundation.Infra.Tests.Deserialization;

public class TenantConfigFileDeserializationTests
{
    [Fact]
    public void Tenant_WithUserAliases_Deserializes()
    {
        const string yaml = @"
tenant:
  subscription_aliases:
    main: bd8e250a-66a6-4038-acd8-0d6aced3e3c8
  user_aliases:
    finops-lead: 11111111-2222-3333-4444-555555555555
    engineering-manager: eng.manager@contoso.example
";

        var cfg = YamlUtility.Deserialize<TenantConfigFile>(yaml);

        cfg.ShouldNotBeNull();
        cfg.Tenant.ShouldNotBeNull();
        cfg.Tenant.SubscriptionAliases.ShouldNotBeNull();
        cfg.Tenant.SubscriptionAliases["main"].ShouldBe("bd8e250a-66a6-4038-acd8-0d6aced3e3c8");
        cfg.Tenant.UserAliases.ShouldNotBeNull();
        cfg.Tenant.UserAliases["finops-lead"].ShouldBe("11111111-2222-3333-4444-555555555555");
        cfg.Tenant.UserAliases["engineering-manager"].ShouldBe("eng.manager@contoso.example");
    }
}
