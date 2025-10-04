using System.ComponentModel.DataAnnotations;

namespace BadBort.AzureRm.Foundation.Infra.Model;

public class GitHubFederatedCredential : FederatedCredential
{
    [Required]
    public string? Organization { get; set; }
    
    [Required]
    public string? Repository { get; set; }
    
    public GitHubFicEntity? Entity { get; set; }
    
    public string? Tag { get; set; }
    
    public string? Branch { get; set; }
    
    public string? Environment { get; set; }


    public override FederatedCredential GetPopulatedInstance()
    {
        return new FederatedCredential
        {
            Name = Name,
            Issuer = "https://token.actions.githubusercontent.com",
            SubjectIdentifier = GetSubjectIssuer(),
            Type = Type
        };
    }

    private string? GetSubjectIssuer()
    {
        var entity = Entity;

        if (entity == null || entity == GitHubFicEntity.Automatic)
        {
            if(!string.IsNullOrEmpty(Tag))
                entity = GitHubFicEntity.Tag;
            else if(!string.IsNullOrEmpty(Branch))
                entity = GitHubFicEntity.Branch;
            else if(!string.IsNullOrEmpty(Environment))
                entity = GitHubFicEntity.Environment;
        }

        if (entity == null || entity == GitHubFicEntity.Automatic)
        {
            return null;
        }

        switch (entity)
        {
            case GitHubFicEntity.Environment:
                return $"repo:{Organization}/{Repository}:environment:{Environment}";
            case GitHubFicEntity.Branch:
                return $"repo:{Organization}/{Repository}:ref:refs/heads/{Branch}";
            case GitHubFicEntity.PullRequest:
                return $"repo:{Organization}/{Repository}:pull_request";
            case GitHubFicEntity.Tag:
                return $"repo:{Organization}/{Repository}:ref:refs/tags/{Tag}";
            default:
                return null;
        }
    }
}