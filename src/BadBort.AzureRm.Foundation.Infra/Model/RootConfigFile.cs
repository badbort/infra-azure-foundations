using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace BadBort.AzureRm.Foundation.Infra.Model;

public class RootConfigFile
{
    [Required]
    [Description("Map of aliases to tenant ids")]
    public Dictionary<string, string>? TenantAliases { get; set; }
}

public class AdApplicationClientSecret
{
    
}