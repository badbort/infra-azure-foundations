using BadBort.AzureRm.Foundation.Infra.Model;

namespace BadBort.AzureRm.Foundation.Infra.Serialization;

public class FileSystemConvention
{
    public const string TenantsFile = "tenants.yaml";

    public DirectoryInfo Root { get; }

    public FileSystemConvention(string rootDirectory)
    {
        Root = new DirectoryInfo(rootDirectory);
    }
    
    public string GetRootConfigFile() => Path.Combine(Root.FullName, TenantsFile);

    public string GetTenantDirectory(string tenantAlias) => Path.Combine(Root.FullName, tenantAlias);

    public string GetSubscriptionDirectory(string tenantAlias, string subscriptionAlias) => Path.Combine(Root.FullName, tenantAlias, subscriptionAlias);
    
    public string GetResourceGroupDirectory(string tenantAlias, string subscriptionAlias, string subPath) => Path.Combine(Root.FullName, tenantAlias, subscriptionAlias, subPath);

    public List<TenantInfo> GetTenants()
    {
        var cfgFilePath = GetRootConfigFile();

        var tenants = new List<TenantInfo>();

        if (!File.Exists(cfgFilePath))
            return tenants;

        var rootCfg = YamlUtility.DeserializeFromFile<RootConfigFile>(cfgFilePath);

        if (rootCfg?.TenantAliases == null)
            return tenants;

        foreach (var (tenantAlias, tid) in rootCfg.TenantAliases)
        {
            var tenantDir = GetTenantDirectory(tenantAlias);
            var tenantInfoPath = Path.Combine(tenantDir, "tenant.yaml");

            if (!File.Exists(tenantInfoPath))
                continue;

            var tenantCfg = YamlUtility.DeserializeFromFile<TenantConfigFile>(tenantInfoPath);

            if (tenantCfg == null)
                continue;
            
            var tenantInfo = new TenantInfo
            {
                Alias = tenantAlias,
                Directory = tenantDir,
                Config = tenantCfg,
                Id = tid
            };

            tenants.Add(tenantInfo);
        }

        return tenants;
    }

    public List<SubscriptionInfo> GetSubscriptions(TenantInfo tenant)
    {
        var subInfos = new List<SubscriptionInfo>();

        foreach (var (subAlias, subId) in tenant.Config.Tenant?.SubscriptionAliases ?? new())
        {
            var subDir = GetSubscriptionDirectory(tenant.Alias, subAlias);

            if (!Directory.Exists(subDir))
                continue;

            var resourceFiles = Directory.GetFiles(subDir, "*.yaml", SearchOption.AllDirectories);

            var subscriptionInfo = new SubscriptionInfo
            {
                Alias = subAlias,
                Directory = subDir,
                Id = subId,
                TenantInfo = tenant
            };

            foreach (var resourceFile in resourceFiles)
            {
                var resourceCfg = YamlUtility.DeserializeFromFile<SubscriptionConfigFile>(resourceFile);
                var parentDir = Path.GetDirectoryName(resourceFile);

                if (resourceCfg == null)
                {
                    continue;
                }
                
                subscriptionInfo.Resources.Add(new SubscriptionResourcesInfo
                {
                    File = resourceFile,
                    Directory = Path.GetDirectoryName(resourceFile)!,
                    Config = resourceCfg,
                    Category = Path.GetRelativePath(subDir, parentDir!),
                    SubscriptionInfo = subscriptionInfo
                });
            }

            subInfos.Add(subscriptionInfo);
        }

        return subInfos;
    }
}

public record TenantInfo
{
    public required string Alias { get; init; }
    public required string Id { get; init; }
    public required TenantConfigFile Config { get; init; }
    public required string Directory { get; init; }
}

public record SubscriptionInfo
{
    public required string Alias { get; init; }
    public required string Id { get; init; }
    public required string Directory { get; init; }
    public required TenantInfo TenantInfo { get; init; }
    
    /// <summary>
    /// Many yaml files can be found under a subscription directory. Each file is represented as one <see cref="SubscriptionResourcesInfo"/>
    /// </summary>
    public List<SubscriptionResourcesInfo> Resources { get; } = new();
}

public record SubscriptionResourcesInfo
{
    public required string? Category { get; init; }
    public required SubscriptionConfigFile Config { get; init; }
    public required string File { get; init; }
    public required string Directory { get; init; }
    public required SubscriptionInfo SubscriptionInfo { get; init; }
}