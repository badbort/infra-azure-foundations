using System.ComponentModel.DataAnnotations;

namespace BadBort.AzureRm.Foundation.Infra.Model;

public class TenantConfigFile
{
    [Required] public required AdTenantConfig Tenant { get; init; }
}

public class AdTenantConfig
{
    /// <summary>
    /// Map of azure subscription aliases to ids
    /// </summary>
    [Required]
    public Dictionary<string, string>? SubscriptionAliases { get; set; }
    
    /// <summary>
    /// Optional map of alias to Entra user identifier (object id or user principal name).
    /// </summary>
    public Dictionary<string, string>? UserAliases { get; set; }
}
