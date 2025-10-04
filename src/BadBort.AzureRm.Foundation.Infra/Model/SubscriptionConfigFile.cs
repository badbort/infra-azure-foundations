using System.ComponentModel.DataAnnotations;
using JetBrains.Annotations;

namespace BadBort.AzureRm.Foundation.Infra.Model;

[UsedImplicitly]
public class SubscriptionConfigFile
{
    public Dictionary<string, ResourceGroupConfig>? ResourceGroups { get; set; }
    
    /// <summary>
    /// Subscription level role assignments
    /// </summary>
    public List<RoleAssignment>? RoleAssignments { get; set; }
}

[UsedImplicitly]
public class ResourceGroupConfig
{
    [Required]
    public string? Location { get; set; }
    
    public List<UserAssignedIdentifyConfig>? UserAssignedIdentities { get; set; }
    
    public List<RoleAssignment>? RoleAssignments { get; set; }
    
    public Dictionary<string,string>? Tags { get; set; }
}

[UsedImplicitly]
public class RoleAssignment
{
    public string? Group { get; set; }
    
    public string? ServicePrinciple { get; set; }
    
    [Required]
    public List<string>? Roles { get; set; }
    
    /// <summary>
    /// Optional description to apply to all role assignments
    /// </summary>
    public string? Description { get; set; }
    
    public IdentityInfo? GetIdentityInfo()
    {
        if(!string.IsNullOrEmpty(Group))
            return new (IdentityType.Group, Group);
        if(!string.IsNullOrEmpty(ServicePrinciple))
            return new (IdentityType.ServicePrincipal,  ServicePrinciple);
        return null;
    }
}

[UsedImplicitly]
public class SpecificRoleAssignment
{
    public string? Group { get; set; }
    
    public string? ServicePrincipal { get; set; }

    public string? Description { get; set; }

    public IdentityInfo? GetIdentityInfo()
    {
        if(!string.IsNullOrEmpty(Group))
            return new (IdentityType.Group, Group);
        if(!string.IsNullOrEmpty(ServicePrincipal))
            return new (IdentityType.ServicePrincipal,  ServicePrincipal);
        return null;
    }
}

public record IdentityInfo(IdentityType? IdentityType, string? Name);

[UsedImplicitly]
public class UserAssignedIdentifyConfig
{
    [Required]
    public string? Name { get; set; }
    
    public List<FederatedCredential>? FederatedCredentials { get; set; }
    
    /// <summary>
    /// List of identities that will be granted the Managed Identity Operator role.
    /// </summary>
    public List<SpecificRoleAssignment>? ManagedIdentityOperators { get; set; }
}

public enum IdentityType
{
    ServicePrincipal,
    Group
}