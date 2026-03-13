using BadBort.AzureRm.Foundation.Infra.Model;
using Shouldly;

namespace BadBort.AzureRm.Foundation.Infra.Tests.Model;

public class GitHubFederatedCredentialTests
{
    [Fact]
    public void GetPopulatedInstance_WithBranchEntity_PopulatesExpectedValues()
    {
        var model = new GitHubFederatedCredential
        {
            Name = "fic-branch",
            Type = "github",
            Organization = "badbort",
            Repository = "infra-azure-foundations",
            Entity = GitHubFicEntity.Branch,
            Branch = "main"
        };

        var populated = model.GetPopulatedInstance();

        populated.Name.ShouldBe("fic-branch");
        populated.Type.ShouldBe("github");
        populated.Issuer.ShouldBe("https://token.actions.githubusercontent.com");
        populated.Audience.ShouldBe("api://AzureADTokenExchange");
        populated.SubjectIdentifier.ShouldBe("repo:badbort/infra-azure-foundations:ref:refs/heads/main");
    }

    [Fact]
    public void GetPopulatedInstance_WithAutomaticEntity_UsesConfiguredBranch()
    {
        var model = new GitHubFederatedCredential
        {
            Name = "fic-auto-branch",
            Type = "github",
            Organization = "badbort",
            Repository = "infra-azure-foundations",
            Entity = GitHubFicEntity.Automatic,
            Branch = "release/2026"
        };

        var populated = model.GetPopulatedInstance();

        populated.SubjectIdentifier.ShouldBe("repo:badbort/infra-azure-foundations:ref:refs/heads/release/2026");
    }

    [Fact]
    public void GetPopulatedInstance_WithTagAndBranch_PrefersTagForAutomaticEntity()
    {
        var model = new GitHubFederatedCredential
        {
            Name = "fic-auto-tag",
            Type = "github",
            Organization = "badbort",
            Repository = "infra-azure-foundations",
            Tag = "v1.2.3",
            Branch = "main"
        };

        var populated = model.GetPopulatedInstance();

        populated.SubjectIdentifier.ShouldBe("repo:badbort/infra-azure-foundations:ref:refs/tags/v1.2.3");
    }
}
