using BadBort.AzureRm.Foundation.Infra.Serialization;
using Pulumi;
using Pulumi.Azure;
using Pulumi.Azure.ArmMsi;
using Pulumi.Azure.Authorization;
using Pulumi.Azure.Core;
using Config = Pulumi.Config;

namespace BadBort.AzureRm.Foundation.Infra;

public class SubscriptionStack : Stack
{
    public SubscriptionStack()
    {
        var config = new Config();
        
        string dataDir = config.Require(ConfigKeys.YamlRoot);
        string tenantConfig = config.Require(ConfigKeys.Tenant);
        string subscriptionConfig = config.Require(ConfigKeys.Subscription);

        var dirContext = new FileSystemConvention(dataDir);
        
        var tenantInfo = dirContext.GetTenants().FirstOrDefault(t => t.Id == tenantConfig || t.Alias == tenantConfig);

        if (tenantInfo == null)
        {
            throw new InvalidOperationException("Could not find tenant in data_dir");
        }
        
        var subscriptionInfo = dirContext.GetSubscriptions(tenantInfo).FirstOrDefault(s => s.Id == subscriptionConfig || s.Alias == subscriptionConfig);
        
        if (subscriptionInfo == null)
        {
            throw new InvalidOperationException("Could not find subscription dir in specified tenant");
        }

        Log.Info($"Executing for subscription {subscriptionInfo.Id}");

        var uamiByName = new Dictionary<string, UserAssignedIdentity>();

        var provider = new Provider($"az-{subscriptionInfo.Id}", new ProviderArgs
        {
            SubscriptionId = subscriptionInfo.Id,
        });
        
        foreach (var resourceInfo in subscriptionInfo.Resources)
        {
            foreach (var (resourceGroupName, resourceGroupConfig) in resourceInfo.Config.ResourceGroups ?? new())
            {
                var rg = new ResourceGroup( resourceGroupName, new ResourceGroupArgs
                {
                    Name = resourceGroupName,
                    Location          = resourceGroupConfig.Location!,
                    Tags              = resourceGroupConfig.Tags ?? new InputMap<string>() // if your model includes Tags
                }, new CustomResourceOptions
                {
                    Provider = provider
                });
                
                // Create azure resource group
                // Include resourceGroupConfig.Tags and resourceGroupConfig.Location
                
                var resourceOptions = new CustomResourceOptions
                {
                    Parent = rg
                };

                foreach (var userAssignedIdentity in resourceGroupConfig.UserAssignedIdentities ?? new())
                {
                    // Create a user assigned identity with name userAssignedIdentity.Name
                    // add the uami resource to a string dictionary lookup, as role assignments across resource groups and subscriptions may assign roles to these uamis 
                    var uami = new UserAssignedIdentity(userAssignedIdentity.Name!, new UserAssignedIdentityArgs
                    {
                        ResourceGroupName = rg.Name,
                        Name      = userAssignedIdentity.Name!,
                        Location          = rg.Location,
                        Tags              = resourceGroupConfig.Tags ?? new InputMap<string>()
                        
                    }, resourceOptions);
                    
                    uamiByName[userAssignedIdentity.Name!] = uami;
                    
                    foreach (var federatedCredentialRaw in userAssignedIdentity.FederatedCredentials ?? new ())
                    {
                        // Support for polymorphism
                        var federatedCredential = federatedCredentialRaw.GetPopulatedInstance();
                        
                        // Create federated credentials
                        // federatedCredential.Name
                        // federatedCredential.Issuer
                        // federatedCredential.Type
                        // federatedCredential.SubjectIdentifier
                        
                        _ = new FederatedIdentityCredential(federatedCredential.Name!, new FederatedIdentityCredentialArgs
                        {
                            ResourceGroupName = rg.Name,
                            ParentId = uami.Id,
                            Name      = uami.Name,
                            Issuer   = federatedCredential.Issuer!,
                            Subject  = federatedCredential.SubjectIdentifier!,
                            Audience = federatedCredential.Issuer ?? "api://AzureADTokenExchange"
                        }, new CustomResourceOptions { Provider = provider, Parent = uami });
                    }
                }
            }
        }
        
        // Perform role assignments after all uami resources have been created
        // Assignments may be either a group or a service principle. See: rgRoleAssignments.Group or rgRoleAssignments.ServicePrinciple
        // If a UAMI was not created, we want to do a data lookup on the service principle
        foreach (var resourceInfo in subscriptionInfo.Resources)
        {
            foreach (var (_, resourceGroupConfig) in resourceInfo.Config.ResourceGroups ?? new())
            {
                foreach (var rgRoleAssignments in resourceGroupConfig.RoleAssignments ?? new())
                {
                    foreach (string role in rgRoleAssignments.Roles ?? new())
                    {
                    
                    }
                }
            }
            
            foreach (var subscriptionRoleAssignments in resourceInfo.Config.RoleAssignments ?? new())
            {
                foreach (string role in subscriptionRoleAssignments.Roles ?? new())
                {
                    
                }
            }
        }
    }
}