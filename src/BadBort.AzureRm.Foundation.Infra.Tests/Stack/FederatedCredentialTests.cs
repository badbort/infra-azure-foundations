using System.Collections.Immutable;
using BadBort.AzureRm.Foundation.Infra.Tests.Utility;
using Pulumi;
using Pulumi.Azure.ArmMsi;
using Pulumi.Testing;
using Shouldly;

namespace BadBort.AzureRm.Foundation.Infra.Tests.Stack;

public class FederatedCredentialTests
{
    private readonly ITestOutputHelper _output;

    public FederatedCredentialTests(ITestOutputHelper output)
    {
        _output = output;
    }

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
        await _output.WritePreviewSummaryAsync(resources);

        var fic = resources.OfType<FederatedIdentityCredential>().ShouldHaveSingleItem();

        fic.GetResourceName().ShouldBe("uami-sample-app-fic-main");

        (await fic.Name.GetValueAsync()).ShouldBe("fic-main");
        (await fic.Issuer.GetValueAsync()).ShouldBe("https://token.actions.githubusercontent.com");
        (await fic.Subject.GetValueAsync()).ShouldBe("repo:badbort/infra-azure-foundations:ref:refs/heads/main");
        (await fic.Audience.GetValueAsync()).ShouldBe("api://AzureADTokenExchange");
    }

    [Fact]
    public async Task Deploy_WithUrnFederatedCredentialAndDuplicateFicNamesAcrossUamis_CreatesDistinctUrnSubjects()
    {
        const string yaml = @"
resource_groups:
  sample-rg:
    location: Australia East
    user_assigned_identities:
      - name: uami-alpha
        federated_credentials:
          - name: fic-shared
            issuer: https://issuer.example.com
            subject_identifier: urn:sample:alpha
      - name: uami-beta
        federated_credentials:
          - name: fic-shared
            issuer: https://issuer.example.com
            subject_identifier: urn:sample:beta
";

        using var temp = new FileSystemConventionBuilder()
            .SetupRandomTenantAndSubscription()
            .SetupResources(subPath: "test", yamlContent: yaml)
            .Build()
            .SetPulumiConfig();

        ImmutableArray<Resource> resources = await Deployment.TestAsync<SubscriptionStack>(new EmptyMocks());

        resources.ShouldNotBeEmpty();
        await _output.WritePreviewSummaryAsync(resources);

        var ficsByResourceName = resources
            .OfType<FederatedIdentityCredential>()
            .ToDictionary(fic => fic.GetResourceName(), StringComparer.OrdinalIgnoreCase);

        ficsByResourceName.Count.ShouldBe(2);
        ficsByResourceName.ContainsKey("uami-alpha-fic-shared").ShouldBeTrue();
        ficsByResourceName.ContainsKey("uami-beta-fic-shared").ShouldBeTrue();

        var alphaFic = ficsByResourceName["uami-alpha-fic-shared"];
        var betaFic = ficsByResourceName["uami-beta-fic-shared"];

        (await alphaFic.Name.GetValueAsync()).ShouldBe("fic-shared");
        (await betaFic.Name.GetValueAsync()).ShouldBe("fic-shared");
    }

    [Fact]
    public async Task Deploy_WithUrnFederatedCredentialAndCustomAudience_UsesDistinctUrnSubjectsPerUami()
    {
        const string yaml = @"
resource_groups:
  sample-rg:
    location: Australia East
    user_assigned_identities:
      - name: uami-gamma
        federated_credentials:
          - name: fic-shared
            issuer: https://issuer.example.com
            audience: api://custom-audience
            subject_identifier: urn:sample:gamma
      - name: uami-delta
        federated_credentials:
          - name: fic-shared
            issuer: https://issuer.example.com
            audience: api://custom-audience
            subject_identifier: urn:sample:delta
";

        using var temp = new FileSystemConventionBuilder()
            .SetupRandomTenantAndSubscription()
            .SetupResources(subPath: "test", yamlContent: yaml)
            .Build()
            .SetPulumiConfig();

        ImmutableArray<Resource> resources = await Deployment.TestAsync<SubscriptionStack>(new EmptyMocks());

        resources.ShouldNotBeEmpty();
        await _output.WritePreviewSummaryAsync(resources);

        var ficsByResourceName = resources
            .OfType<FederatedIdentityCredential>()
            .ToDictionary(fic => fic.GetResourceName(), StringComparer.OrdinalIgnoreCase);

        ficsByResourceName.Count.ShouldBe(2);
        ficsByResourceName.ContainsKey("uami-gamma-fic-shared").ShouldBeTrue();
        ficsByResourceName.ContainsKey("uami-delta-fic-shared").ShouldBeTrue();

        var gammaFic = ficsByResourceName["uami-gamma-fic-shared"];
        var deltaFic = ficsByResourceName["uami-delta-fic-shared"];

        (await gammaFic.Name.GetValueAsync()).ShouldBe("fic-shared");
        (await deltaFic.Name.GetValueAsync()).ShouldBe("fic-shared");
    }
}
