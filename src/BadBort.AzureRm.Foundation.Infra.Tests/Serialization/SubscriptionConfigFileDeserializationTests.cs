using BadBort.AzureRm.Foundation.Infra.Model;
using BadBort.AzureRm.Foundation.Infra.Serialization;
using Shouldly;

namespace BadBort.AzureRm.Foundation.Infra.Tests.Deserialization;

public class SubscriptionConfigFileDeserializationTests
{
    private const string SampleYaml = @"
resource_groups:
  sample-test:
    location: Australia East
    
    user_assigned_identities:
      - name: uami-sample-app
        federated_credentials:
          - name: github-badbort-sample-app-main-fic
            type: github
            entity: Branch
            organization: badbort
            repository: example
            branch: main
    
    role_assignments:
      - service_principle: uami-sample-app
        roles:
          - Key Vault Secrets User
";

    [Fact]
    public void DeserializeSampleYaml_WithNestedObjects_AndPolymorphism()
    {
        // act
        var rootConfig = YamlUtility.Deserialize<SubscriptionConfigFile>(SampleYaml);

        rootConfig.ShouldNotBeNull();
        rootConfig.RoleAssignments.ShouldBeNull();

        rootConfig.ResourceGroups.ShouldNotBeNull();
        rootConfig.ResourceGroups!.ContainsKey("sample-test").ShouldBeTrue();

        var rg = rootConfig.ResourceGroups["sample-test"];
        rg.ShouldNotBeNull();

        // NOTE: Name is a property but your YAML uses the dictionary key as the name.
        // Unless you copy the key into the object post-deserialization, Name will be null.
        rg.Location.ShouldBe("Australia East");

        // UAMI + FIC
        rg.UserAssignedIdentities.ShouldNotBeNull();
        rg.UserAssignedIdentities!.Count.ShouldBe(1);

        var uami = rg.UserAssignedIdentities.Single();
        uami.Name.ShouldBe("uami-sample-app");
        uami.FederatedCredentials.ShouldNotBeNull();
        uami.FederatedCredentials!.Count.ShouldBe(1);

        var fic = uami.FederatedCredentials.Single();
        fic.ShouldBeOfType<GitHubFederatedCredential>();
        var gh = (GitHubFederatedCredential)fic;

        gh.Name.ShouldBe("github-badbort-sample-app-main-fic");
        gh.Type.ShouldBe("github");
        gh.Organization.ShouldBe("badbort");
        gh.Repository.ShouldBe("example");
        gh.Branch.ShouldBe("main");
        gh.Entity.ShouldBe(GitHubFicEntity.Branch);

        // RG-level role assignments
        rg.RoleAssignments.ShouldNotBeNull();
        rg.RoleAssignments!.Count.ShouldBe(1);
        var ra = rg.RoleAssignments.Single();
        ra.Group.ShouldBeNull();
        ra.ServicePrincipal.ShouldBe("uami-sample-app");
        ra.Roles.ShouldNotBeNull();
        ra.Roles.ShouldContain("Key Vault Secrets User");
    }

    [Fact]
    public void ResourceGroup_EmptySections_AreNull()
    {
        const string yaml = @"
resource_groups:
  rg-empty:
    location: Australia East
";
        var cfg = YamlUtility.Deserialize<SubscriptionConfigFile>(yaml);

        cfg.ShouldNotBeNull();
        cfg.ResourceGroups.ShouldNotBeNull();
        cfg.ResourceGroups!.ContainsKey("rg-empty").ShouldBeTrue();

        var rg = cfg.ResourceGroups["rg-empty"];
        rg.Location.ShouldBe("Australia East");
        rg.UserAssignedIdentities.ShouldBeNull();
        rg.RoleAssignments.ShouldBeNull();
        cfg.RoleAssignments.ShouldBeNull();
    }

    [Fact]
    public void MultipleResourceGroups_AreDeserialized()
    {
        const string yaml = @"
resource_groups:
  rg-one:
    location: Australia East
  rg-two:
    location: Australia Southeast
";
        var cfg = YamlUtility.Deserialize<SubscriptionConfigFile>(yaml);

        cfg.ShouldNotBeNull();
        cfg.ResourceGroups.ShouldNotBeNull();
        cfg.ResourceGroups.Keys.ShouldBe(["rg-one", "rg-two"], true);
        cfg.ResourceGroups["rg-one"].Location.ShouldBe("Australia East");
        cfg.ResourceGroups["rg-two"].Location.ShouldBe("Australia Southeast");
    }

    [Fact]
    public void ResourceGroup_WithRoleAssignments()
    {
        const string yaml = @"
resource_groups:
  rg-app:
    location: Australia East
    role_assignments:
      - service_principal: uami-app
        roles:
          - Contributor
          - Key Vault Secrets User
";
        var cfg = YamlUtility.Deserialize<SubscriptionConfigFile>(yaml);

        cfg.ShouldNotBeNull();
        var rgRa = cfg.ResourceGroups!["rg-app"].RoleAssignments.ShouldNotBeNull().Single();
        rgRa.ServicePrincipal.ShouldBe("uami-app");
        rgRa.Roles.ShouldBe(["Contributor", "Key Vault Secrets User"], true);
    }

    [Fact]
    public void RoleAssignment_WithFederatedCredentials()
    {
        const string yaml = @"
resource_groups:
  sample-rg:
    location: Australia East
    user_assigned_identities:
      - name: uami
        federated_credentials:
          - name: fic-branch
            type: github
            entity: Branch
            organization: testorg
            repository: testrepo
            branch: main
";
        var cfg = YamlUtility.Deserialize<SubscriptionConfigFile>(yaml);

        var federatedCredential = cfg?.ResourceGroups!["sample-rg"]
            .UserAssignedIdentities!.Single()
            .FederatedCredentials!.Single();
        
        var gh = federatedCredential.ShouldBeOfType<GitHubFederatedCredential>();

        gh.ShouldBeEquivalentTo(new GitHubFederatedCredential
        {
            Name = "fic-branch",
            Type = "github",
            Branch = "main",
            Entity = GitHubFicEntity.Branch,
            Organization = "testorg",
            Repository = "testrepo"
        });
    }
}