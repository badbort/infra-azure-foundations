using System.Text.Json;
using BadBort.AzureRm.Foundation.Infra.Model;
using BadBort.AzureRm.Foundation.Infra.Serialization;

namespace BadBort.AzureRm.Foundation.Infra.Tests.Utility;

public class ConventionUtility
{
}

public sealed class FileSystemConventionBuilder : IDisposable
{
    public string Root { get; } = Path.Combine(Path.GetTempPath(), "pulumi-it", Guid.NewGuid().ToString("N"));


    private TenantInfo? _tenantInfo;

    private readonly List<SubscriptionInfo> _subscriptions = new();

    private SubscriptionInfo? _currentSubscription;

    public FileSystemConventionBuilder SetupTenant(string tenantId, string tenantAlias)
    {
        _tenantInfo = new TenantInfo
        {
            Alias = tenantAlias,
            Id = tenantId,
            Config = new TenantConfigFile
            {
                Tenant = new AdTenantConfig
                {
                }
            },
            Directory = FileSystemConvention.GetTenantDirectory(Root, tenantAlias)
        };

        Directory.CreateDirectory(_tenantInfo.Directory);
        return this;
    }

    public FileSystemConventionBuilder SetupSubscription(string subscriptionId, string subscriptionAlias)
    {
        if (_tenantInfo == null)
            throw new InvalidOperationException("Tenant not setup");

        var subInfo = new SubscriptionInfo
        {
            Alias = subscriptionAlias,
            Id = subscriptionId,
            Directory = FileSystemConvention.GetSubscriptionDirectory(Root, _tenantInfo.Alias, subscriptionAlias),
            TenantInfo = _tenantInfo
        };

        Directory.CreateDirectory(subInfo.Directory);

        if (_subscriptions.Any(s => s.Alias == subInfo.Alias))
            throw new InvalidOperationException("Subscription already setup");

        _subscriptions.Add(subInfo);
        _currentSubscription = subInfo;
        return this;
    }

    public FileSystemConventionBuilder Build()
    {
        if (_tenantInfo == null)
            throw new InvalidOperationException("Tenant not setup");
        
        _tenantInfo.Config.Tenant.SubscriptionAliases = _subscriptions.ToDictionary(s => s.Alias, s => s.Id);

        var root = new RootConfigFile
        {
            TenantAliases = new Dictionary<string, string>
            {
                {_tenantInfo.Alias, _tenantInfo.Id}
            }
        };
        
        YamlUtility.SerializeToFile(Path.Combine(Root, "tenants.yaml"), root);
        YamlUtility.SerializeToFile(Path.Combine(_tenantInfo.Directory, "tenant.yaml"), _tenantInfo.Config);

        return this;
    }

    public FileSystemConventionBuilder SetPulumiConfig()
    {
        if (_tenantInfo == null)
            throw new InvalidOperationException("Tenant not setup");
        
        var cfg = new Dictionary<string, object?>
        {
            ["project:data_dir"] = Root,
            ["project:tenant"] = _tenantInfo.Alias,
        };
        
        var envJson = JsonSerializer.Serialize(cfg);
        Environment.SetEnvironmentVariable("PULUMI_CONFIG", envJson);

        return this;
    }

    public FileSystemConventionBuilder SetupResources(string? subscriptionAlias = null, string? subPath = null, SubscriptionConfigFile? config = null, string? yamlContent = null)
    {
        var subInfo = subscriptionAlias != null ? _subscriptions.Single(s => s.Alias == subscriptionAlias) : _currentSubscription;

        if (subInfo == null)
            throw new InvalidOperationException("Subscription not setup or not found");
        
        if (_tenantInfo == null)
            throw new InvalidOperationException("Tenant not setup");
        
        var rgDir = FileSystemConvention.GetResourceGroupDirectory(Root, _tenantInfo.Alias, subInfo.Alias, subPath);
        var rgFile = Path.Combine(rgDir, "rg.yaml");
        Directory.CreateDirectory(rgDir);
        
        if (yamlContent != null)
        {
            File.WriteAllText(rgFile, yamlContent);
        }
        else if (config != null)
        {
            YamlUtility.SerializeToFile(rgFile, config);
        }
        
        return this;
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
        else if (config != null)
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