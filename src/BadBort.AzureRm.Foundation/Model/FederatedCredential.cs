using BadBort.AzureRm.Foundation.Serialization;

namespace BadBort.AzureRm.Foundation.Model;

public class FederatedCredential : IPolymorphicType
{
    public string? Type { get; set; }

    public string? Name { get; set; }
    
    public string? Issuer { get; set; }
    
    public string? SubjectIdentifier { get; set; }
    
    public static Dictionary<string, Type> GetTypes()
    {
        return new ()
        {
            {"github", typeof(GitHubFederatedCredential)}
        };
    }

    public virtual FederatedCredential GetPopulatedInstance() => this;
}