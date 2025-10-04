using BadBort.AzureRm.Foundation.Infra.Model;
using BadBort.AzureRm.Foundation.Infra.Serialization;

namespace BadBort.AzureRm.Foundation.Infra.Tests.Utility;

public class ConventionUtility
{
}

public sealed class TempData : IDisposable
{
    public string Root { get; } = Path.Combine(Path.GetTempPath(), "pulumi-it", Guid.NewGuid().ToString("N"));
    

    public void WriteTenantAndSubscription(string tenantAlias, string tenantId, string subscriptionAlias, string subscriptionId)
    {
        WriteSubscription(subscriptionAlias, subscriptionId);
    }

    public void WriteSubscription(string subscriptionAlias, string subscriptionId)
    {
    }
    
    public void WriteTenantAndSubscription(
        string tenantAlias,
        string tenantId,
        string subAlias,
        string subId,
        SubscriptionConfigFile? config = null,
        string? yamlContent = null)
    {
        Directory.CreateDirectory(Root);
        var tenantDir = Path.Combine(Root, tenantAlias);
        Directory.CreateDirectory(tenantDir);

        var subDir = Path.Combine(tenantDir, subAlias);
        Directory.CreateDirectory(subDir);

        var convention = new FileSystemConvention(Root);

        var rootConfig = new RootConfigFile
        {
            TenantAliases = new()
            {
                { tenantAlias, tenantId }
            }
        };

        YamlUtility.SerializeToFile(convention.GetRootConfigFile(), rootConfig);

        var tenantConfig = new TenantConfigFile
        {
            Tenant = new()
            {
                SubscriptionAliases = new()
                {
                    { subAlias, subId }
                }
            }
        };

        YamlUtility.SerializeToFile(Path.Combine(convention.GetTenantDirectory(tenantAlias), "tenant.yaml"), tenantConfig);
        
        var rgFile = Path.Combine(convention.GetSubscriptionDirectory(tenantAlias, subAlias), "test", "rg.yaml");
        Directory.CreateDirectory(Path.GetDirectoryName(rgFile)!);
        
        if (yamlContent != null)
        {
            File.WriteAllText(rgFile, yamlContent);
        }
        else if(config != null)
        {
            YamlUtility.SerializeToFile(rgFile, config);
        }
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(Root)) 
                Directory.Delete(Root, recursive: true);
        }
        catch
        {
            // ignored
        }
    }
}