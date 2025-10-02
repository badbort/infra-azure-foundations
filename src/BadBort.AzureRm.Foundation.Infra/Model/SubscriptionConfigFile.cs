using System.ComponentModel.DataAnnotations;

namespace BadBort.AzureRm.Foundation.Infra.Model;

public class SubscriptionConfigFile
{
    public Dictionary<string, ResourceGroupConfig>? ResourceGroups { get; set; }
    
    /// <summary>
    /// Subscription level role assignments
    /// </summary>
    public List<RoleAssignment>? RoleAssignments { get; set; }
}

public class ResourceGroupConfig
{
    [Required]
    public string? Location { get; set; }
    
    public List<UserAssignedIdentifyConfig>? UserAssignedIdentities { get; set; }
    
    public List<RoleAssignment>? RoleAssignments { get; set; }
    
    public Dictionary<string,string>? Tags { get; set; }
}

public class RoleAssignment
{
    public string? Group { get; set; }
    
    public string? ServicePrinciple { get; set; }
    
    [Required]
    public List<string>? Roles { get; set; }
}

public class UserAssignedIdentifyConfig
{
    [Required]
    public string? Name { get; set; }
    
    public List<FederatedCredential>? FederatedCredentials { get; set; }
}
