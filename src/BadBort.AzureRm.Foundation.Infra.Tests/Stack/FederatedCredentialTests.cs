using System.Collections.Immutable;
using BadBort.AzureRm.Foundation.Infra.Tests.Utility;
using Pulumi;
using Pulumi.Azure.ArmMsi;
using Pulumi.Testing;
using Shouldly;

namespace BadBort.AzureRm.Foundation.Infra.Tests.Stack;

public class FederatedCredentialTests
{
    [Fact]
    public async Task Deploy_WithGitHubFederatedCredential_CreatesExpectedNamingAndValues()
    {
        const string yaml = @"
resource_groups:
  sample-rg:
    location: Australia East
    user_assigned_identities:
      - name: uami-sample-app
        federated_credentials:
          - name: fic-main
            type: github
            entity: Branch
            organization: badbort
            repository: infra-azure-foundations
            branch: main
";

        using var temp = new FileSystemConventionBuilder()
            .SetupRandomTenantAndSubscription()
            .SetupResources(subPath: "test", yamlContent: yaml)
            .Build()
            .SetPulumiConfig();

        ImmutableArray<Resource> resources = await Deployment.TestAsync<SubscriptionStack>(new EmptyMocks());

        resources.ShouldNotBeEmpty();

        var fic = resources.OfType<FederatedIdentityCredential>().ShouldHaveSingleItem();

        fic.GetResourceName().ShouldBe("uami-sample-app-fic-main");

        (await fic.Name.GetValueAsync()).ShouldBe("fic-main");
        (await fic.Issuer.GetValueAsync()).ShouldBe("https://token.actions.githubusercontent.com");
        (await fic.Subject.GetValueAsync()).ShouldBe("repo:badbort/infra-azure-foundations:ref:refs/heads/main");
        (await fic.Audience.GetValueAsync()).ShouldBe("api://AzureADTokenExchange");
    }
}
