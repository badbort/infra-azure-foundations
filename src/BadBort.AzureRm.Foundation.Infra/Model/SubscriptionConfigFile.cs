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
    
    /// <summary>
    /// Resource-group scoped cost budgets and alert notifications.
    /// </summary>
    public List<ResourceGroupBudgetConfig>? Budgets { get; set; }
    
    public Dictionary<string,string>? Tags { get; set; }
}

[UsedImplicitly]
public class ResourceGroupBudgetConfig
{
    [Required]
    public string? Name { get; set; }
    
    [Required]
    public decimal? Amount { get; set; }
    
    /// <summary>
    /// Default time grain is Monthly.
    /// </summary>
    public string? TimeGrain { get; set; }
    
    [Required]
    public string? StartDate { get; set; }
    
    public string? EndDate { get; set; }

    public List<BudgetNotificationConfig>? Notifications { get; set; }
}

[UsedImplicitly]
public class BudgetNotificationConfig
{
    [Required]
    public string? Name { get; set; }
    
    [Required]
    public decimal? ThresholdPercent { get; set; }
    
    public bool? Enabled { get; set; }
    
    public string? Operator { get; set; }
    
    public List<string>? ContactEmails { get; set; }
    
    /// <summary>
    /// Azure action group resource IDs.
    /// </summary>
    public List<string>? ContactGroups { get; set; }
    
    /// <summary>
    /// User references that resolve to email: object id (GUID), user principal name, or tenant user alias.
    /// </summary>
    public List<string>? ContactUsers { get; set; }
}

[UsedImplicitly]
public class RoleAssignment
{
    public string? Group { get; set; }
    
    public string? ServicePrincipal { get; set; }
    
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
        if(!string.IsNullOrEmpty(ServicePrincipal))
            return new (IdentityType.ServicePrincipal,  ServicePrincipal);
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
