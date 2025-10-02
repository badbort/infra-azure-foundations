namespace BadBort.AzureRm.Foundation.Infra;

public static class ConfigKeys
{
    /// <summary>
    /// Root directory which includes tenants.yaml and tenant directories
    /// </summary>
    public const string YamlRoot = "data_dir";
    
    /// <summary>
    /// The specific subscription by alias or ID that will be updated
    /// </summary>
    public const string Subscription = "subscription";
    
    /// <summary>
    /// The specific tenant by alias or ID that will be updated
    /// </summary>
    public const string Tenant = "tenant";
}