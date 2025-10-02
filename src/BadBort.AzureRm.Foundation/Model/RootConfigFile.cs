using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace BadBort.AzureRm.Foundation.Model;

public class RootConfigFile
{
    [Required]
    [Description("Map of aliases to tenant ids")]
    public Dictionary<string, string>? TenantAliases { get; set; }
}

public class AdApplicationClientSecret
{
    
}