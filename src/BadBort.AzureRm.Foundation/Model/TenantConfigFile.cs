using System.ComponentModel.DataAnnotations;

namespace BadBort.AzureRm.Foundation.Model;

public class TenantConfigFile
{
    [Required]
    public required AdTenantConfig Tenant {get; init; }
}

public class AdTenantConfig
{
    /// <summary>
    /// Map of azure subscription ids to an alias
    /// </summary>
    [Required]
    public Dictionary<string, string>? SubscriptionAliases { get; set; }
}